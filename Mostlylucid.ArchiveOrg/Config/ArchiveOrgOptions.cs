namespace Mostlylucid.ArchiveOrg.Config;

public class ArchiveOrgOptions
{
    public const string SectionName = "ArchiveOrg";

    /// <summary>
    ///     The base URL of the website to download from Archive.org
    ///     e.g., "https://example.com"
    /// </summary>
    public string TargetUrl { get; set; } = string.Empty;

    /// <summary>
    ///     Optional: Only download snapshots up to this date (inclusive)
    ///     Format: yyyy-MM-dd
    /// </summary>
    public DateTime? EndDate { get; set; }

    /// <summary>
    ///     Optional: Only download snapshots from this date (inclusive)
    /// </summary>
    public DateTime? StartDate { get; set; }

    /// <summary>
    ///     Output directory for downloaded HTML files
    /// </summary>
    public string OutputDirectory { get; set; } = "./archive-output";

    /// <summary>
    ///     Rate limit: milliseconds between requests to Archive.org
    ///     Default: 5000ms (5 seconds) to be respectful of their limits
    /// </summary>
    public int RateLimitMs { get; set; } = 5000;

    /// <summary>
    ///     Maximum concurrent downloads (keep low to respect rate limits)
    /// </summary>
    public int MaxConcurrentDownloads { get; set; } = 1;

    /// <summary>
    ///     URL patterns to include (regex). If empty, all URLs are included.
    /// </summary>
    public List<string> IncludePatterns { get; set; } = [];

    /// <summary>
    ///     URL patterns to exclude (regex)
    /// </summary>
    public List<string> ExcludePatterns { get; set; } = [];

    /// <summary>
    ///     Only download unique URLs (latest snapshot per URL)
    /// </summary>
    public bool UniqueUrlsOnly { get; set; } = true;

    /// <summary>
    ///     MIME types to include (defaults to text/html if empty)
    /// </summary>
    public List<string> MimeTypes { get; set; } = [];

    /// <summary>
    ///     HTTP status codes to include (defaults to 200 if empty)
    /// </summary>
    public List<int> StatusCodes { get; set; } = [];

    /// <summary>
    ///     Timeout in seconds for each HTTP request
    ///     Default: 180 seconds (3 minutes)
    /// </summary>
    public int RequestTimeoutSeconds { get; set; } = 180;

    /// <summary>
    ///     Number of retry attempts for failed downloads
    ///     Default: 3
    /// </summary>
    public int MaxRetries { get; set; } = 3;

    /// <summary>
    ///     Delay in milliseconds between retry attempts
    ///     Default: 5000ms (5 seconds)
    /// </summary>
    public int RetryDelayMs { get; set; } = 5000;
}