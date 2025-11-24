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
///     SerpApi search provider (free tier: 100 searches/month)
///     Get your API key at: https://serpapi.com/
/// </summary>
public class SerpApiSearchProvider : ISearchProvider
{
    private const string BaseUrl = "https://serpapi.com/search.json";
    private readonly HttpClient _httpClient;
    private readonly ILogger<SerpApiSearchProvider> _logger;
    private readonly SerpApiSettings _settings;

    public SerpApiSearchProvider(
        HttpClient httpClient,
        IOptions<SearchProviderOptions> options,
        ILogger<SerpApiSearchProvider> logger)
    {
        _httpClient = httpClient;
        _logger = logger;
        _settings = options.Value.SerpApi;
    }

    public string Name => "SerpApi";
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
            response.Error = "SerpApi API key not configured";
            RagSearchTelemetry.RecordSearchResult(activity, response);
            return response;
        }

        try
        {
            _logger.LogDebug("Searching SerpApi for: {Query}", query);

            var encodedQuery = Uri.EscapeDataString(query);
            var encodedLocation = Uri.EscapeDataString(_settings.Location);
            var url =
                $"{BaseUrl}?engine={_settings.Engine}&q={encodedQuery}&location={encodedLocation}&api_key={_settings.ApiKey}&num={maxResults}";

            var httpResponse = await _httpClient.GetAsync(url, cancellationToken);
            httpResponse.EnsureSuccessStatusCode();

            var serpResponse = await httpResponse.Content.ReadFromJsonAsync<SerpApiResponse>(cancellationToken);

            if (serpResponse != null)
            {
                // Add knowledge graph if available
                if (serpResponse.KnowledgeGraph != null)
                {
                    var kg = serpResponse.KnowledgeGraph;
                    var kgContent = kg.Description ?? string.Empty;

                    if (kg.Attributes != null && kg.Attributes.Count > 0)
                        kgContent += "\n\n" + string.Join("\n", kg.Attributes.Select(a => $"{a.Key}: {a.Value}"));

                    response.Results.Add(new SearchResult
                    {
                        Title = kg.Title ?? "Knowledge Graph",
                        Snippet = kg.Description ?? string.Empty,
                        Url = kg.Website ?? string.Empty,
                        Provider = Name,
                        Content = kgContent,
                        Score = 1.0f
                    });
                }

                // Add answer box if available
                if (serpResponse.AnswerBox != null)
                    response.Results.Add(new SearchResult
                    {
                        Title = serpResponse.AnswerBox.Title ?? "Answer",
                        Snippet = serpResponse.AnswerBox.Snippet ?? serpResponse.AnswerBox.Answer ?? string.Empty,
                        Url = serpResponse.AnswerBox.Link ?? string.Empty,
                        Provider = Name,
                        Content = serpResponse.AnswerBox.Snippet ?? serpResponse.AnswerBox.Answer
                    });

                // Add organic results
                if (serpResponse.OrganicResults != null)
                {
                    foreach (var result in serpResponse.OrganicResults.Take(maxResults))
                        response.Results.Add(new SearchResult
                        {
                            Title = result.Title ?? string.Empty,
                            Snippet = result.Snippet ?? string.Empty,
                            Url = result.Link ?? string.Empty,
                            Provider = Name,
                            Content = result.Snippet,
                            Metadata = new Dictionary<string, string>
                            {
                                ["position"] = result.Position.ToString(),
                                ["date"] = result.Date ?? string.Empty
                            }
                        });

                    response.TotalResults = serpResponse.OrganicResults.Count;
                }
            }

            stopwatch.Stop();
            response.SearchTimeMs = stopwatch.ElapsedMilliseconds;
            RagSearchTelemetry.RecordSearchResult(activity, response);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error searching SerpApi for: {Query}", query);
            response.Error = ex.Message;
            stopwatch.Stop();
            response.SearchTimeMs = stopwatch.ElapsedMilliseconds;
            RagSearchTelemetry.RecordException(activity, ex);
        }

        _logger.LogDebug("SerpApi search completed in {Time}ms with {Count} results",
            response.SearchTimeMs, response.Results.Count);

        return response;
    }
}

// SerpApi response models
internal class SerpApiResponse
{
    [JsonPropertyName("search_metadata")] public SerpApiMetadata? SearchMetadata { get; set; }

    [JsonPropertyName("organic_results")] public List<SerpApiOrganicResult>? OrganicResults { get; set; }

    [JsonPropertyName("knowledge_graph")] public SerpApiKnowledgeGraph? KnowledgeGraph { get; set; }

    [JsonPropertyName("answer_box")] public SerpApiAnswerBox? AnswerBox { get; set; }

    [JsonPropertyName("related_questions")]
    public List<SerpApiRelatedQuestion>? RelatedQuestions { get; set; }
}

internal class SerpApiMetadata
{
    [JsonPropertyName("status")] public string? Status { get; set; }

    [JsonPropertyName("total_time_taken")] public float TotalTimeTaken { get; set; }
}

internal class SerpApiOrganicResult
{
    [JsonPropertyName("position")] public int Position { get; set; }

    [JsonPropertyName("title")] public string? Title { get; set; }

    [JsonPropertyName("link")] public string? Link { get; set; }

    [JsonPropertyName("snippet")] public string? Snippet { get; set; }

    [JsonPropertyName("date")] public string? Date { get; set; }

    [JsonPropertyName("displayed_link")] public string? DisplayedLink { get; set; }
}

internal class SerpApiKnowledgeGraph
{
    [JsonPropertyName("title")] public string? Title { get; set; }

    [JsonPropertyName("type")] public string? Type { get; set; }

    [JsonPropertyName("description")] public string? Description { get; set; }

    [JsonPropertyName("website")] public string? Website { get; set; }

    [JsonPropertyName("kgmid")] public string? Kgmid { get; set; }

    [JsonExtensionData] public Dictionary<string, object>? Attributes { get; set; }
}

internal class SerpApiAnswerBox
{
    [JsonPropertyName("type")] public string? Type { get; set; }

    [JsonPropertyName("title")] public string? Title { get; set; }

    [JsonPropertyName("answer")] public string? Answer { get; set; }

    [JsonPropertyName("snippet")] public string? Snippet { get; set; }

    [JsonPropertyName("link")] public string? Link { get; set; }
}

internal class SerpApiRelatedQuestion
{
    [JsonPropertyName("question")] public string? Question { get; set; }

    [JsonPropertyName("snippet")] public string? Snippet { get; set; }

    [JsonPropertyName("title")] public string? Title { get; set; }

    [JsonPropertyName("link")] public string? Link { get; set; }
}