namespace Mostlylucid.ArchiveOrg.Services;

public interface IOllamaTagGenerator
{
    /// <summary>
    /// Generate tags/categories for an article using Ollama LLM
    /// </summary>
    /// <param name="title">Article title</param>
    /// <param name="content">Article content (markdown or plain text)</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>List of generated tags</returns>
    Task<List<string>> GenerateTagsAsync(
        string title,
        string content,
        CancellationToken cancellationToken = default);
}
