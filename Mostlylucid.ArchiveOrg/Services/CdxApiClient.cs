using System.Text.Json;
using System.Text.RegularExpressions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Mostlylucid.ArchiveOrg.Config;
using Mostlylucid.ArchiveOrg.Models;

namespace Mostlylucid.ArchiveOrg.Services;

public class CdxApiClient : ICdxApiClient
{
    private const string CdxApiBaseUrl = "https://web.archive.org/cdx/search/cdx";
    private readonly HttpClient _httpClient;
    private readonly ILogger<CdxApiClient> _logger;
    private readonly ArchiveOrgOptions _options;

    public CdxApiClient(
        HttpClient httpClient,
        IOptions<ArchiveOrgOptions> options,
        ILogger<CdxApiClient> logger)
    {
        _httpClient = httpClient;
        _logger = logger;
        _options = options.Value;
    }

    public async Task<List<CdxRecord>> GetCdxRecordsAsync(
        string url,
        DateTime? startDate = null,
        DateTime? endDate = null,
        CancellationToken cancellationToken = default)
    {
        var records = new List<CdxRecord>();
        var maxRetries = _options.MaxRetries;
        var timeout = TimeSpan.FromSeconds(_options.RequestTimeoutSeconds);

        for (var attempt = 1; attempt <= maxRetries; attempt++)
            try
            {
                // Build the CDX API URL
                // Note: The /* wildcard implies prefix matching - no need for matchType parameter
                var queryParams = new List<string>
                {
                    $"url={Uri.EscapeDataString(url)}/*",
                    "output=json",
                    "fl=urlkey,timestamp,original,mimetype,statuscode,digest,length"
                };

                // Add date filters if specified
                if (startDate.HasValue) queryParams.Add($"from={startDate.Value:yyyyMMdd}");

                if (endDate.HasValue) queryParams.Add($"to={endDate.Value:yyyyMMdd}");

                // Add MIME type filter
                if (_options.MimeTypes.Count > 0)
                    foreach (var mimeType in _options.MimeTypes)
                        queryParams.Add($"filter=mimetype:{mimeType}");

                // Add status code filter
                if (_options.StatusCodes.Count > 0)
                    foreach (var statusCode in _options.StatusCodes)
                        queryParams.Add($"filter=statuscode:{statusCode}");

                // Collapse to unique URLs if requested
                if (_options.UniqueUrlsOnly) queryParams.Add("collapse=urlkey");

                var apiUrl = $"{CdxApiBaseUrl}?{string.Join("&", queryParams)}";
                _logger.LogInformation("Fetching CDX records from: {Url} (attempt {Attempt}/{MaxRetries})",
                    apiUrl, attempt, maxRetries);

                // Create a linked cancellation token with timeout
                using var timeoutCts = new CancellationTokenSource(timeout);
                using var linkedCts = CancellationTokenSource.CreateLinkedTokenSource(
                    cancellationToken, timeoutCts.Token);

                var response = await _httpClient.GetAsync(apiUrl, linkedCts.Token);
                response.EnsureSuccessStatusCode();

                _logger.LogInformation("CDX response received, reading content...");
                var content = await response.Content.ReadAsStringAsync(linkedCts.Token);
                _logger.LogInformation("CDX response read: {Length:N0} bytes", content.Length);

                if (string.IsNullOrWhiteSpace(content))
                {
                    _logger.LogWarning("No CDX records found for URL: {Url}", url);
                    return records;
                }

                // Parse JSON array response - handle truncated responses
                _logger.LogInformation("Parsing CDX JSON response...");
                string[][]? jsonArray = null;
                try
                {
                    jsonArray = JsonSerializer.Deserialize<string[][]>(content);
                }
                catch (JsonException jsonEx) when (jsonEx.Message.Contains("end of data") ||
                                                   jsonEx.Message.Contains("truncated"))
                {
                    // Response was truncated - try to salvage what we can
                    _logger.LogWarning(
                        "CDX response appears truncated at line {Line}, attempting to parse partial data...",
                        jsonEx.LineNumber);

                    // Try to fix the JSON by finding the last complete array and closing it
                    var lastCompleteRow = content.LastIndexOf("],", StringComparison.Ordinal);
                    if (lastCompleteRow > 0)
                    {
                        var fixedContent = content[..(lastCompleteRow + 1)] + "]";
                        try
                        {
                            jsonArray = JsonSerializer.Deserialize<string[][]>(fixedContent);
                            _logger.LogInformation(
                                "Successfully parsed truncated response - recovered {Count:N0} records",
                                jsonArray?.Length ?? 0);
                        }
                        catch
                        {
                            _logger.LogWarning("Could not recover truncated CDX response");
                        }
                    }
                }

                if (jsonArray == null || jsonArray.Length == 0)
                {
                    _logger.LogWarning("CDX response was empty or invalid JSON");
                    return records;
                }

                _logger.LogInformation("CDX JSON parsed: {Count:N0} raw records, filtering...", jsonArray.Length - 1);

                // Skip the header row (first row contains column names)
                var processedCount = 0;
                var filteredCount = 0;
                foreach (var row in jsonArray.Skip(1))
                    try
                    {
                        var record = CdxRecord.FromJsonArray(row);
                        processedCount++;

                        // Apply include/exclude filters
                        if (ShouldIncludeRecord(record))
                            records.Add(record);
                        else
                            filteredCount++;

                        // Log progress every 1000 records
                        if (processedCount % 1000 == 0)
                            _logger.LogInformation("Processing CDX records: {Processed:N0}/{Total:N0}...",
                                processedCount, jsonArray.Length - 1);
                    }
                    catch (Exception ex)
                    {
                        _logger.LogWarning(ex, "Failed to parse CDX record: {Row}", string.Join(",", row));
                    }

                _logger.LogInformation("Found {Count:N0} CDX records for URL: {Url} (filtered out {Filtered:N0})",
                    records.Count, url, filteredCount);
                return records;
            }
            catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
            {
                // This was a timeout, not a user cancellation
                _logger.LogWarning("Timeout fetching CDX records (attempt {Attempt}/{MaxRetries})", attempt,
                    maxRetries);

                if (attempt < maxRetries)
                {
                    _logger.LogInformation("Retrying in {Delay}ms...", _options.RetryDelayMs);
                    await Task.Delay(_options.RetryDelayMs, cancellationToken);
                    continue;
                }

                throw new TimeoutException(
                    $"Failed to fetch CDX records after {maxRetries} attempts (timeout: {_options.RequestTimeoutSeconds}s)");
            }
            catch (HttpRequestException ex)
            {
                _logger.LogWarning(ex, "HTTP error fetching CDX records (attempt {Attempt}/{MaxRetries})", attempt,
                    maxRetries);

                if (attempt < maxRetries)
                {
                    _logger.LogInformation("Retrying in {Delay}ms...", _options.RetryDelayMs);
                    await Task.Delay(_options.RetryDelayMs, cancellationToken);
                    continue;
                }

                throw;
            }
            catch (OperationCanceledException)
            {
                // User cancellation - rethrow immediately
                throw;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error fetching CDX records for URL: {Url} (attempt {Attempt}/{MaxRetries})",
                    url, attempt, maxRetries);

                if (attempt < maxRetries)
                {
                    _logger.LogInformation("Retrying in {Delay}ms...", _options.RetryDelayMs);
                    await Task.Delay(_options.RetryDelayMs, cancellationToken);
                    continue;
                }

                throw;
            }

