using Mostlylucid.LlmI18nAssistant.Models;

namespace Mostlylucid.LlmI18nAssistant.Services;

/// <summary>
///     Service for maintaining translation consistency using RAG over glossary and translation memory
/// </summary>
public interface IConsistencyModeService
{
    /// <summary>
    ///     Initializes the consistency service for a specific file translation
    /// </summary>
    /// <param name="resourceFile">The resource file being translated</param>
    /// <param name="targetLanguage">Target language</param>
    /// <param name="cancellationToken">Cancellation token</param>
    Task InitializeForFileAsync(ResourceFile resourceFile, string targetLanguage,
        CancellationToken cancellationToken = default);

    /// <summary>
    ///     Loads a glossary file
    /// </summary>
    /// <param name="glossaryPath">Path to the glossary file or directory</param>
    /// <param name="cancellationToken">Cancellation token</param>
    Task LoadGlossaryAsync(string glossaryPath, CancellationToken cancellationToken = default);

    /// <summary>
    ///     Finds similar entries from glossary and translation memory
    /// </summary>
    /// <param name="sourceText">Source text to find similar entries for</param>
    /// <param name="sourceLanguage">Source language</param>
    /// <param name="targetLanguage">Target language</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>List of similar context entries</returns>
    Task<List<ContextEntry>> FindSimilarEntriesAsync(
        string sourceText,
        string sourceLanguage,
        string targetLanguage,
        CancellationToken cancellationToken = default);

    /// <summary>
    ///     Stores a translation in the translation memory
    /// </summary>
    /// <param name="sourceText">Source text</param>
    /// <param name="translatedText">Translated text</param>
    /// <param name="sourceLanguage">Source language</param>
    /// <param name="targetLanguage">Target language</param>
    /// <param name="cancellationToken">Cancellation token</param>
    Task StoreTranslationAsync(
        string sourceText,
        string translatedText,
        string sourceLanguage,
        string targetLanguage,
        CancellationToken cancellationToken = default);

    /// <summary>
    ///     Gets a specific glossary entry
    /// </summary>
    /// <param name="term">The term to look up</param>
    /// <param name="targetLanguage">Target language</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Glossary entry if found</returns>
    Task<GlossaryEntry?> GetGlossaryEntryAsync(
        string term,
        string targetLanguage,
        CancellationToken cancellationToken = default);

    /// <summary>
    ///     Checks if the service is available
    /// </summary>
    Task<bool> IsAvailableAsync(CancellationToken cancellationToken = default);

    /// <summary>
    ///     Clears the in-session translation memory
    /// </summary>
    Task ClearSessionMemoryAsync(CancellationToken cancellationToken = default);
}