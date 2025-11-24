using Mostlylucid.LlmI18nAssistant.Models;

namespace Mostlylucid.LlmI18nAssistant.Services;

/// <summary>
///     Client for interacting with Ollama LLM
/// </summary>
public interface IOllamaClient
{
    /// <summary>
    ///     Translates text using the LLM
    /// </summary>
    /// <param name="sourceText">Text to translate</param>
    /// <param name="sourceLanguage">Source language</param>
    /// <param name="targetLanguage">Target language</param>
    /// <param name="nmtBaseline">Optional NMT baseline translation for post-editing</param>
    /// <param name="contextEntries">Context entries for consistency</param>
    /// <param name="additionalContext">Additional context about the text</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Translated text</returns>
    Task<string> TranslateAsync(
        string sourceText,
        string sourceLanguage,
        string targetLanguage,
        string? nmtBaseline = null,
        List<ContextEntry>? contextEntries = null,
        string? additionalContext = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    ///     Checks if Ollama is available
    /// </summary>
    Task<bool> IsAvailableAsync(CancellationToken cancellationToken = default);

    /// <summary>
    ///     Gets available models
    /// </summary>
    Task<List<string>> GetModelsAsync(CancellationToken cancellationToken = default);
}