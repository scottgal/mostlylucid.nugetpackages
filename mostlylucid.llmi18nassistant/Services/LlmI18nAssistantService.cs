using System.Diagnostics;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Mostlylucid.LlmI18nAssistant.Models;
using Mostlylucid.LlmI18nAssistant.Telemetry;

namespace Mostlylucid.LlmI18nAssistant.Services;

/// <summary>
///     Main service for LLM-assisted i18n translation
/// </summary>
public class LlmI18nAssistantService : ILlmI18nAssistant
{
    private readonly LlmI18nAssistantConfig _config;
    private readonly IConsistencyModeService _consistencyService;
    private readonly ILogger<LlmI18nAssistantService> _logger;
    private readonly INmtClient _nmtClient;
    private readonly IOllamaClient _ollamaClient;
    private readonly IResourceFileParser _parser;
    private readonly IValueTransformer _valueTransformer;

    public LlmI18nAssistantService(
        ILogger<LlmI18nAssistantService> logger,
        IOptions<LlmI18nAssistantConfig> config,
        IResourceFileParser parser,
        IOllamaClient ollamaClient,
        INmtClient nmtClient,
        IConsistencyModeService consistencyService,
        IValueTransformer valueTransformer)
    {
        _logger = logger;
        _config = config.Value;
        _parser = parser;
        _ollamaClient = ollamaClient;
        _nmtClient = nmtClient;
        _consistencyService = consistencyService;
        _valueTransformer = valueTransformer;
    }

    /// <inheritdoc />
    public async Task<TranslationResult> TranslateResourceFileAsync(
        string filePath,
        string sourceLanguage,
        string targetLanguage,
        TranslationOptions? options = null,
        CancellationToken cancellationToken = default)
    {
        using var activity = I18nAssistantTelemetry.StartTranslateResourceFileActivity(
            filePath, sourceLanguage, targetLanguage);

        options ??= new TranslationOptions();
        var stopwatch = Stopwatch.StartNew();

        _logger.LogInformation("Starting translation of {FilePath} from {Source} to {Target}",
            filePath, sourceLanguage, targetLanguage);

        try
        {
            // Parse the resource file
            var resourceFile = await _parser.ParseAsync(filePath, cancellationToken);
            resourceFile.SourceLanguage = sourceLanguage;

            // Translate
            var result = await TranslateResourceFileInternalAsync(
                resourceFile, sourceLanguage, targetLanguage, options, cancellationToken);

            result.Duration = stopwatch.Elapsed;
            I18nAssistantTelemetry.RecordResult(activity, result);
            return result;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to translate resource file {FilePath}", filePath);
            I18nAssistantTelemetry.RecordException(activity, ex);
            return new TranslationResult
            {
                SourceFile = new ResourceFile { FilePath = filePath },
                SourceLanguage = sourceLanguage,
                TargetLanguage = targetLanguage,
                Success = false,
                Errors = [new TranslationError { Message = ex.Message, Exception = ex.ToString() }],
                Duration = stopwatch.Elapsed
            };
        }
    }

    /// <inheritdoc />
    public async Task<TranslationResult> TranslateResourceStreamAsync(
        Stream stream,
        ResourceFileType fileType,
        string sourceLanguage,
        string targetLanguage,
        TranslationOptions? options = null,
        CancellationToken cancellationToken = default)
    {
        using var activity = I18nAssistantTelemetry.StartTranslateStreamActivity(
            fileType, sourceLanguage, targetLanguage);

        options ??= new TranslationOptions();
        var stopwatch = Stopwatch.StartNew();

        try
        {
            var resourceFile = await _parser.ParseAsync(stream, fileType, null, cancellationToken);
            resourceFile.SourceLanguage = sourceLanguage;

            var result = await TranslateResourceFileInternalAsync(
                resourceFile, sourceLanguage, targetLanguage, options, cancellationToken);

            result.Duration = stopwatch.Elapsed;
            I18nAssistantTelemetry.RecordResult(activity, result);
            return result;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to translate resource stream");
            I18nAssistantTelemetry.RecordException(activity, ex);
            return new TranslationResult
            {
                SourceFile = new ResourceFile { FileType = fileType },
                SourceLanguage = sourceLanguage,
                TargetLanguage = targetLanguage,
                Success = false,
                Errors = [new TranslationError { Message = ex.Message, Exception = ex.ToString() }],
                Duration = stopwatch.Elapsed
            };
        }
    }

