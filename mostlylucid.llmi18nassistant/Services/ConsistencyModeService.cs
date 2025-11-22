using System.Collections.Concurrent;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Mostlylucid.LlmI18nAssistant.Models;

namespace Mostlylucid.LlmI18nAssistant.Services;

/// <summary>
///     Service for maintaining translation consistency using RAG over glossary and translation memory
/// </summary>
public class ConsistencyModeService : IConsistencyModeService
{
    private readonly LlmI18nAssistantConfig _config;
    private readonly IEmbeddingGenerator _embeddingGenerator;
    private readonly ConcurrentDictionary<string, GlossaryEntry> _glossaryEntries = new();
    private readonly ILogger<ConsistencyModeService> _logger;
    private readonly ConcurrentDictionary<string, TranslationMemoryEntry> _sessionMemory = new();

    public ConsistencyModeService(
        ILogger<ConsistencyModeService> logger,
        IOptions<LlmI18nAssistantConfig> config,
        IEmbeddingGenerator embeddingGenerator)
    {
        _logger = logger;
        _config = config.Value;
        _embeddingGenerator = embeddingGenerator;
    }

    /// <inheritdoc />
    public async Task InitializeForFileAsync(ResourceFile resourceFile, string targetLanguage,
        CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Initializing consistency mode for file {FileName} to {Language}",
            resourceFile.FileName, targetLanguage);

        // Clear session memory for new file
        _sessionMemory.Clear();

        // Load glossary if configured and not already loaded
        if (!string.IsNullOrEmpty(_config.ConsistencyMode.GlossaryPath) &&
            Directory.Exists(_config.ConsistencyMode.GlossaryPath) &&
            _glossaryEntries.IsEmpty)
            await LoadGlossaryAsync(_config.ConsistencyMode.GlossaryPath, cancellationToken);

