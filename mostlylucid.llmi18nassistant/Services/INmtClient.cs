namespace Mostlylucid.LlmI18nAssistant.Services;

/// <summary>
///     Client for Neural Machine Translation service
/// </summary>
public interface INmtClient
{
    /// <summary>
    ///     Translates text using NMT
    /// </summary>
    /// <param name="text">Text to translate</param>
    /// <param name="sourceLanguage">Source language</param>
    /// <param name="targetLanguage">Target language</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Translated text</returns>
    Task<string?> TranslateAsync(
        string text,
        string sourceLanguage,
        string targetLanguage,
        CancellationToken cancellationToken = default);

    /// <summary>
    ///     Translates multiple texts in batch
    /// </summary>
    /// <param name="texts">Texts to translate</param>
    /// <param name="sourceLanguage">Source language</param>
    /// <param name="targetLanguage">Target language</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Translated texts</returns>
    Task<List<string>> TranslateBatchAsync(
        List<string> texts,
        string sourceLanguage,
        string targetLanguage,
        CancellationToken cancellationToken = default);

    /// <summary>
    ///     Checks if NMT service is available
    /// </summary>
    Task<bool> IsAvailableAsync(CancellationToken cancellationToken = default);
}
