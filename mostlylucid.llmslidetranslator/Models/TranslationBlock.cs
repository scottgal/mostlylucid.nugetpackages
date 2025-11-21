namespace mostlylucid.llmslidetranslator.Models;

/// <summary>
///     Represents a block of text to be translated with its metadata
/// </summary>
public class TranslationBlock
{
    /// <summary>
    ///     Unique identifier for this block
    /// </summary>
    public required string BlockId { get; set; }

    /// <summary>
    ///     Index/order of this block in the document
    /// </summary>
    public int Index { get; set; }

    /// <summary>
    ///     Document identifier
    /// </summary>
    public required string DocumentId { get; set; }

    /// <summary>
    ///     Original text content
    /// </summary>
    public required string Text { get; set; }

    /// <summary>
    ///     Translated text content
    /// </summary>
    public string? TranslatedText { get; set; }

    /// <summary>
    ///     Embedding vector for this block
    /// </summary>
    public float[]? Embedding { get; set; }

    /// <summary>
    ///     Source language code
    /// </summary>
    public required string SourceLanguage { get; set; }

    /// <summary>
    ///     Target language code
    /// </summary>
    public required string TargetLanguage { get; set; }

    /// <summary>
    ///     Block type (e.g., paragraph, code, heading)
    /// </summary>
    public string BlockType { get; set; } = "paragraph";

    /// <summary>
    ///     Whether this block should be translated (false for code blocks, URLs, etc.)
    /// </summary>
    public bool ShouldTranslate { get; set; } = true;
}