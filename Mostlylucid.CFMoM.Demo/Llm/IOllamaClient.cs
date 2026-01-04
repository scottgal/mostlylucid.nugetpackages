namespace Mostlylucid.CFMoM.Demo.Llm;

/// <summary>
///     Interface for Ollama LLM client.
/// </summary>
public interface IOllamaClient
{
    /// <summary>
    ///     Generate a response from the LLM.
    /// </summary>
    /// <param name="prompt">The prompt to send.</param>
    /// <param name="model">The model to use (e.g., "llama3.2:3b").</param>
    /// <param name="systemPrompt">Optional system prompt.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The generated response text.</returns>
    Task<string> GenerateAsync(
        string prompt,
        string model = "llama3.2:3b",
        string? systemPrompt = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    ///     Generate a response and parse as JSON.
    /// </summary>
    /// <typeparam name="T">The type to deserialize to.</typeparam>
    Task<T?> GenerateJsonAsync<T>(
        string prompt,
        string model = "llama3.2:3b",
        string? systemPrompt = null,
        CancellationToken cancellationToken = default) where T : class;

    /// <summary>
    ///     Check if Ollama is available.
    /// </summary>
    Task<bool> IsAvailableAsync(CancellationToken cancellationToken = default);
}
