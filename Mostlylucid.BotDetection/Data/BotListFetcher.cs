using System.Text.Json;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;

namespace Mostlylucid.BotDetection.Data;

/// <summary>
///     Service for fetching and caching bot detection lists from authoritative sources
/// </summary>
public interface IBotListFetcher
{
    Task<List<string>> GetCrawlerUserAgentsAsync(CancellationToken cancellationToken = default);
    Task<List<string>> GetDatacenterIpRangesAsync(CancellationToken cancellationToken = default);
    Task<List<BotPattern>> GetMatomoBotPatternsAsync(CancellationToken cancellationToken = default);
}

public class BotPattern
{
    public string? Name { get; set; }
    public string? Pattern { get; set; }
    public string? Category { get; set; }
    public string? Url { get; set; }
}

/// <summary>
///     Fetches bot lists from authoritative sources with caching
/// </summary>
public class BotListFetcher : IBotListFetcher
{
    private static readonly TimeSpan CacheDuration = TimeSpan.FromHours(24);
    private readonly IMemoryCache _cache;
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly ILogger<BotListFetcher> _logger;

    public BotListFetcher(
        IHttpClientFactory httpClientFactory,
        IMemoryCache cache,
        ILogger<BotListFetcher> logger)
    {
        _httpClientFactory = httpClientFactory;
        _cache = cache;
        _logger = logger;
    }

    public async Task<List<string>> GetCrawlerUserAgentsAsync(CancellationToken cancellationToken = default)
    {
        const string cacheKey = "bot_list_crawler_uas";

        if (_cache.TryGetValue<List<string>>(cacheKey, out var cached) && cached != null) return cached;

        try
        {
            var client = _httpClientFactory.CreateClient();
            client.Timeout = TimeSpan.FromSeconds(30);

            // Fetch from crawler-user-agents repository
            var json = await client.GetStringAsync(BotListSources.CrawlerUserAgents, cancellationToken);
            var crawlers = JsonSerializer.Deserialize<List<CrawlerEntry>>(json);

            var patterns = crawlers?
                .Where(c => !string.IsNullOrEmpty(c.Pattern))
                .Select(c => c.Pattern!)
                .ToList() ?? new List<string>();

            _logger.LogInformation("Fetched {Count} crawler patterns from remote source", patterns.Count);

            _cache.Set(cacheKey, patterns, CacheDuration);
            return patterns;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to fetch crawler user agents, using fallback");
            return GetFallbackCrawlerPatterns();
        }
    }

