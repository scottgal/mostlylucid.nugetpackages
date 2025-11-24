using System.Text.Json;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using mostlylucid.llmslidetranslator.Models;

namespace mostlylucid.llmslidetranslator.Services;

/// <summary>
///     File-based vector store for embeddings
/// </summary>
public class FileVectorStore(
    ILogger<FileVectorStore> logger,
    IEmbeddingGenerator embeddingGenerator,
    IOptions<LlmSlideTranslatorConfig> config) : IVectorStore
{
    private readonly string dataPath = InitializeDataPath(config.Value.DataPath);
    private readonly SemaphoreSlim fileLock = new(1, 1);

    public async Task StoreAsync(
        List<TranslationBlock> blocks,
        string documentId,
        CancellationToken cancellationToken = default)
    {
        logger.LogInformation("Storing {Count} blocks for document {DocumentId}",
            blocks.Count, documentId);

        var filePath = GetDocumentPath(documentId);

        await fileLock.WaitAsync(cancellationToken);
        try
        {
            var json = JsonSerializer.Serialize(blocks, new JsonSerializerOptions
            {
                WriteIndented = true
            });

            await File.WriteAllTextAsync(filePath, json, cancellationToken);

            logger.LogInformation("Stored blocks to {FilePath}", filePath);
        }
        finally
        {
            fileLock.Release();
        }
    }

    public async Task<List<(TranslationBlock Block, float Similarity)>> SearchAsync(
        float[] queryEmbedding,
        string documentId,
        int topK,
        float minSimilarity = 0.0f,
        CancellationToken cancellationToken = default)
    {
        logger.LogDebug("Searching for top {TopK} similar blocks in document {DocumentId}",
            topK, documentId);

        var blocks = await GetDocumentBlocksAsync(documentId, cancellationToken);

        var results = blocks
            .Where(b => b.Embedding != null)
            .Select(b => new
            {
                Block = b,
                Similarity = embeddingGenerator.CalculateSimilarity(queryEmbedding, b.Embedding!)
            })
            .Where(r => r.Similarity >= minSimilarity)
            .OrderByDescending(r => r.Similarity)
            .Take(topK)
            .Select(r => (r.Block, r.Similarity))
            .ToList();

        logger.LogDebug("Found {Count} similar blocks above threshold {MinSimilarity}",
            results.Count, minSimilarity);

        return results;
    }

    public async Task<List<TranslationBlock>> GetDocumentBlocksAsync(
        string documentId,
        CancellationToken cancellationToken = default)
    {
        var filePath = GetDocumentPath(documentId);

        if (!File.Exists(filePath))
        {
            logger.LogWarning("Document {DocumentId} not found at {FilePath}",
                documentId, filePath);
            return new List<TranslationBlock>();
        }

        await fileLock.WaitAsync(cancellationToken);
        try
        {
            var json = await File.ReadAllTextAsync(filePath, cancellationToken);
            var blocks = JsonSerializer.Deserialize<List<TranslationBlock>>(json);

            return blocks ?? new List<TranslationBlock>();
        }
        finally
        {
            fileLock.Release();
        }
    }

    public async Task ClearDocumentAsync(string documentId, CancellationToken cancellationToken = default)
    {
        var filePath = GetDocumentPath(documentId);

        if (File.Exists(filePath))
        {
            await fileLock.WaitAsync(cancellationToken);
            try
            {
                File.Delete(filePath);
                logger.LogInformation("Cleared document {DocumentId}", documentId);
            }
            finally
            {
                fileLock.Release();
            }
        }
    }

    private static string InitializeDataPath(string path)
    {
        // Ensure data directory exists
        Directory.CreateDirectory(path);
        return path;
    }

    private string GetDocumentPath(string documentId)
    {
        // Sanitize document ID for use as filename
        var safeDocId = string.Join("_", documentId.Split(Path.GetInvalidFileNameChars()));
        return Path.Combine(dataPath, $"{safeDocId}.json");
    }
}