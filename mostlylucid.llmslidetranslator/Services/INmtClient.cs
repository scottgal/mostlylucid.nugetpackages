using mostlylucid.llmslidetranslator.Models;

namespace mostlylucid.llmslidetranslator.Services;

/// <summary>
///     Client for NMT (Neural Machine Translation) service
///     Based on https://github.com/scottgal/mostlyucid-nmt (Opus-MT)
/// </summary>
public interface INmtClient
{
    /// <summary>
    ///     Translate text using NMT
    /// </summary>
    /// <param name="text">Text to translate</param>
    /// <param name="sourceLanguage">Source language code</param>
    /// <param name="targetLanguage">Target language code</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Translated text</returns>
    Task<string> TranslateAsync(
        string text,
        string sourceLanguage,
        string targetLanguage,
        CancellationToken cancellationToken = default);

    /// <summary>
    ///     Translate multiple blocks using NMT
    /// </summary>
    /// <param name="blocks">Blocks to translate</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Blocks with translations</returns>
    Task<List<TranslationBlock>> TranslateBatchAsync(
        List<TranslationBlock> blocks,
        CancellationToken cancellationToken = default);

    /// <summary>
    ///     Check if NMT service is available
    /// </summary>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>True if available</returns>
    Task<bool> IsAvailableAsync(CancellationToken cancellationToken = default);
}