    public async Task<List<string>> GetDatacenterIpRangesAsync(CancellationToken cancellationToken = default)
    {
        const string cacheKey = "bot_list_datacenter_ips";

        if (_cache.TryGetValue<List<string>>(cacheKey, out var cached) && cached != null) return cached;

        var ranges = new List<string>();

        try
        {
            var client = _httpClientFactory.CreateClient();
            client.Timeout = TimeSpan.FromSeconds(30);

            // Fetch AWS IP ranges
            try
            {
                var awsJson = await client.GetStringAsync(BotListSources.AwsIpRanges, cancellationToken);
                var awsData = JsonSerializer.Deserialize<AwsIpRanges>(awsJson);
                if (awsData?.Prefixes != null)
                {
                    ranges.AddRange(awsData.Prefixes.Select(p => p.IpPrefix));
                    _logger.LogInformation("Fetched {Count} AWS IP ranges", awsData.Prefixes.Count);
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to fetch AWS IP ranges");
            }

            // Fetch GCP IP ranges
            try
            {
                var gcpJson = await client.GetStringAsync(BotListSources.GcpIpRanges, cancellationToken);
                var gcpData = JsonSerializer.Deserialize<GcpIpRanges>(gcpJson);
                if (gcpData?.Prefixes != null)
                {
                    ranges.AddRange(gcpData.Prefixes
                        .Where(p => !string.IsNullOrEmpty(p.Ipv4Prefix))
                        .Select(p => p.Ipv4Prefix!));
                    _logger.LogInformation("Fetched {Count} GCP IP ranges", gcpData.Prefixes.Count);
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to fetch GCP IP ranges");
            }

            // Fetch Cloudflare IP ranges
            try
            {
                var cfIpv4 = await client.GetStringAsync(BotListSources.CloudflareIpv4, cancellationToken);
                ranges.AddRange(cfIpv4.Split('\n', StringSplitOptions.RemoveEmptyEntries));
                _logger.LogInformation("Fetched Cloudflare IPv4 ranges");
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to fetch Cloudflare IP ranges");
            }

            _cache.Set(cacheKey, ranges, CacheDuration);
            _logger.LogInformation("Total datacenter IP ranges: {Count}", ranges.Count);
            return ranges;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to fetch datacenter IP ranges, using fallback");
            return GetFallbackDatacenterRanges();
        }
    }

    public async Task<List<BotPattern>> GetMatomoBotPatternsAsync(CancellationToken cancellationToken = default)
    {
        const string cacheKey = "bot_list_matomo_patterns";

        if (_cache.TryGetValue<List<BotPattern>>(cacheKey, out var cached) && cached != null) return cached;

        try
        {
            var client = _httpClientFactory.CreateClient();
            client.Timeout = TimeSpan.FromSeconds(30);

            var yaml = await client.GetStringAsync(BotListSources.MatomoBotList, cancellationToken);
            var patterns = ParseMatomoYaml(yaml);

            _logger.LogInformation("Fetched {Count} Matomo bot patterns", patterns.Count);

            _cache.Set(cacheKey, patterns, CacheDuration);
            return patterns;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to fetch Matomo patterns, using fallback");
            return GetFallbackMatomoPatterns();
        }
    }

    private List<BotPattern> ParseMatomoYaml(string yaml)
    {
        // Simple YAML parser for the Matomo format
        // Format is: - regex: "pattern" / name: "BotName" / category: "Category"
        var patterns = new List<BotPattern>();
        var lines = yaml.Split('\n');

        BotPattern? currentPattern = null;

        foreach (var line in lines)
        {
            var trimmed = line.Trim();

            if (trimmed.StartsWith("- regex:"))
            {
                if (currentPattern != null) patterns.Add(currentPattern);

                currentPattern = new BotPattern
                {
                    Pattern = ExtractQuotedValue(trimmed, "regex:")
                };
            }
            else if (currentPattern != null)
            {
                if (trimmed.StartsWith("name:"))
                    currentPattern.Name = ExtractQuotedValue(trimmed, "name:");
                else if (trimmed.StartsWith("category:"))
                    currentPattern.Category = ExtractQuotedValue(trimmed, "category:");
                else if (trimmed.StartsWith("url:")) currentPattern.Url = ExtractQuotedValue(trimmed, "url:");
            }
        }

        if (currentPattern != null) patterns.Add(currentPattern);

        return patterns;
    }

    private string? ExtractQuotedValue(string line, string prefix)
    {
        var start = line.IndexOf(prefix) + prefix.Length;
        var text = line.Substring(start).Trim();

        // Remove quotes if present
        if (text.StartsWith('"') || text.StartsWith('\'')) text = text.Substring(1);
        if (text.EndsWith('"') || text.EndsWith('\'')) text = text.Substring(0, text.Length - 1);

        return string.IsNullOrEmpty(text) ? null : text;
    }

    private List<string> GetFallbackCrawlerPatterns()
    {
        // Fallback to embedded list if download fails
        return new List<string>(BotSignatures.MaliciousBotPatterns
            .Concat(BotSignatures.AutomationFrameworks));
    }

    private List<string> GetFallbackDatacenterRanges()
    {
        // Basic fallback ranges
        return new List<string>
        {
            "3.0.0.0/8", "13.0.0.0/8", "18.0.0.0/8", "52.0.0.0/8", // AWS
            "20.0.0.0/8", "40.0.0.0/8", "104.0.0.0/8", // Azure
            "34.0.0.0/8", "35.0.0.0/8" // GCP
        };
    }

    private List<BotPattern> GetFallbackMatomoPatterns()
    {
        return BotSignatures.GoodBots.Select(kvp => new BotPattern
        {
            Name = kvp.Value,
            Pattern = kvp.Key,
            Category = "Search Engine"
        }).ToList();
    }

    // JSON models for parsing responses
    private class CrawlerEntry
    {
        public string? Pattern { get; set; }
        public string? Url { get; set; }
    }

    private class AwsIpRanges
    {
        public List<AwsPrefix>? Prefixes { get; set; }
    }

    private class AwsPrefix
    {
        public string IpPrefix { get; } = "";
        public string Region { get; set; } = "";
        public string Service { get; set; } = "";
    }

    private class GcpIpRanges
    {
        public List<GcpPrefix>? Prefixes { get; set; }
    }

    private class GcpPrefix
    {
        public string? Ipv4Prefix { get; set; }
        public string? Ipv6Prefix { get; set; }
    }
}