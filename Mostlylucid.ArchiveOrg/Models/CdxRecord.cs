namespace Mostlylucid.ArchiveOrg.Models;

/// <summary>
/// Represents a single record from the Archive.org CDX API
/// CDX format: urlkey, timestamp, original, mimetype, statuscode, digest, length
/// </summary>
public class CdxRecord
{
    /// <summary>
    /// SURT-format URL key
    /// </summary>
    public string UrlKey { get; set; } = string.Empty;

    /// <summary>
    /// Archive timestamp in format: yyyyMMddHHmmss
    /// </summary>
    public string Timestamp { get; set; } = string.Empty;

    /// <summary>
    /// Original URL that was archived
    /// </summary>
    public string OriginalUrl { get; set; } = string.Empty;

    /// <summary>
    /// MIME type of the archived content
    /// </summary>
    public string MimeType { get; set; } = string.Empty;

    /// <summary>
    /// HTTP status code when archived
    /// </summary>
    public int StatusCode { get; set; }

    /// <summary>
    /// Content digest/hash
    /// </summary>
    public string Digest { get; set; } = string.Empty;

    /// <summary>
    /// Content length in bytes
    /// </summary>
    public long Length { get; set; }

    /// <summary>
    /// Parsed datetime from the timestamp
    /// </summary>
    public DateTime ArchiveDate => ParseTimestamp(Timestamp);

    /// <summary>
    /// Full Wayback Machine URL to access this snapshot
    /// </summary>
    public string WaybackUrl => $"https://web.archive.org/web/{Timestamp}/{OriginalUrl}";

    /// <summary>
    /// Raw Wayback Machine URL (without replay modifications)
    /// </summary>
    public string WaybackRawUrl => $"https://web.archive.org/web/{Timestamp}id_/{OriginalUrl}";

    private static DateTime ParseTimestamp(string timestamp)
    {
        if (string.IsNullOrEmpty(timestamp) || timestamp.Length < 14)
            return DateTime.MinValue;

        if (DateTime.TryParseExact(
                timestamp[..14],
                "yyyyMMddHHmmss",
                null,
                System.Globalization.DateTimeStyles.None,
                out var result))
        {
            return result;
        }

        return DateTime.MinValue;
    }

    /// <summary>
    /// Parse a CDX API JSON array row into a CdxRecord
    /// </summary>
    public static CdxRecord FromJsonArray(string[] fields)
    {
        if (fields.Length < 7)
            throw new ArgumentException("CDX record requires at least 7 fields");

        return new CdxRecord
        {
            UrlKey = fields[0],
            Timestamp = fields[1],
            OriginalUrl = fields[2],
            MimeType = fields[3],
            StatusCode = int.TryParse(fields[4], out var code) ? code : 0,
            Digest = fields[5],
            Length = long.TryParse(fields[6], out var len) ? len : 0
        };
    }
}
