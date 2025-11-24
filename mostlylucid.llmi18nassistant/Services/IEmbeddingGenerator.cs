namespace Mostlylucid.LlmI18nAssistant.Services;

/// <summary>
///     Interface for generating text embeddings
/// </summary>
public interface IEmbeddingGenerator
{
    /// <summary>
    ///     Generates an embedding for the given text
    /// </summary>
    /// <param name="text">Text to embed</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Embedding vector</returns>
    Task<float[]> GenerateEmbeddingAsync(string text, CancellationToken cancellationToken = default);

    /// <summary>
    ///     Generates embeddings for multiple texts
    /// </summary>
    /// <param name="texts">Texts to embed</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>List of embedding vectors</returns>
    Task<List<float[]>> GenerateBatchEmbeddingsAsync(
        List<string> texts,
        CancellationToken cancellationToken = default);

    /// <summary>
    ///     Calculates similarity between two embedding vectors
    /// </summary>
    float CalculateSimilarity(float[] embedding1, float[] embedding2);
}