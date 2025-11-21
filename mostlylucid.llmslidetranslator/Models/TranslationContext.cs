namespace mostlylucid.llmslidetranslator.Models;

/// <summary>
///     Context information for translating a block
/// </summary>
public class TranslationContext
{
    /// <summary>
    ///     The block to translate
    /// </summary>
    public required TranslationBlock CurrentBlock { get; set; }

    /// <summary>
    ///     Previous block (sliding window)
    /// </summary>
    public TranslationBlock? PreviousBlock { get; set; }

    /// <summary>
    ///     Similar blocks retrieved via RAG
    /// </summary>
    public List<TranslationBlock> SimilarBlocks { get; set; } = new();

    /// <summary>
    ///     System instructions for the LLM
    /// </summary>
    public string? SystemPrompt { get; set; }

    /// <summary>
    ///     Additional context instructions
    /// </summary>
    public string? AdditionalContext { get; set; }
}