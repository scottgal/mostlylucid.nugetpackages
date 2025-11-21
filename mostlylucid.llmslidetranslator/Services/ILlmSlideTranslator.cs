using mostlylucid.llmslidetranslator.Models;

namespace mostlylucid.llmslidetranslator.Services;

/// <summary>
///     Main translation service orchestrating RAG-assisted LLM translation
/// </summary>
public interface ILlmSlideTranslator
{
    /// <summary>
    ///     Translate a markdown document using RAG-assisted LLM
    /// </summary>
    /// <param name="markdown">Markdown content</param>
    /// <param name="documentId">Document identifier</param>
    /// <param name="sourceLanguage">Source language code</param>
    /// <param name="targetLanguage">Target language code</param>
    /// <param name="method">Translation method to use</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Translation result</returns>
    Task<TranslationResult> TranslateAsync(
        string markdown,
        string documentId,
        string sourceLanguage,
        string targetLanguage,
        TranslationMethod method = TranslationMethod.RagLlm,
        CancellationToken cancellationToken = default);

    /// <summary>
    ///     Translate a single block with RAG context
    /// </summary>
    /// <param name="block">Block to translate</param>
    /// <param name="previousBlock">Previous block (sliding window)</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Translated block</returns>
    Task<TranslationBlock> TranslateBlockAsync(
        TranslationBlock block,
        TranslationBlock? previousBlock = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    ///     Get translation progress for a document
    /// </summary>
    /// <param name="documentId">Document identifier</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Progress information</returns>
    Task<TranslationProgress> GetProgressAsync(
        string documentId,
        CancellationToken cancellationToken = default);
}

/// <summary>
///     Translation progress information
/// </summary>
public class TranslationProgress
{
    /// <summary>
    ///     Document identifier
    /// </summary>
    public required string DocumentId { get; set; }

    /// <summary>
    ///     Total number of blocks
    /// </summary>
    public int TotalBlocks { get; set; }

    /// <summary>
    ///     Number of blocks translated
    /// </summary>
    public int TranslatedBlocks { get; set; }

    /// <summary>
    ///     Percentage complete (0-100)
    /// </summary>
    public float PercentComplete => TotalBlocks > 0 ? (float)TranslatedBlocks / TotalBlocks * 100 : 0;

    /// <summary>
    ///     Current block being translated
    /// </summary>
    public int? CurrentBlockIndex { get; set; }

    /// <summary>
    ///     Estimated time remaining
    /// </summary>
    public TimeSpan? EstimatedTimeRemaining { get; set; }
}