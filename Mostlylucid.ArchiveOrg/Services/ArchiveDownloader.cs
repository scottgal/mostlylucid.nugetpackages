using System.Text.RegularExpressions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Mostlylucid.ArchiveOrg.Config;
using Mostlylucid.ArchiveOrg.Models;

namespace Mostlylucid.ArchiveOrg.Services;

public partial class ArchiveDownloader : IArchiveDownloader
{
    private readonly ICdxApiClient _cdxApiClient;
    private readonly HttpClient _httpClient;
    private readonly ILogger<ArchiveDownloader> _logger;
    private readonly ArchiveOrgOptions _options;
    private readonly SemaphoreSlim _rateLimiter;
    private DateTime _lastRequestTime = DateTime.MinValue;

    public ArchiveDownloader(
        HttpClient httpClient,
        ICdxApiClient cdxApiClient,
        IOptions<ArchiveOrgOptions> options,
        ILogger<ArchiveDownloader> logger)
    {
        _httpClient = httpClient;
        _cdxApiClient = cdxApiClient;
        _options = options.Value;
        _logger = logger;
        _rateLimiter = new SemaphoreSlim(_options.MaxConcurrentDownloads);
    }

    public async Task<List<DownloadResult>> DownloadAllAsync(
        IProgress<DownloadProgress>? progress = null,
        CancellationToken cancellationToken = default)
    {
        var results = new List<DownloadResult>();

        // Ensure output directory exists
        Directory.CreateDirectory(_options.OutputDirectory);

        // Get all CDX records
        _logger.LogInformation("Fetching CDX records for {Url}", _options.TargetUrl);
        var records = await _cdxApiClient.GetCdxRecordsAsync(
            _options.TargetUrl,
            _options.StartDate,
            _options.EndDate,
            cancellationToken);

        if (records.Count == 0)
        {
            _logger.LogWarning("No records found for URL: {Url}", _options.TargetUrl);
            return results;
        }

        _logger.LogInformation("Found {Count} records to download", records.Count);

        var progressReport = new DownloadProgress { TotalRecords = records.Count };

        foreach (var record in records)
        {
            cancellationToken.ThrowIfCancellationRequested();

            progressReport.CurrentUrl = record.OriginalUrl;
            progress?.Report(progressReport);

            var result = await DownloadRecordAsync(record, cancellationToken);
            results.Add(result);

            progressReport.ProcessedRecords++;
            if (result.Success)
                progressReport.SuccessfulDownloads++;
            else
                progressReport.FailedDownloads++;

            progress?.Report(progressReport);

            _logger.LogInformation(
                "Progress: {Processed}/{Total} ({Percent:F1}%) - {Url}",
                progressReport.ProcessedRecords,
                progressReport.TotalRecords,
                progressReport.PercentComplete,
                record.OriginalUrl);
        }

        return results;
    }

    public async Task<DownloadResult> DownloadRecordAsync(
        CdxRecord record,
        CancellationToken cancellationToken = default)
    {
        await _rateLimiter.WaitAsync(cancellationToken);

        try
        {
            // Enforce rate limiting
            await EnforceRateLimitAsync(cancellationToken);

            // Generate output filename
            var fileName = GenerateFileName(record);
            var filePath = Path.Combine(_options.OutputDirectory, fileName);

            // Skip if already downloaded
            if (File.Exists(filePath))
            {
                _logger.LogInformation("Skipping already downloaded: {Url}", record.OriginalUrl);
                return DownloadResult.Succeeded(record, filePath);
            }

            // Retry loop with timeout handling
            var maxRetries = _options.MaxRetries;
            var timeout = TimeSpan.FromSeconds(_options.RequestTimeoutSeconds);

            for (var attempt = 1; attempt <= maxRetries; attempt++)
                try
                {
                    _logger.LogInformation("Downloading (attempt {Attempt}/{MaxRetries}): {Url}",
                        attempt, maxRetries, record.OriginalUrl);

                    // Create a linked cancellation token with timeout
                    using var timeoutCts = new CancellationTokenSource(timeout);
                    using var linkedCts = CancellationTokenSource.CreateLinkedTokenSource(
                        cancellationToken, timeoutCts.Token);

                    // Download from Wayback Machine (use raw URL to avoid JS modifications)
                    var response = await _httpClient.GetAsync(record.WaybackRawUrl, linkedCts.Token);

                    if (!response.IsSuccessStatusCode)
                    {
                        var error = $"HTTP {(int)response.StatusCode}: {response.ReasonPhrase}";
                        _logger.LogWarning("Failed to download {Url}: {Error}", record.OriginalUrl, error);

                        // Don't retry on 4xx client errors (except 429 rate limit)
                        if ((int)response.StatusCode >= 400 && (int)response.StatusCode < 500
                                                            && (int)response.StatusCode != 429)
                            return DownloadResult.Failed(record, error);

                        // Retry on server errors or rate limiting
                        if (attempt < maxRetries)
                        {
                            _logger.LogInformation("Retrying in {Delay}ms...", _options.RetryDelayMs);
                            await Task.Delay(_options.RetryDelayMs, cancellationToken);
                            continue;
                        }

                        return DownloadResult.Failed(record, error);
                    }

                    var content = await response.Content.ReadAsStringAsync(linkedCts.Token);

                    // Clean up Wayback Machine artifacts from the HTML
                    content = CleanWaybackArtifacts(content);

                    // Write to file with metadata
                    await WriteHtmlFileAsync(filePath, record, content, cancellationToken);

                    _logger.LogInformation("Downloaded: {Url} -> {FilePath}", record.OriginalUrl, filePath);
                    return DownloadResult.Succeeded(record, filePath, content);
                }
                catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
                {
                    // This was a timeout, not a user cancellation
                    _logger.LogWarning("Timeout downloading {Url} (attempt {Attempt}/{MaxRetries})",
                        record.OriginalUrl, attempt, maxRetries);

                    if (attempt < maxRetries)
                    {
                        _logger.LogInformation("Retrying in {Delay}ms...", _options.RetryDelayMs);
                        await Task.Delay(_options.RetryDelayMs, cancellationToken);
                        continue;
                    }

                    return DownloadResult.Failed(record,
                        $"Timeout after {_options.RequestTimeoutSeconds} seconds (all {maxRetries} attempts failed)");
                }
                catch (HttpRequestException ex)
                {
                    _logger.LogWarning(ex, "HTTP error downloading {Url} (attempt {Attempt}/{MaxRetries})",
                        record.OriginalUrl, attempt, maxRetries);

                    if (attempt < maxRetries)
                    {
                        _logger.LogInformation("Retrying in {Delay}ms...", _options.RetryDelayMs);
                        await Task.Delay(_options.RetryDelayMs, cancellationToken);
                        continue;
                    }

                    return DownloadResult.Failed(record, $"HTTP error: {ex.Message}");
                }

            // Should not reach here, but just in case
            return DownloadResult.Failed(record, "Unknown error - all retries exhausted");
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            // User requested cancellation
            _logger.LogInformation("Download cancelled by user: {Url}", record.OriginalUrl);
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unexpected error downloading {Url}", record.OriginalUrl);
            return DownloadResult.Failed(record, ex.Message);
        }
        finally
        {
            _rateLimiter.Release();
        }
    }

