namespace Mostlylucid.LlmI18nAssistant.Models;

/// <summary>
///     Options for translation operations
/// </summary>
public class TranslationOptions
{
    /// <summary>
    ///     Translation method to use
    /// </summary>
    public TranslationMethod Method { get; set; } = TranslationMethod.RagLlm;

    /// <summary>
    ///     Enable consistency mode (RAG over glossary and existing translations)
    /// </summary>
    public bool UseConsistencyMode { get; set; } = true;

    /// <summary>
    ///     Preserve .NET format strings like {0}, {1:N2}
    /// </summary>
    public bool PreserveFormatStrings { get; set; } = true;

    /// <summary>
    ///     Preserve HTML tags in values
    /// </summary>
    public bool PreserveHtmlTags { get; set; } = true;

    /// <summary>
    ///     Additional context for the LLM (e.g., "This is a mobile app for banking")
    /// </summary>
    public string? AdditionalContext { get; set; }

    /// <summary>
    ///     Custom system prompt override
    /// </summary>
    public string? CustomSystemPrompt { get; set; }

    /// <summary>
    ///     Maximum concurrent translations (0 = sequential)
    /// </summary>
    public int MaxConcurrency { get; set; } = 4;

    /// <summary>
    ///     Number of retries for failed translations
    /// </summary>
    public int MaxRetries { get; set; } = 2;

    /// <summary>
    ///     Progress callback for tracking translation progress
    /// </summary>
    public Action<TranslationProgress>? OnProgress { get; set; }

    /// <summary>
    ///     Keys to skip (don't translate)
    /// </summary>
    public HashSet<string> SkipKeys { get; set; } = [];

    /// <summary>
    ///     Only translate these keys (if empty, translate all)
    /// </summary>
    public HashSet<string> OnlyKeys { get; set; } = [];
}

/// <summary>
///     Progress update during translation
/// </summary>
public class TranslationProgress
{
    /// <summary>
    ///     Current entry being translated
    /// </summary>
    public int CurrentIndex { get; set; }

    /// <summary>
    ///     Total entries to translate
    /// </summary>
    public int TotalCount { get; set; }

    /// <summary>
    ///     Current key being translated
    /// </summary>
    public string? CurrentKey { get; set; }

    /// <summary>
    ///     Current value being translated
    /// </summary>
    public string? CurrentValue { get; set; }

    /// <summary>
    ///     Status message
    /// </summary>
    public string? Status { get; set; }

    /// <summary>
    ///     Target language
    /// </summary>
    public string? TargetLanguage { get; set; }

    /// <summary>
    ///     Percentage complete (0-100)
    /// </summary>
    public double PercentComplete => TotalCount > 0
        ? (double)CurrentIndex / TotalCount * 100
        : 0;

    /// <summary>
    ///     Estimated time remaining
    /// </summary>
    public TimeSpan? EstimatedTimeRemaining { get; set; }
}