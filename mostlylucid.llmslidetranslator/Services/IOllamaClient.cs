using mostlylucid.llmslidetranslator.Models;

namespace mostlylucid.llmslidetranslator.Services;

/// <summary>
///     Client for Ollama LLM API
/// </summary>
public interface IOllamaClient
{
    /// <summary>
    ///     Translate a block using the LLM with context
    /// </summary>
    /// <param name="context">Translation context with current block and RAG context</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Translated text</returns>
    Task<string> TranslateWithContextAsync(
        TranslationContext context,
        CancellationToken cancellationToken = default);

    /// <summary>
    ///     Check if Ollama service is available
    /// </summary>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>True if available</returns>
    Task<bool> IsAvailableAsync(CancellationToken cancellationToken = default);

    /// <summary>
    ///     Get list of available models
    /// </summary>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>List of model names</returns>
    Task<List<string>> GetModelsAsync(CancellationToken cancellationToken = default);
}