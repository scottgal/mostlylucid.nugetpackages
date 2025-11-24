using Mostlylucid.RagLlmSearch.Models;

namespace Mostlylucid.RagLlmSearch.LlmServices;

/// <summary>
///     Interface for LLM service operations
/// </summary>
public interface ILlmService
{
    /// <summary>
    ///     Generates a chat response based on conversation history and context
    /// </summary>
    /// <param name="messages">Conversation history</param>
    /// <param name="context">Optional RAG/search context to include</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>The assistant's response</returns>
    Task<string> GenerateResponseAsync(
        IEnumerable<ChatMessage> messages,
        string? context = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    ///     Generates a streaming chat response
    /// </summary>
    /// <param name="messages">Conversation history</param>
    /// <param name="context">Optional RAG/search context to include</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Async enumerable of response chunks</returns>
    IAsyncEnumerable<string> GenerateStreamingResponseAsync(
        IEnumerable<ChatMessage> messages,
        string? context = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    ///     Generates embeddings for text
    /// </summary>
    /// <param name="text">Text to embed</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Embedding vector</returns>
    Task<float[]> GenerateEmbeddingAsync(string text, CancellationToken cancellationToken = default);

    /// <summary>
    ///     Checks if the LLM service is available
    /// </summary>
    Task<bool> IsAvailableAsync(CancellationToken cancellationToken = default);

    /// <summary>
    ///     Determines if a query needs web search based on its content
    /// </summary>
    /// <param name="query">The user's query</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>True if web search would be helpful</returns>
    Task<bool> ShouldSearchWebAsync(string query, CancellationToken cancellationToken = default);
}