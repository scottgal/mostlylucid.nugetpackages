using mostlylucid.llmslidetranslator.Models;

namespace mostlylucid.llmslidetranslator.Services;

/// <summary>
///     Generates embeddings for text blocks
/// </summary>
public interface IEmbeddingGenerator
{
    /// <summary>
    ///     Generate embedding for a single block
    /// </summary>
    /// <param name="text">Text to embed</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Embedding vector</returns>
    Task<float[]> GenerateEmbeddingAsync(string text, CancellationToken cancellationToken = default);

    /// <summary>
    ///     Generate embeddings for multiple blocks
    /// </summary>
    /// <param name="blocks">Blocks to embed</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Blocks with embeddings populated</returns>
    Task<List<TranslationBlock>> GenerateEmbeddingsAsync(
        List<TranslationBlock> blocks,
        CancellationToken cancellationToken = default);

    /// <summary>
    ///     Calculate cosine similarity between two embeddings
    /// </summary>
    /// <param name="embedding1">First embedding</param>
    /// <param name="embedding2">Second embedding</param>
    /// <returns>Similarity score (0.0 - 1.0)</returns>
    float CalculateSimilarity(float[] embedding1, float[] embedding2);
}