using mostlylucid.llmslidetranslator.Models;

namespace mostlylucid.llmslidetranslator.Services;

/// <summary>
///     File-based vector store for embeddings
/// </summary>
public interface IVectorStore
{
    /// <summary>
    ///     Store translation blocks with embeddings
    /// </summary>
    /// <param name="blocks">Blocks to store</param>
    /// <param name="documentId">Document identifier</param>
    /// <param name="cancellationToken">Cancellation token</param>
    Task StoreAsync(List<TranslationBlock> blocks, string documentId, CancellationToken cancellationToken = default);

    /// <summary>
    ///     Search for similar blocks using vector similarity
    /// </summary>
    /// <param name="queryEmbedding">Query embedding vector</param>
    /// <param name="documentId">Document identifier to search within</param>
    /// <param name="topK">Number of results to return</param>
    /// <param name="minSimilarity">Minimum similarity threshold</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>List of similar blocks with similarity scores</returns>
    Task<List<(TranslationBlock Block, float Similarity)>> SearchAsync(
        float[] queryEmbedding,
        string documentId,
        int topK,
        float minSimilarity = 0.0f,
        CancellationToken cancellationToken = default);

    /// <summary>
    ///     Get all blocks for a document
    /// </summary>
    /// <param name="documentId">Document identifier</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>List of blocks</returns>
    Task<List<TranslationBlock>> GetDocumentBlocksAsync(
        string documentId,
        CancellationToken cancellationToken = default);

    /// <summary>
    ///     Clear all data for a document
    /// </summary>
    /// <param name="documentId">Document identifier</param>
    /// <param name="cancellationToken">Cancellation token</param>
    Task ClearDocumentAsync(string documentId, CancellationToken cancellationToken = default);
}