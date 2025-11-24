namespace Mostlylucid.ArchiveOrg.Models;

/// <summary>
///     Result of downloading an archived page
/// </summary>
public class DownloadResult
{
    public bool Success { get; set; }
    public string OriginalUrl { get; set; } = string.Empty;
    public DateTime ArchiveDate { get; set; }
    public string? FilePath { get; set; }
    public string? HtmlContent { get; set; }
    public string? ErrorMessage { get; set; }
    public CdxRecord? CdxRecord { get; set; }

    public static DownloadResult Failed(CdxRecord record, string error)
    {
        return new DownloadResult
        {
            Success = false,
            OriginalUrl = record.OriginalUrl,
            ArchiveDate = record.ArchiveDate,
            ErrorMessage = error,
            CdxRecord = record
        };
    }

    public static DownloadResult Succeeded(CdxRecord record, string filePath, string? content = null)
    {
        return new DownloadResult
        {
            Success = true,
            OriginalUrl = record.OriginalUrl,
            ArchiveDate = record.ArchiveDate,
            FilePath = filePath,
            HtmlContent = content,
            CdxRecord = record
        };
    }
}