    /// <inheritdoc />
    public async Task<MultiLanguageTranslationResult> TranslateToMultipleLanguagesAsync(
        string filePath,
        string sourceLanguage,
        IEnumerable<string> targetLanguages,
        TranslationOptions? options = null,
        CancellationToken cancellationToken = default)
    {
        var languages = targetLanguages.ToList();
        using var activity = I18nAssistantTelemetry.StartTranslateMultipleLanguagesActivity(
            filePath, sourceLanguage, languages.Count);

        options ??= new TranslationOptions();
        var stopwatch = Stopwatch.StartNew();

        try
        {
            var resourceFile = await _parser.ParseAsync(filePath, cancellationToken);
            resourceFile.SourceLanguage = sourceLanguage;

            var result = new MultiLanguageTranslationResult
            {
                SourceFile = resourceFile,
                SourceLanguage = sourceLanguage
            };

            _logger.LogInformation("Translating {FilePath} to {Count} languages: {Languages}",
                filePath, languages.Count, string.Join(", ", languages));

            // Translate to each language
            foreach (var targetLanguage in languages)
            {
                cancellationToken.ThrowIfCancellationRequested();

                var translation = await TranslateResourceFileInternalAsync(
                    resourceFile, sourceLanguage, targetLanguage, options, cancellationToken);

                result.Results[targetLanguage] = translation;
            }

            result.TotalDuration = stopwatch.Elapsed;
            I18nAssistantTelemetry.RecordMultiLanguageResult(activity, result);
            return result;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to translate resource file to multiple languages {FilePath}", filePath);
            I18nAssistantTelemetry.RecordException(activity, ex);
            throw;
        }
    }

    /// <inheritdoc />
    public async Task<string> TranslateStringAsync(
        string text,
        string sourceLanguage,
        string targetLanguage,
        string? context = null,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(text))
            return text;

        using var activity = I18nAssistantTelemetry.StartTranslateStringActivity(
            sourceLanguage, targetLanguage, text.Length);

        _logger.LogDebug("Translating single string from {Source} to {Target}: {Text}",
            sourceLanguage, targetLanguage, text.Length > 50 ? text[..50] + "..." : text);

