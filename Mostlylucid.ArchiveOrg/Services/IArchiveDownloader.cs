using Mostlylucid.ArchiveOrg.Models;

namespace Mostlylucid.ArchiveOrg.Services;

public interface IArchiveDownloader
{
    /// <summary>
    /// Download all archived pages for a website
    /// </summary>
    /// <param name="progress">Optional progress reporter</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>List of download results</returns>
    Task<List<DownloadResult>> DownloadAllAsync(
        IProgress<DownloadProgress>? progress = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Download a single CDX record
    /// </summary>
    Task<DownloadResult> DownloadRecordAsync(
        CdxRecord record,
        CancellationToken cancellationToken = default);
}

public class DownloadProgress
{
    public int TotalRecords { get; set; }
    public int ProcessedRecords { get; set; }
    public int SuccessfulDownloads { get; set; }
    public int FailedDownloads { get; set; }
    public string CurrentUrl { get; set; } = string.Empty;
    public double PercentComplete => TotalRecords > 0 ? (double)ProcessedRecords / TotalRecords * 100 : 0;
}
