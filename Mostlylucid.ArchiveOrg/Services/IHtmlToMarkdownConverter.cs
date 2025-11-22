using Mostlylucid.ArchiveOrg.Models;

namespace Mostlylucid.ArchiveOrg.Services;

public interface IHtmlToMarkdownConverter
{
    /// <summary>
    /// Convert all HTML files in the input directory to Markdown
    /// </summary>
    /// <param name="progress">Optional progress reporter</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>List of converted articles</returns>
    Task<List<MarkdownArticle>> ConvertAllAsync(
        IProgress<ConversionProgress>? progress = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Convert a single HTML file to Markdown (may return multiple articles for multi-post pages)
    /// </summary>
    Task<List<MarkdownArticle>> ConvertFileAsync(
        string htmlFilePath,
        CancellationToken cancellationToken = default);
}

public class ConversionProgress
{
    public int TotalFiles { get; set; }
    public int ProcessedFiles { get; set; }
    public int SuccessfulConversions { get; set; }
    public int FailedConversions { get; set; }
    public string CurrentFile { get; set; } = string.Empty;
    public double PercentComplete => TotalFiles > 0 ? (double)ProcessedFiles / TotalFiles * 100 : 0;
}
