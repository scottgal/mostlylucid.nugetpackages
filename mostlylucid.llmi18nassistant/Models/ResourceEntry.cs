namespace Mostlylucid.LlmI18nAssistant.Models;

/// <summary>
///     Represents a single key-value entry in a resource file
/// </summary>
public class ResourceEntry
{
    /// <summary>
    ///     The resource key (never modified during translation)
    /// </summary>
    public required string Key { get; set; }

    /// <summary>
    ///     The source value to be translated
    /// </summary>
    public required string Value { get; set; }

    /// <summary>
    ///     Optional comment/description for the entry
    /// </summary>
    public string? Comment { get; set; }

    /// <summary>
    ///     The translated value (populated after translation)
    /// </summary>
    public string? TranslatedValue { get; set; }

    /// <summary>
    ///     Embedding vector for the source value (used in consistency mode)
    /// </summary>
    public float[]? Embedding { get; set; }

    /// <summary>
    ///     Whether this entry should be translated
    /// </summary>
    public bool ShouldTranslate { get; set; } = true;

    /// <summary>
    ///     Reason for skipping translation (if ShouldTranslate is false)
    /// </summary>
    public string? SkipReason { get; set; }

    /// <summary>
    ///     Translation method used for this entry
    /// </summary>
    public TranslationMethod? TranslationMethod { get; set; }

    /// <summary>
    ///     Context entries used during translation (for consistency mode)
    /// </summary>
    public List<ContextEntry>? ContextUsed { get; set; }

    /// <summary>
    ///     Creates a copy with translation
    /// </summary>
    public ResourceEntry WithTranslation(string translatedValue, TranslationMethod method)
    {
        return new ResourceEntry
        {
            Key = Key,
            Value = Value,
            Comment = Comment,
            TranslatedValue = translatedValue,
            Embedding = Embedding,
            ShouldTranslate = ShouldTranslate,
            SkipReason = SkipReason,
            TranslationMethod = method,
            ContextUsed = ContextUsed
        };
    }
}

/// <summary>
///     Context entry used during translation
/// </summary>
public class ContextEntry
{
    /// <summary>
    ///     Source text from context
    /// </summary>
    public required string SourceText { get; set; }

    /// <summary>
    ///     Translated text from context
    /// </summary>
    public required string TranslatedText { get; set; }

    /// <summary>
    ///     Similarity score (0.0 - 1.0)
    /// </summary>
    public float Similarity { get; set; }

    /// <summary>
    ///     Source of context (glossary, previous translation, same file)
    /// </summary>
    public ContextSource Source { get; set; }
}

/// <summary>
///     Source of context entries
/// </summary>
public enum ContextSource
{
    /// <summary>
    ///     From a glossary file
    /// </summary>
    Glossary,

    /// <summary>
    ///     From a previous translation in the same session
    /// </summary>
    SameFile,

    /// <summary>
    ///     From translation memory (stored embeddings)
    /// </summary>
    TranslationMemory
}

/// <summary>
///     Translation method used
/// </summary>
public enum TranslationMethod
{
    /// <summary>
    ///     LLM only translation
    /// </summary>
    LlmOnly,

    /// <summary>
    ///     NMT only translation
    /// </summary>
    NmtOnly,

    /// <summary>
    ///     NMT baseline with LLM post-editing
    /// </summary>
    NmtPlusLlm,

    /// <summary>
    ///     RAG-enhanced LLM translation with consistency mode
    /// </summary>
    RagLlm,

    /// <summary>
    ///     Entry was skipped (not translated)
    /// </summary>
    Skipped,

    /// <summary>
    ///     Entry was copied as-is
    /// </summary>
    Copied
}
