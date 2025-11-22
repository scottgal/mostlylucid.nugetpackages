using System.Diagnostics;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Mostlylucid.RagLlmSearch.Configuration;
using Mostlylucid.RagLlmSearch.Models;

namespace Mostlylucid.RagLlmSearch.SearchProviders;

/// <summary>
/// DuckDuckGo Instant Answer API search provider (free, no API key required)
/// </summary>
public class DuckDuckGoSearchProvider : ISearchProvider
{
    private readonly HttpClient _httpClient;
    private readonly ILogger<DuckDuckGoSearchProvider> _logger;
    private readonly DuckDuckGoSettings _settings;
    private const string BaseUrl = "https://api.duckduckgo.com/";

    public DuckDuckGoSearchProvider(
        HttpClient httpClient,
        IOptions<SearchProviderOptions> options,
        ILogger<DuckDuckGoSearchProvider> logger)
    {
        _httpClient = httpClient;
        _logger = logger;
        _settings = options.Value.DuckDuckGo;
    }

    public string Name => "DuckDuckGo";
    public bool IsAvailable => true; // No API key required

    public async Task<SearchResponse> SearchAsync(string query, int maxResults = 5, CancellationToken cancellationToken = default)
    {
        var stopwatch = Stopwatch.StartNew();
        var response = new SearchResponse
        {
            Query = query,
            Provider = Name
        };

        try
        {
            _logger.LogDebug("Searching DuckDuckGo for: {Query}", query);

            var encodedQuery = Uri.EscapeDataString(query);
            var url = $"{BaseUrl}?q={encodedQuery}&format=json&no_html=1&skip_disambig=1";

            var httpResponse = await _httpClient.GetAsync(url, cancellationToken);
            httpResponse.EnsureSuccessStatusCode();

            var ddgResponse = await httpResponse.Content.ReadFromJsonAsync<DuckDuckGoResponse>(cancellationToken: cancellationToken);

            if (ddgResponse != null)
            {
                // Add abstract if available
                if (!string.IsNullOrEmpty(ddgResponse.Abstract))
                {
                    response.Results.Add(new SearchResult
                    {
                        Title = ddgResponse.Heading ?? "Summary",
                        Snippet = ddgResponse.Abstract,
                        Url = ddgResponse.AbstractUrl ?? string.Empty,
                        Provider = Name,
                        Content = ddgResponse.Abstract
                    });
                }

                // Add related topics
                if (ddgResponse.RelatedTopics != null)
                {
                    foreach (var topic in ddgResponse.RelatedTopics.Take(maxResults - response.Results.Count))
                    {
                        if (!string.IsNullOrEmpty(topic.Text))
                        {
                            response.Results.Add(new SearchResult
                            {
                                Title = ExtractTitle(topic.Text),
                                Snippet = topic.Text,
                                Url = topic.FirstUrl ?? string.Empty,
                                Provider = Name
                            });
                        }
                    }
                }

                // Add results from Answer
                if (!string.IsNullOrEmpty(ddgResponse.Answer))
                {
                    response.Results.Insert(0, new SearchResult
                    {
                        Title = "Direct Answer",
                        Snippet = ddgResponse.Answer,
                        Url = ddgResponse.AnswerType ?? string.Empty,
                        Provider = Name,
                        Content = ddgResponse.Answer
                    });
                }

                // Add Infobox data if available
                if (ddgResponse.Infobox?.Content != null)
                {
                    var infoContent = string.Join("\n", ddgResponse.Infobox.Content
                        .Where(c => !string.IsNullOrEmpty(c.Value))
                        .Select(c => $"{c.Label}: {c.Value}"));

                    if (!string.IsNullOrEmpty(infoContent))
                    {
                        response.Results.Add(new SearchResult
                        {
                            Title = "Quick Facts",
                            Snippet = infoContent,
                            Url = ddgResponse.AbstractUrl ?? string.Empty,
                            Provider = Name,
                            Content = infoContent
                        });
                    }
                }

                response.TotalResults = response.Results.Count;
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error searching DuckDuckGo for: {Query}", query);
            response.Error = ex.Message;
        }

        stopwatch.Stop();
        response.SearchTimeMs = stopwatch.ElapsedMilliseconds;

        _logger.LogDebug("DuckDuckGo search completed in {Time}ms with {Count} results",
            response.SearchTimeMs, response.Results.Count);

        return response;
    }

    private static string ExtractTitle(string text)
    {
        // Try to extract title from text (usually first sentence or phrase before " - ")
        var dashIndex = text.IndexOf(" - ", StringComparison.Ordinal);
        if (dashIndex > 0 && dashIndex < 100)
        {
            return text[..dashIndex];
        }

        // Return first 50 characters
        return text.Length > 50 ? text[..50] + "..." : text;
    }
}

// DuckDuckGo API response models
internal class DuckDuckGoResponse
{
    [JsonPropertyName("Abstract")]
    public string? Abstract { get; set; }

    [JsonPropertyName("AbstractText")]
    public string? AbstractText { get; set; }

    [JsonPropertyName("AbstractSource")]
    public string? AbstractSource { get; set; }

    [JsonPropertyName("AbstractURL")]
    public string? AbstractUrl { get; set; }

    [JsonPropertyName("Heading")]
    public string? Heading { get; set; }

    [JsonPropertyName("Answer")]
    public string? Answer { get; set; }

    [JsonPropertyName("AnswerType")]
    public string? AnswerType { get; set; }

    [JsonPropertyName("Definition")]
    public string? Definition { get; set; }

    [JsonPropertyName("DefinitionSource")]
    public string? DefinitionSource { get; set; }

    [JsonPropertyName("DefinitionURL")]
    public string? DefinitionUrl { get; set; }

    [JsonPropertyName("RelatedTopics")]
    public List<DuckDuckGoTopic>? RelatedTopics { get; set; }

    [JsonPropertyName("Results")]
    public List<DuckDuckGoTopic>? Results { get; set; }

    [JsonPropertyName("Infobox")]
    public DuckDuckGoInfobox? Infobox { get; set; }
}

internal class DuckDuckGoTopic
{
    [JsonPropertyName("Text")]
    public string? Text { get; set; }

    [JsonPropertyName("FirstURL")]
    public string? FirstUrl { get; set; }

    [JsonPropertyName("Icon")]
    public DuckDuckGoIcon? Icon { get; set; }

    [JsonPropertyName("Result")]
    public string? Result { get; set; }
}

internal class DuckDuckGoIcon
{
    [JsonPropertyName("URL")]
    public string? Url { get; set; }

    [JsonPropertyName("Height")]
    public string? Height { get; set; }

    [JsonPropertyName("Width")]
    public string? Width { get; set; }
}

internal class DuckDuckGoInfobox
{
    [JsonPropertyName("content")]
    public List<DuckDuckGoInfoboxContent>? Content { get; set; }

    [JsonPropertyName("meta")]
    public List<DuckDuckGoInfoboxMeta>? Meta { get; set; }
}

internal class DuckDuckGoInfoboxContent
{
    [JsonPropertyName("data_type")]
    public string? DataType { get; set; }

    [JsonPropertyName("value")]
    public string? Value { get; set; }

    [JsonPropertyName("label")]
    public string? Label { get; set; }
}

internal class DuckDuckGoInfoboxMeta
{
    [JsonPropertyName("data_type")]
    public string? DataType { get; set; }

    [JsonPropertyName("value")]
    public string? Value { get; set; }

    [JsonPropertyName("label")]
    public string? Label { get; set; }
}
