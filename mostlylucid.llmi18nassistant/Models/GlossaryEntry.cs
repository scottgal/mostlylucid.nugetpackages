namespace Mostlylucid.LlmI18nAssistant.Models;

/// <summary>
///     Represents a glossary term with translations
/// </summary>
public class GlossaryEntry
{
    /// <summary>
    ///     Unique identifier for this glossary entry
    /// </summary>
    public string Id { get; set; } = Guid.NewGuid().ToString("N");

    /// <summary>
    ///     The source term
    /// </summary>
    public required string SourceTerm { get; set; }

    /// <summary>
    ///     Source language
    /// </summary>
    public required string SourceLanguage { get; set; }

    /// <summary>
    ///     Translations per target language
    /// </summary>
    public Dictionary<string, string> Translations { get; set; } = new();

    /// <summary>
    ///     Context or usage notes
    /// </summary>
    public string? Context { get; set; }

    /// <summary>
    ///     Category (e.g., "UI", "Technical", "Legal")
    /// </summary>
    public string? Category { get; set; }

    /// <summary>
    ///     Whether this is a "do not translate" term
    /// </summary>
    public bool DoNotTranslate { get; set; }

    /// <summary>
    ///     Embedding vector for the source term
    /// </summary>
    public float[]? Embedding { get; set; }

    /// <summary>
    ///     Gets translation for a specific language
    /// </summary>
    public string? GetTranslation(string language)
    {
        if (DoNotTranslate)
            return SourceTerm;

        return Translations.TryGetValue(language, out var translation)
            ? translation
            : null;
    }
}

/// <summary>
///     A collection of glossary entries
/// </summary>
public class Glossary
{
    /// <summary>
    ///     Glossary name/identifier
    /// </summary>
    public string Name { get; set; } = "Default";

    /// <summary>
    ///     Description of the glossary
    /// </summary>
    public string? Description { get; set; }

    /// <summary>
    ///     Source language for all entries
    /// </summary>
    public string SourceLanguage { get; set; } = "en";

    /// <summary>
    ///     Available target languages
    /// </summary>
    public List<string> TargetLanguages { get; set; } = [];

    /// <summary>
    ///     All glossary entries
    /// </summary>
    public List<GlossaryEntry> Entries { get; set; } = [];

    /// <summary>
    ///     When the glossary was last updated
    /// </summary>
    public DateTime LastUpdated { get; set; } = DateTime.UtcNow;

    /// <summary>
    ///     Version of the glossary
    /// </summary>
    public string Version { get; set; } = "1.0";
}

/// <summary>
///     Translation memory entry (stores past translations)
/// </summary>
public class TranslationMemoryEntry
{
    /// <summary>
    ///     Unique identifier
    /// </summary>
    public string Id { get; set; } = Guid.NewGuid().ToString("N");

    /// <summary>
    ///     Source text
    /// </summary>
    public required string SourceText { get; set; }

    /// <summary>
    ///     Source language
    /// </summary>
    public required string SourceLanguage { get; set; }

    /// <summary>
    ///     Target language
    /// </summary>
    public required string TargetLanguage { get; set; }

    /// <summary>
    ///     Translated text
    /// </summary>
    public required string TranslatedText { get; set; }

    /// <summary>
    ///     Embedding vector for the source text
    /// </summary>
    public float[]? Embedding { get; set; }

    /// <summary>
    ///     When this translation was created
    /// </summary>
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    /// <summary>
    ///     Source of this translation (file, manual, etc.)
    /// </summary>
    public string? Source { get; set; }

    /// <summary>
    ///     Quality score (if available)
    /// </summary>
    public float? QualityScore { get; set; }
}