        return records;
    }

    public async Task<List<CdxRecord>> GetCdxRecordsAsync(
        IEnumerable<string> urls,
        DateTime? startDate = null,
        DateTime? endDate = null,
        CancellationToken cancellationToken = default)
    {
        var allRecords = new List<CdxRecord>();
        var seenDigests = new HashSet<string>();

        foreach (var url in urls)
        {
            _logger.LogInformation("Fetching CDX records for: {Url}", url);
            var records = await GetCdxRecordsAsync(url, startDate, endDate, cancellationToken);

            // Deduplicate by digest (same content = same digest)
            var newRecords = 0;
            foreach (var record in records)
            {
                if (seenDigests.Add(record.Digest))
                {
                    allRecords.Add(record);
                    newRecords++;
                }
            }

            _logger.LogInformation("Added {New} new records from {Url} ({Dupes} duplicates skipped)",
                newRecords, url, records.Count - newRecords);
        }

        _logger.LogInformation("Total unique records from all URLs: {Count}", allRecords.Count);
        return allRecords;
    }

    private bool ShouldIncludeRecord(CdxRecord record)
    {
        // Check include patterns
        if (_options.IncludePatterns.Count > 0)
        {
            var matches = _options.IncludePatterns.Any(pattern =>
                Regex.IsMatch(record.OriginalUrl, pattern));

            if (!matches) return false;
        }

        // Check exclude patterns
        if (_options.ExcludePatterns.Count > 0)
        {
            var excluded = _options.ExcludePatterns.Any(pattern =>
                Regex.IsMatch(record.OriginalUrl, pattern));

            if (excluded) return false;
        }

        return true;
    }
}