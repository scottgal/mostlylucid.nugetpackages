using Mostlylucid.LlmI18nAssistant.Models;

namespace Mostlylucid.LlmI18nAssistant.Services;

/// <summary>
///     Main service for LLM-assisted i18n translation
/// </summary>
public interface ILlmI18nAssistant
{
    /// <summary>
    ///     Translates a resource file from a file path
    /// </summary>
    /// <param name="filePath">Path to the resource file (.resx or .json)</param>
    /// <param name="sourceLanguage">Source language code</param>
    /// <param name="targetLanguage">Target language code</param>
    /// <param name="options">Translation options</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Translation result</returns>
    Task<TranslationResult> TranslateResourceFileAsync(
        string filePath,
        string sourceLanguage,
        string targetLanguage,
        TranslationOptions? options = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    ///     Translates a resource file from a stream
    /// </summary>
    /// <param name="stream">Stream containing the resource file</param>
    /// <param name="fileType">Type of resource file</param>
    /// <param name="sourceLanguage">Source language code</param>
    /// <param name="targetLanguage">Target language code</param>
    /// <param name="options">Translation options</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Translation result</returns>
    Task<TranslationResult> TranslateResourceStreamAsync(
        Stream stream,
        ResourceFileType fileType,
        string sourceLanguage,
        string targetLanguage,
        TranslationOptions? options = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    ///     Translates a resource file to multiple languages
    /// </summary>
    /// <param name="filePath">Path to the resource file</param>
    /// <param name="sourceLanguage">Source language code</param>
    /// <param name="targetLanguages">Target language codes</param>
    /// <param name="options">Translation options</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Multi-language translation result</returns>
    Task<MultiLanguageTranslationResult> TranslateToMultipleLanguagesAsync(
        string filePath,
        string sourceLanguage,
        IEnumerable<string> targetLanguages,
        TranslationOptions? options = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    ///     Translates a single string
    /// </summary>
    /// <param name="text">Text to translate</param>
    /// <param name="sourceLanguage">Source language code</param>
    /// <param name="targetLanguage">Target language code</param>
    /// <param name="context">Optional context for the translation</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Translated text</returns>
    Task<string> TranslateStringAsync(
        string text,
        string sourceLanguage,
        string targetLanguage,
        string? context = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    ///     Translates a batch of strings
    /// </summary>
    /// <param name="entries">Key-value pairs to translate</param>
    /// <param name="sourceLanguage">Source language code</param>
    /// <param name="targetLanguage">Target language code</param>
    /// <param name="options">Translation options</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Translated key-value pairs</returns>
    Task<Dictionary<string, string>> TranslateBatchAsync(
        Dictionary<string, string> entries,
        string sourceLanguage,
        string targetLanguage,
        TranslationOptions? options = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    ///     Checks if the translation services are available
    /// </summary>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Service availability status</returns>
    Task<ServiceStatus> CheckServicesAsync(CancellationToken cancellationToken = default);
}

/// <summary>
///     Status of translation services
/// </summary>
public class ServiceStatus
{
    /// <summary>
    ///     Whether Ollama is available
    /// </summary>
    public bool OllamaAvailable { get; set; }

    /// <summary>
    ///     Available Ollama models
    /// </summary>
    public List<string> OllamaModels { get; set; } = [];

    /// <summary>
    ///     Whether NMT service is available
    /// </summary>
    public bool NmtAvailable { get; set; }

    /// <summary>
    ///     Whether embedding service is available
    /// </summary>
    public bool EmbeddingAvailable { get; set; }

    /// <summary>
    ///     Whether all required services are available
    /// </summary>
    public bool AllServicesAvailable => OllamaAvailable;

    /// <summary>
    ///     Status message
    /// </summary>
    public string? Message { get; set; }
}