    private async Task EnforceRateLimitAsync(CancellationToken cancellationToken)
    {
        var timeSinceLastRequest = DateTime.UtcNow - _lastRequestTime;
        var requiredDelay = TimeSpan.FromMilliseconds(_options.RateLimitMs);

        if (timeSinceLastRequest < requiredDelay)
        {
            var delay = requiredDelay - timeSinceLastRequest;
            _logger.LogDebug("Rate limiting: waiting {Delay}ms", delay.TotalMilliseconds);
            await Task.Delay(delay, cancellationToken);
        }

        _lastRequestTime = DateTime.UtcNow;
    }

    private string GenerateFileName(CdxRecord record)
    {
        // Create a safe filename from the URL
        var uri = new Uri(record.OriginalUrl);
        var pathPart = uri.AbsolutePath.Trim('/').Replace('/', '_');

        if (string.IsNullOrEmpty(pathPart))
            pathPart = "index";

        // Sanitize the filename
        var invalidChars = Path.GetInvalidFileNameChars();
        foreach (var c in invalidChars) pathPart = pathPart.Replace(c, '_');

        // Add timestamp to ensure uniqueness
        return $"{pathPart}_{record.Timestamp}.html";
    }

    private static string CleanWaybackArtifacts(string html)
    {
        // Remove Wayback Machine toolbar and scripts
        html = WaybackToolbarRegex().Replace(html, string.Empty);
        html = WaybackScriptRegex().Replace(html, string.Empty);
        html = WaybackCommentRegex().Replace(html, string.Empty);

        // Remove Wayback Machine URL rewrites in href and src attributes
        html = WaybackUrlRewriteRegex().Replace(html, "$1$2");

        return html;
    }

    private static async Task WriteHtmlFileAsync(
        string filePath,
        CdxRecord record,
        string content,
        CancellationToken cancellationToken)
    {
        // Add metadata comment at the top of the file
        var metadata = $"""
                        <!--
                        Archive.org Download Metadata:
                        Original URL: {record.OriginalUrl}
                        Archive Date: {record.ArchiveDate:yyyy-MM-dd HH:mm:ss}
                        Wayback URL: {record.WaybackUrl}
                        Downloaded: {DateTime.UtcNow:yyyy-MM-dd HH:mm:ss} UTC
                        -->
                        """;

        var fullContent = metadata + "\n" + content;
        await File.WriteAllTextAsync(filePath, fullContent, cancellationToken);
    }

    [GeneratedRegex(@"<!-- BEGIN WAYBACK TOOLBAR INSERT -->.*?<!-- END WAYBACK TOOLBAR INSERT -->",
        RegexOptions.Singleline)]
    private static partial Regex WaybackToolbarRegex();

    [GeneratedRegex(@"<script[^>]*>.*?//playback\.archive\.org.*?</script>",
        RegexOptions.Singleline | RegexOptions.IgnoreCase)]
    private static partial Regex WaybackScriptRegex();

    [GeneratedRegex(@"<!--\s*FILE ARCHIVED ON.*?-->", RegexOptions.Singleline)]
    private static partial Regex WaybackCommentRegex();

    [GeneratedRegex(@"(href|src)=""https?://web\.archive\.org/web/\d+[a-z_]*/", RegexOptions.IgnoreCase)]
    private static partial Regex WaybackUrlRewriteRegex();
}