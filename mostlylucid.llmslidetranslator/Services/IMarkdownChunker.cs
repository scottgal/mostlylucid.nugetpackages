using mostlylucid.llmslidetranslator.Models;

namespace mostlylucid.llmslidetranslator.Services;

/// <summary>
///     Chunks markdown documents into translatable blocks
/// </summary>
public interface IMarkdownChunker
{
    /// <summary>
    ///     Chunk a markdown document into translation blocks
    /// </summary>
    /// <param name="markdown">Markdown content</param>
    /// <param name="documentId">Document identifier</param>
    /// <param name="sourceLanguage">Source language code</param>
    /// <param name="targetLanguage">Target language code</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>List of translation blocks</returns>
    Task<List<TranslationBlock>> ChunkAsync(
        string markdown,
        string documentId,
        string sourceLanguage,
        string targetLanguage,
        CancellationToken cancellationToken = default);
}