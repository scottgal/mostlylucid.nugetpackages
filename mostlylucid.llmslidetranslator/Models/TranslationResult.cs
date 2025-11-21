namespace mostlylucid.llmslidetranslator.Models;

/// <summary>
///     Result of a translation operation
/// </summary>
public class TranslationResult
{
    /// <summary>
    ///     Document identifier
    /// </summary>
    public required string DocumentId { get; set; }

    /// <summary>
    ///     Translated blocks
    /// </summary>
    public List<TranslationBlock> Blocks { get; set; } = new();

    /// <summary>
    ///     Source language
    /// </summary>
    public required string SourceLanguage { get; set; }

    /// <summary>
    ///     Target language
    /// </summary>
    public required string TargetLanguage { get; set; }

    /// <summary>
    ///     Translation method used
    /// </summary>
    public TranslationMethod Method { get; set; }

    /// <summary>
    ///     Total time taken for translation
    /// </summary>
    public TimeSpan Duration { get; set; }

    /// <summary>
    ///     Any errors encountered
    /// </summary>
    public List<string> Errors { get; set; } = new();

    /// <summary>
    ///     Get the full translated text by concatenating all blocks
    /// </summary>
    public string GetTranslatedText()
    {
        return string.Join("\n\n", Blocks
            .OrderBy(b => b.Index)
            .Select(b => b.TranslatedText ?? b.Text));
    }
}

/// <summary>
///     Translation method used
/// </summary>
public enum TranslationMethod
{
    /// <summary>
    ///     LLM only
    /// </summary>
    LlmOnly,

    /// <summary>
    ///     NMT only
    /// </summary>
    NmtOnly,

    /// <summary>
    ///     NMT baseline + LLM post-editing
    /// </summary>
    NmtPlusLlm,

    /// <summary>
    ///     RAG-enhanced LLM
    /// </summary>
    RagLlm
}

/// <summary>
///     Comparison between two translations
/// </summary>
public class TranslationComparison
{
    /// <summary>
    ///     Document identifier
    /// </summary>
    public required string DocumentId { get; set; }

    /// <summary>
    ///     First translation result
    /// </summary>
    public required TranslationResult Translation1 { get; set; }

    /// <summary>
    ///     Second translation result
    /// </summary>
    public required TranslationResult Translation2 { get; set; }

    /// <summary>
    ///     Block-by-block differences
    /// </summary>
    public List<BlockDifference> Differences { get; set; } = new();

    /// <summary>
    ///     Overall similarity score (0.0 - 1.0)
    /// </summary>
    public float SimilarityScore { get; set; }
}

/// <summary>
///     Difference between two translation blocks
/// </summary>
public class BlockDifference
{
    /// <summary>
    ///     Block index
    /// </summary>
    public int BlockIndex { get; set; }

    /// <summary>
    ///     Text from translation 1
    /// </summary>
    public required string Text1 { get; set; }

    /// <summary>
    ///     Text from translation 2
    /// </summary>
    public required string Text2 { get; set; }

    /// <summary>
    ///     Similarity score for this block (0.0 - 1.0)
    /// </summary>
    public float Similarity { get; set; }

    /// <summary>
    ///     Edit distance between the two texts
    /// </summary>
    public int EditDistance { get; set; }
}