        try
        {
            // Get consistency context if enabled
            List<ContextEntry>? consistencyContext = null;
            if (_config.ConsistencyMode.Enabled)
                consistencyContext = await _consistencyService.FindSimilarEntriesAsync(
                    text, sourceLanguage, targetLanguage, cancellationToken);

            // Try NMT first if enabled
            string? nmtResult = null;
            if (_config.Nmt.Enabled && _config.Nmt.UseAsBaseline)
                nmtResult = await _nmtClient.TranslateAsync(text, sourceLanguage, targetLanguage, cancellationToken);

            // Use LLM for final translation
            var translation = await _ollamaClient.TranslateAsync(
                text,
                sourceLanguage,
                targetLanguage,
                nmtResult,
                consistencyContext,
                context,
                cancellationToken);

            I18nAssistantTelemetry.RecordStringResult(activity, translation.Length);
            return translation;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to translate string");
            I18nAssistantTelemetry.RecordException(activity, ex);
            throw;
        }
    }

    /// <inheritdoc />
    public async Task<Dictionary<string, string>> TranslateBatchAsync(
        Dictionary<string, string> entries,
        string sourceLanguage,
        string targetLanguage,
        TranslationOptions? options = null,
        CancellationToken cancellationToken = default)
    {
        using var activity = I18nAssistantTelemetry.StartTranslateBatchActivity(
            sourceLanguage, targetLanguage, entries.Count);

        options ??= new TranslationOptions();

        try
        {
            var result = new Dictionary<string, string>();
            var total = entries.Count;
            var current = 0;

            foreach (var (key, value) in entries)
            {
                cancellationToken.ThrowIfCancellationRequested();

                current++;
                options.OnProgress?.Invoke(new TranslationProgress
                {
                    CurrentIndex = current,
                    TotalCount = total,
                    CurrentKey = key,
                    CurrentValue = value,
                    TargetLanguage = targetLanguage,
                    Status = $"Translating entry {current}/{total}"
                });

                try
                {
                    var translated = await TranslateStringAsync(value, sourceLanguage, targetLanguage,
                        $"Resource key: {key}", cancellationToken);
                    result[key] = translated;
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Failed to translate key {Key}, using original value", key);
                    result[key] = value;
                }
            }

            I18nAssistantTelemetry.RecordBatchResult(activity, result.Count, total);
            return result;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to translate batch");
            I18nAssistantTelemetry.RecordException(activity, ex);
            throw;
        }
    }

    /// <inheritdoc />
    public async Task<ServiceStatus> CheckServicesAsync(CancellationToken cancellationToken = default)
    {
        var status = new ServiceStatus();

        try
        {
            status.OllamaAvailable = await _ollamaClient.IsAvailableAsync(cancellationToken);
            if (status.OllamaAvailable)
                status.OllamaModels = await _ollamaClient.GetModelsAsync(cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to check Ollama availability");
        }

        try
        {
            status.NmtAvailable = await _nmtClient.IsAvailableAsync(cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to check NMT availability");
        }

        try
        {
            status.EmbeddingAvailable = await _consistencyService.IsAvailableAsync(cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to check embedding availability");
        }

        status.Message = status.AllServicesAvailable
            ? "All services available"
            : "Some services are unavailable";

        return status;
    }

    private async Task<TranslationResult> TranslateResourceFileInternalAsync(
        ResourceFile resourceFile,
        string sourceLanguage,
        string targetLanguage,
        TranslationOptions options,
        CancellationToken cancellationToken)
    {
        var result = new TranslationResult
        {
            SourceFile = resourceFile,
            SourceLanguage = sourceLanguage,
            TargetLanguage = targetLanguage,
            Statistics = new TranslationStatistics
            {
                TotalEntries = resourceFile.Entries.Count
            }
        };

        // Filter entries based on options
        var entriesToTranslate = resourceFile.Entries
            .Where(e => e.ShouldTranslate)
            .Where(e => options.OnlyKeys.Count == 0 || options.OnlyKeys.Contains(e.Key))
            .Where(e => !options.SkipKeys.Contains(e.Key))
            .ToList();

        _logger.LogInformation("Translating {Count} entries (of {Total} total) to {Target}",
            entriesToTranslate.Count, resourceFile.Entries.Count, targetLanguage);

        // Initialize consistency mode if enabled
        if (options.UseConsistencyMode && _config.ConsistencyMode.Enabled)
            await _consistencyService.InitializeForFileAsync(resourceFile, targetLanguage, cancellationToken);

        var translatedEntries = new List<ResourceEntry>();
        var current = 0;
        var total = entriesToTranslate.Count;

        foreach (var entry in entriesToTranslate)
        {
            cancellationToken.ThrowIfCancellationRequested();

            current++;
            options.OnProgress?.Invoke(new TranslationProgress
            {
                CurrentIndex = current,
                TotalCount = total,
                CurrentKey = entry.Key,
                CurrentValue = entry.Value,
                TargetLanguage = targetLanguage,
                Status = $"Translating {current}/{total}: {entry.Key}"
            });

            try
            {
                var translatedEntry = await TranslateEntryAsync(
                    entry, sourceLanguage, targetLanguage, options, cancellationToken);

                translatedEntries.Add(translatedEntry);

                // Update statistics
                if (translatedEntry.TranslationMethod.HasValue)
                {
                    result.Statistics.TranslatedCount++;
                    result.Statistics.TotalCharactersTranslated += entry.Value.Length;

                    switch (translatedEntry.TranslationMethod.Value)
                    {
                        case TranslationMethod.LlmOnly:
                            result.Statistics.LlmOnlyCount++;
                            break;
                        case TranslationMethod.NmtOnly:
                            result.Statistics.NmtOnlyCount++;
                            break;
                        case TranslationMethod.NmtPlusLlm:
                            result.Statistics.NmtPlusLlmCount++;
                            break;
                        case TranslationMethod.RagLlm:
                            result.Statistics.RagLlmCount++;
                            break;
                    }

                    if (translatedEntry.ContextUsed?.Count > 0)
                    {
                        result.Statistics.ContextEntriesUsed += translatedEntry.ContextUsed.Count;
                        result.Statistics.GlossaryTermsUsed +=
                            translatedEntry.ContextUsed.Count(c => c.Source == ContextSource.Glossary);
                    }
                }

                // Store for consistency mode
                if (options.UseConsistencyMode && translatedEntry.TranslatedValue != null)
                    await _consistencyService.StoreTranslationAsync(
                        entry.Value, translatedEntry.TranslatedValue,
                        sourceLanguage, targetLanguage, cancellationToken);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to translate entry {Key}", entry.Key);
                result.Errors.Add(new TranslationError
                {
                    Key = entry.Key,
                    Message = ex.Message,
                    Exception = ex.ToString()
                });
                result.Statistics.FailedCount++;

                // Add original value
                translatedEntries.Add(entry);
            }
        }

        // Add skipped entries (with original values)
        foreach (var entry in resourceFile.Entries.Where(e => !e.ShouldTranslate))
        {
            translatedEntries.Add(new ResourceEntry
            {
                Key = entry.Key,
                Value = entry.Value,
                Comment = entry.Comment,
                TranslatedValue = entry.Value,
                ShouldTranslate = false,
                SkipReason = entry.SkipReason,
                TranslationMethod = TranslationMethod.Skipped
            });
            result.Statistics.SkippedCount++;
        }

        result.Entries = translatedEntries.OrderBy(e =>
            resourceFile.Entries.FindIndex(r => r.Key == e.Key)).ToList();

        result.Statistics.AverageCharactersPerEntry = result.Statistics.TranslatedCount > 0
            ? (double)result.Statistics.TotalCharactersTranslated / result.Statistics.TranslatedCount
            : 0;

        result.Success = result.Statistics.FailedCount == 0;

        _logger.LogInformation(
            "Translation complete: {Translated}/{Total} entries, {Failed} failed, {Skipped} skipped",
            result.Statistics.TranslatedCount, result.Statistics.TotalEntries,
            result.Statistics.FailedCount, result.Statistics.SkippedCount);

        return result;
    }

    private async Task<ResourceEntry> TranslateEntryAsync(
        ResourceEntry entry,
        string sourceLanguage,
        string targetLanguage,
        TranslationOptions options,
        CancellationToken cancellationToken)
    {
        // Extract and protect special content
        var (processedValue, protectedTokens) = _valueTransformer.ExtractProtectedContent(entry.Value, options);

        // Get consistency context
        List<ContextEntry>? contextEntries = null;
        if (options.UseConsistencyMode && _config.ConsistencyMode.Enabled)
            contextEntries = await _consistencyService.FindSimilarEntriesAsync(
                processedValue, sourceLanguage, targetLanguage, cancellationToken);

        // Determine translation method
        var method = options.Method;
        string? nmtBaseline = null;

        // Get NMT baseline if configured
        if (_config.Nmt.Enabled && _config.Nmt.UseAsBaseline &&
            method is TranslationMethod.NmtPlusLlm or TranslationMethod.RagLlm)
            nmtBaseline = await _nmtClient.TranslateAsync(
                processedValue, sourceLanguage, targetLanguage, cancellationToken);

        // Translate with LLM
        var translated = await _ollamaClient.TranslateAsync(
            processedValue,
            sourceLanguage,
            targetLanguage,
            nmtBaseline,
            contextEntries,
            options.AdditionalContext,
            cancellationToken);

        // Restore protected content
        translated = _valueTransformer.RestoreProtectedContent(translated, protectedTokens);

        // Determine actual method used
        var actualMethod = contextEntries?.Count > 0
            ? TranslationMethod.RagLlm
            : nmtBaseline != null
                ? TranslationMethod.NmtPlusLlm
                : TranslationMethod.LlmOnly;

        var translatedEntry = entry.WithTranslation(translated, actualMethod);
        translatedEntry.ContextUsed = contextEntries;
        return translatedEntry;
    }
}