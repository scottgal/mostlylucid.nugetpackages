using System.Diagnostics;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Mostlylucid.RagLlmSearch.Configuration;
using Mostlylucid.RagLlmSearch.Models;
using Mostlylucid.RagLlmSearch.Telemetry;

namespace Mostlylucid.RagLlmSearch.SearchProviders;

/// <summary>
/// Tavily AI Search API provider (optimized for AI/RAG use cases)
/// Get your API key at: https://tavily.com/
/// </summary>
public class TavilySearchProvider : ISearchProvider
{
    private readonly HttpClient _httpClient;
    private readonly ILogger<TavilySearchProvider> _logger;
    private readonly TavilySettings _settings;
    private const string BaseUrl = "https://api.tavily.com/search";

    public TavilySearchProvider(
        HttpClient httpClient,
        IOptions<SearchProviderOptions> options,
        ILogger<TavilySearchProvider> logger)
    {
        _httpClient = httpClient;
        _logger = logger;
        _settings = options.Value.Tavily;
    }

    public string Name => "Tavily";
    public bool IsAvailable => !string.IsNullOrEmpty(_settings.ApiKey);

    public async Task<SearchResponse> SearchAsync(string query, int maxResults = 5, CancellationToken cancellationToken = default)
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
            response.Error = "Tavily API key not configured";
            RagSearchTelemetry.RecordSearchResult(activity, response);
            return response;
        }

        try
        {
            _logger.LogDebug("Searching Tavily for: {Query}", query);

            var requestBody = new TavilySearchRequest
            {
                ApiKey = _settings.ApiKey!,
                Query = query,
                SearchDepth = _settings.SearchDepth,
                IncludeAnswer = _settings.IncludeAnswer,
                MaxResults = maxResults
            };

            var json = JsonSerializer.Serialize(requestBody);
            var content = new StringContent(json, Encoding.UTF8, "application/json");

            var httpResponse = await _httpClient.PostAsync(BaseUrl, content, cancellationToken);
            httpResponse.EnsureSuccessStatusCode();

            var tavilyResponse = await httpResponse.Content.ReadFromJsonAsync<TavilySearchResponse>(cancellationToken: cancellationToken);

            if (tavilyResponse != null)
            {
                // Add the AI-generated answer if available
                if (!string.IsNullOrEmpty(tavilyResponse.Answer))
                {
                    response.Results.Add(new SearchResult
                    {
                        Title = "AI Summary",
                        Snippet = tavilyResponse.Answer,
                        Url = string.Empty,
                        Provider = Name,
                        Content = tavilyResponse.Answer,
                        Score = 1.0f
                    });
                }

                // Add search results
                if (tavilyResponse.Results != null)
                {
                    foreach (var result in tavilyResponse.Results.Take(maxResults))
                    {
                        response.Results.Add(new SearchResult
                        {
                            Title = result.Title ?? string.Empty,
                            Snippet = result.Content ?? string.Empty,
                            Url = result.Url ?? string.Empty,
                            Provider = Name,
                            Content = result.Content,
                            Score = result.Score
                        });
                    }

                    response.TotalResults = tavilyResponse.Results.Count;
                }
            }

            stopwatch.Stop();
            response.SearchTimeMs = stopwatch.ElapsedMilliseconds;
            RagSearchTelemetry.RecordSearchResult(activity, response);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error searching Tavily for: {Query}", query);
            response.Error = ex.Message;
            stopwatch.Stop();
            response.SearchTimeMs = stopwatch.ElapsedMilliseconds;
            RagSearchTelemetry.RecordException(activity, ex);
        }

        _logger.LogDebug("Tavily search completed in {Time}ms with {Count} results",
            response.SearchTimeMs, response.Results.Count);

        return response;
    }
}

// Tavily API request/response models
internal class TavilySearchRequest
{
    [JsonPropertyName("api_key")]
    public string ApiKey { get; set; } = string.Empty;

    [JsonPropertyName("query")]
    public string Query { get; set; } = string.Empty;

    [JsonPropertyName("search_depth")]
    public string SearchDepth { get; set; } = "basic";

    [JsonPropertyName("include_answer")]
    public bool IncludeAnswer { get; set; } = true;

    [JsonPropertyName("include_images")]
    public bool IncludeImages { get; set; } = false;

    [JsonPropertyName("max_results")]
    public int MaxResults { get; set; } = 5;
}

internal class TavilySearchResponse
{
    [JsonPropertyName("query")]
    public string? Query { get; set; }

    [JsonPropertyName("answer")]
    public string? Answer { get; set; }

    [JsonPropertyName("response_time")]
    public float ResponseTime { get; set; }

    [JsonPropertyName("results")]
    public List<TavilyResult>? Results { get; set; }
}

internal class TavilyResult
{
    [JsonPropertyName("title")]
    public string? Title { get; set; }

    [JsonPropertyName("url")]
    public string? Url { get; set; }

    [JsonPropertyName("content")]
    public string? Content { get; set; }

    [JsonPropertyName("score")]
    public float Score { get; set; }
}