        // Pre-compute embeddings for glossary entries if not already done
        foreach (var entry in _glossaryEntries.Values.Where(e => e.Embedding == null))
            try
            {
                entry.Embedding = await _embeddingGenerator.GenerateEmbeddingAsync(
                    entry.SourceTerm, cancellationToken);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to generate embedding for glossary term: {Term}",
                    entry.SourceTerm);
            }
    }

    /// <inheritdoc />
    public async Task LoadGlossaryAsync(string glossaryPath, CancellationToken cancellationToken = default)
    {
        if (File.Exists(glossaryPath))
        {
            await LoadGlossaryFileAsync(glossaryPath, cancellationToken);
        }
        else if (Directory.Exists(glossaryPath))
        {
            var files = Directory.GetFiles(glossaryPath, "*.json", SearchOption.AllDirectories);
            foreach (var file in files) await LoadGlossaryFileAsync(file, cancellationToken);
        }
        else
        {
            _logger.LogWarning("Glossary path not found: {Path}", glossaryPath);
        }

        _logger.LogInformation("Loaded {Count} glossary entries", _glossaryEntries.Count);
    }

    /// <inheritdoc />
    public async Task<List<ContextEntry>> FindSimilarEntriesAsync(
        string sourceText,
        string sourceLanguage,
        string targetLanguage,
        CancellationToken cancellationToken = default)
    {
        var results = new List<(ContextEntry Entry, float Similarity)>();

        try
        {
            // Generate embedding for source text
            var sourceEmbedding = await _embeddingGenerator.GenerateEmbeddingAsync(sourceText, cancellationToken);

            // Search glossary
            foreach (var glossaryEntry in _glossaryEntries.Values)
            {
                if (glossaryEntry.Embedding == null)
                    continue;

                var translation = glossaryEntry.GetTranslation(targetLanguage);
                if (translation == null)
                    continue;

                var similarity = CalculateCosineSimilarity(sourceEmbedding, glossaryEntry.Embedding);

                if (similarity >= _config.ConsistencyMode.MinRelevance)
                    results.Add((new ContextEntry
                    {
                        SourceText = glossaryEntry.SourceTerm,
                        TranslatedText = translation,
                        Similarity = similarity,
                        Source = ContextSource.Glossary
                    }, similarity));
            }

            // Search session memory
            foreach (var memoryEntry in _sessionMemory.Values)
            {
                if (memoryEntry.TargetLanguage != targetLanguage || memoryEntry.Embedding == null)
                    continue;

                var similarity = CalculateCosineSimilarity(sourceEmbedding, memoryEntry.Embedding);

                if (similarity >= _config.ConsistencyMode.MinRelevance)
                    results.Add((new ContextEntry
                    {
                        SourceText = memoryEntry.SourceText,
                        TranslatedText = memoryEntry.TranslatedText,
                        Similarity = similarity,
                        Source = ContextSource.SameFile
                    }, similarity));
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to find similar entries for text: {Text}",
                sourceText.Length > 50 ? sourceText[..50] + "..." : sourceText);
            return [];
        }

        // Sort by similarity and take top K
        return results
            .OrderByDescending(r => r.Similarity)
            .Take(_config.ConsistencyMode.TopK)
            .Select(r => r.Entry)
            .ToList();
    }

    /// <inheritdoc />
    public async Task StoreTranslationAsync(
        string sourceText,
        string translatedText,
        string sourceLanguage,
        string targetLanguage,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var embedding = await _embeddingGenerator.GenerateEmbeddingAsync(sourceText, cancellationToken);

            var entry = new TranslationMemoryEntry
            {
                SourceText = sourceText,
                SourceLanguage = sourceLanguage,
                TargetLanguage = targetLanguage,
                TranslatedText = translatedText,
                Embedding = embedding,
                Source = "session"
            };

            _sessionMemory.TryAdd(entry.Id, entry);

            _logger.LogDebug("Stored translation in session memory: {Source} -> {Target}",
                sourceText.Length > 30 ? sourceText[..30] + "..." : sourceText,
                translatedText.Length > 30 ? translatedText[..30] + "..." : translatedText);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to store translation in memory");
        }
    }

    /// <inheritdoc />
    public Task<GlossaryEntry?> GetGlossaryEntryAsync(
        string term,
        string targetLanguage,
        CancellationToken cancellationToken = default)
    {
        var normalizedTerm = term.ToLowerInvariant().Trim();

        var entry = _glossaryEntries.Values
            .FirstOrDefault(e => e.SourceTerm.Equals(normalizedTerm, StringComparison.OrdinalIgnoreCase) &&
                                 e.Translations.ContainsKey(targetLanguage));

        return Task.FromResult(entry);
    }

    /// <inheritdoc />
    public async Task<bool> IsAvailableAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            // Test embedding generation
            var testEmbedding = await _embeddingGenerator.GenerateEmbeddingAsync("test", cancellationToken);
            return testEmbedding.Length > 0;
        }
        catch
        {
            return false;
        }
    }

    /// <inheritdoc />
    public Task ClearSessionMemoryAsync(CancellationToken cancellationToken = default)
    {
        _sessionMemory.Clear();
        return Task.CompletedTask;
    }

    private async Task LoadGlossaryFileAsync(string filePath, CancellationToken cancellationToken)
    {
        try
        {
            var json = await File.ReadAllTextAsync(filePath, cancellationToken);
            var glossary = JsonSerializer.Deserialize<Glossary>(json, new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true
            });

            if (glossary?.Entries != null)
                foreach (var entry in glossary.Entries)
                    _glossaryEntries.TryAdd(entry.Id, entry);

            _logger.LogDebug("Loaded glossary file: {Path} with {Count} entries",
                filePath, glossary?.Entries?.Count ?? 0);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to load glossary file: {Path}", filePath);
        }
    }

    private static float CalculateCosineSimilarity(float[] a, float[] b)
    {
        if (a.Length != b.Length)
            return 0;

        float dotProduct = 0;
        float normA = 0;
        float normB = 0;

        for (var i = 0; i < a.Length; i++)
        {
            dotProduct += a[i] * b[i];
            normA += a[i] * a[i];
            normB += b[i] * b[i];
        }

        var denominator = MathF.Sqrt(normA) * MathF.Sqrt(normB);
        return denominator == 0 ? 0 : dotProduct / denominator;
    }
}
