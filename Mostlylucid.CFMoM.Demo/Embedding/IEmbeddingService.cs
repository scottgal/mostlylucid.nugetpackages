namespace Mostlylucid.CFMoM.Demo.Embedding;

/// <summary>
///     Interface for embedding service.
/// </summary>
public interface IEmbeddingService
{
    /// <summary>
    ///     Dimensions of the embedding vectors.
    /// </summary>
    int Dimensions { get; }

    /// <summary>
    ///     Generate embedding for a text.
    /// </summary>
    float[] Embed(string text);

    /// <summary>
    ///     Generate embeddings for multiple texts.
    /// </summary>
    float[][] EmbedBatch(IEnumerable<string> texts);

    /// <summary>
    ///     Calculate cosine similarity between two embeddings.
    /// </summary>
    float CosineSimilarity(float[] a, float[] b);
}
