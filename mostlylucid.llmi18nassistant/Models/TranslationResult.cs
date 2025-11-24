namespace Mostlylucid.LlmI18nAssistant.Models;

/// <summary>
///     Result of a resource file translation
/// </summary>
public class TranslationResult
{
    /// <summary>
    ///     The original resource file
    /// </summary>
    public required ResourceFile SourceFile { get; set; }

    /// <summary>
    ///     Source language
    /// </summary>
    public required string SourceLanguage { get; set; }

    /// <summary>
    ///     Target language
    /// </summary>
    public required string TargetLanguage { get; set; }

    /// <summary>
    ///     Translated entries
    /// </summary>
    public List<ResourceEntry> Entries { get; set; } = [];

    /// <summary>
    ///     Whether the translation was successful
    /// </summary>
    public bool Success { get; set; }

    /// <summary>
    ///     Any errors that occurred during translation
    /// </summary>
    public List<TranslationError> Errors { get; set; } = [];

    /// <summary>
    ///     Translation statistics
    /// </summary>
    public TranslationStatistics Statistics { get; set; } = new();

    /// <summary>
    ///     Duration of the translation operation
    /// </summary>
    public TimeSpan Duration { get; set; }

    /// <summary>
    ///     Gets entries that were successfully translated
    /// </summary>
    public IEnumerable<ResourceEntry> TranslatedEntries =>
        Entries.Where(e => e.TranslatedValue != null);

    /// <summary>
    ///     Gets entries that failed translation
    /// </summary>
    public IEnumerable<ResourceEntry> FailedEntries =>
        Entries.Where(e => e.ShouldTranslate && e.TranslatedValue == null);
}

/// <summary>
///     Error that occurred during translation
/// </summary>
public class TranslationError
{
    /// <summary>
    ///     The key that failed
    /// </summary>
    public string? Key { get; set; }

    /// <summary>
    ///     Error message
    /// </summary>
    public required string Message { get; set; }

    /// <summary>
    ///     Exception details (if any)
    /// </summary>
    public string? Exception { get; set; }

    /// <summary>
    ///     Timestamp of the error
    /// </summary>
    public DateTime Timestamp { get; set; } = DateTime.UtcNow;
}

/// <summary>
///     Statistics about the translation operation
/// </summary>
public class TranslationStatistics
{
    /// <summary>
    ///     Total entries in the source file
    /// </summary>
    public int TotalEntries { get; set; }

    /// <summary>
    ///     Entries that were translated
    /// </summary>
    public int TranslatedCount { get; set; }

    /// <summary>
    ///     Entries that were skipped
    /// </summary>
    public int SkippedCount { get; set; }

    /// <summary>
    ///     Entries that failed translation
    /// </summary>
    public int FailedCount { get; set; }

    /// <summary>
    ///     Entries translated using LLM only
    /// </summary>
    public int LlmOnlyCount { get; set; }

    /// <summary>
    ///     Entries translated using NMT only
    /// </summary>
    public int NmtOnlyCount { get; set; }

    /// <summary>
    ///     Entries translated using NMT + LLM
    /// </summary>
    public int NmtPlusLlmCount { get; set; }

    /// <summary>
    ///     Entries translated using RAG + LLM
    /// </summary>
    public int RagLlmCount { get; set; }

    /// <summary>
    ///     Number of glossary terms used
    /// </summary>
    public int GlossaryTermsUsed { get; set; }

    /// <summary>
    ///     Number of context entries used from translation memory
    /// </summary>
    public int ContextEntriesUsed { get; set; }

    /// <summary>
    ///     Average characters per entry
    /// </summary>
    public double AverageCharactersPerEntry { get; set; }

    /// <summary>
    ///     Total characters translated
    /// </summary>
    public int TotalCharactersTranslated { get; set; }

    /// <summary>
    ///     Percentage of entries successfully translated
    /// </summary>
    public double SuccessRate => TotalEntries > 0
        ? (double)TranslatedCount / TotalEntries * 100
        : 0;
}

/// <summary>
///     Result of translating to multiple languages
/// </summary>
public class MultiLanguageTranslationResult
{
    /// <summary>
    ///     The original resource file
    /// </summary>
    public required ResourceFile SourceFile { get; set; }

    /// <summary>
    ///     Source language
    /// </summary>
    public required string SourceLanguage { get; set; }

    /// <summary>
    ///     Results per target language
    /// </summary>
    public Dictionary<string, TranslationResult> Results { get; set; } = new();

    /// <summary>
    ///     Overall success (all languages succeeded)
    /// </summary>
    public bool Success => Results.Values.All(r => r.Success);

    /// <summary>
    ///     Total duration for all translations
    /// </summary>
    public TimeSpan TotalDuration { get; set; }

    /// <summary>
    ///     Gets successfully translated languages
    /// </summary>
    public IEnumerable<string> SuccessfulLanguages =>
        Results.Where(r => r.Value.Success).Select(r => r.Key);

    /// <summary>
    ///     Gets failed languages
    /// </summary>
    public IEnumerable<string> FailedLanguages =>
        Results.Where(r => !r.Value.Success).Select(r => r.Key);
}