using System.Diagnostics;
using System.Net.Http.Json;
using System.Text.Json.Serialization;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Mostlylucid.RagLlmSearch.Configuration;
using Mostlylucid.RagLlmSearch.Models;
using Mostlylucid.RagLlmSearch.Telemetry;

namespace Mostlylucid.RagLlmSearch.SearchProviders;

/// <summary>
///     Brave Search API provider (free tier: 2000 queries/month)
///     Get your API key at: https://brave.com/search/api/
/// </summary>
public class BraveSearchProvider : ISearchProvider
{
    private const string BaseUrl = "https://api.search.brave.com/res/v1/web/search";
    private readonly HttpClient _httpClient;
    private readonly ILogger<BraveSearchProvider> _logger;
    private readonly BraveSearchSettings _settings;

    public BraveSearchProvider(
        HttpClient httpClient,
        IOptions<SearchProviderOptions> options,
        ILogger<BraveSearchProvider> logger)
    {
        _httpClient = httpClient;
        _logger = logger;
        _settings = options.Value.Brave;
    }

    public string Name => "Brave";
    public bool IsAvailable => !string.IsNullOrEmpty(_settings.ApiKey);

    public async Task<SearchResponse> SearchAsync(string query, int maxResults = 5,
        CancellationToken cancellationToken = default)
    {
        using var activity = RagSearchTelemetry.StartSearchActivity(query, Name, maxResults);

        var stopwatch = Stopwatch.StartNew();
        var response = new SearchResponse
        {
            Query = query,
            Provider = Name
        };

        if (!IsAvailable)
        {
            response.Error = "Brave Search API key not configured";
            RagSearchTelemetry.RecordSearchResult(activity, response);
            return response;
        }

        try
        {
            _logger.LogDebug("Searching Brave for: {Query}", query);

            var encodedQuery = Uri.EscapeDataString(query);
            var url = $"{BaseUrl}?q={encodedQuery}&count={maxResults}&country={_settings.Country}";

            if (_settings.SafeSearch) url += "&safesearch=strict";

            var request = new HttpRequestMessage(HttpMethod.Get, url);
            request.Headers.Add("X-Subscription-Token", _settings.ApiKey);
            request.Headers.Add("Accept", "application/json");

            var httpResponse = await _httpClient.SendAsync(request, cancellationToken);
            httpResponse.EnsureSuccessStatusCode();

            var braveResponse = await httpResponse.Content.ReadFromJsonAsync<BraveSearchResponse>(cancellationToken);

            if (braveResponse?.Web?.Results != null)
            {
                foreach (var result in braveResponse.Web.Results.Take(maxResults))
                    response.Results.Add(new SearchResult
                    {
                        Title = result.Title ?? string.Empty,
                        Snippet = result.Description ?? string.Empty,
                        Url = result.Url ?? string.Empty,
                        Provider = Name,
                        Content = result.Description,
                        Metadata = new Dictionary<string, string>
                        {
                            ["age"] = result.Age ?? string.Empty,
                            ["language"] = result.Language ?? string.Empty
                        }
                    });

                response.TotalResults = braveResponse.Web.Results.Count;
            }

            // Add FAQ results if available
            if (braveResponse?.Faq?.Results != null)
                foreach (var faq in braveResponse.Faq.Results.Take(2))
                    response.Results.Add(new SearchResult
                    {
                        Title = faq.Question ?? "FAQ",
                        Snippet = faq.Answer ?? string.Empty,
                        Url = faq.Url ?? string.Empty,
                        Provider = Name,
                        Content = $"Q: {faq.Question}\nA: {faq.Answer}"
                    });

            stopwatch.Stop();
            response.SearchTimeMs = stopwatch.ElapsedMilliseconds;
            RagSearchTelemetry.RecordSearchResult(activity, response);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error searching Brave for: {Query}", query);
            response.Error = ex.Message;
            stopwatch.Stop();
            response.SearchTimeMs = stopwatch.ElapsedMilliseconds;
            RagSearchTelemetry.RecordException(activity, ex);
        }

        _logger.LogDebug("Brave search completed in {Time}ms with {Count} results",
            response.SearchTimeMs, response.Results.Count);

        return response;
    }
}

// Brave Search API response models
internal class BraveSearchResponse
{
    [JsonPropertyName("query")] public BraveQuery? Query { get; set; }

    [JsonPropertyName("web")] public BraveWebResults? Web { get; set; }

    [JsonPropertyName("faq")] public BraveFaqResults? Faq { get; set; }

    [JsonPropertyName("infobox")] public BraveInfobox? Infobox { get; set; }
}

internal class BraveQuery
{
    [JsonPropertyName("original")] public string? Original { get; set; }

    [JsonPropertyName("show_strict_warning")]
    public bool ShowStrictWarning { get; set; }
}

internal class BraveWebResults
{
    [JsonPropertyName("results")] public List<BraveWebResult>? Results { get; set; }
}

internal class BraveWebResult
{
    [JsonPropertyName("title")] public string? Title { get; set; }

    [JsonPropertyName("url")] public string? Url { get; set; }

    [JsonPropertyName("description")] public string? Description { get; set; }

    [JsonPropertyName("age")] public string? Age { get; set; }

    [JsonPropertyName("language")] public string? Language { get; set; }

    [JsonPropertyName("family_friendly")] public bool FamilyFriendly { get; set; }
}

internal class BraveFaqResults
{
    [JsonPropertyName("results")] public List<BraveFaqResult>? Results { get; set; }
}

internal class BraveFaqResult
{
    [JsonPropertyName("question")] public string? Question { get; set; }

    [JsonPropertyName("answer")] public string? Answer { get; set; }

    [JsonPropertyName("url")] public string? Url { get; set; }
}

internal class BraveInfobox
{
    [JsonPropertyName("type")] public string? Type { get; set; }

    [JsonPropertyName("position")] public int Position { get; set; }

    [JsonPropertyName("results")] public List<BraveInfoboxResult>? Results { get; set; }
}

internal class BraveInfoboxResult
{
    [JsonPropertyName("title")] public string? Title { get; set; }

    [JsonPropertyName("description")] public string? Description { get; set; }

    [JsonPropertyName("url")] public string? Url { get; set; }
}