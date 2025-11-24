using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Mostlylucid.ArchiveOrg.Config;

namespace Mostlylucid.ArchiveOrg.Services;

public class OllamaTagGenerator : IOllamaTagGenerator
{
    private readonly HttpClient _httpClient;
    private readonly ILogger<OllamaTagGenerator> _logger;
    private readonly OllamaOptions _options;

    public OllamaTagGenerator(
        HttpClient httpClient,
        IOptions<OllamaOptions> options,
        ILogger<OllamaTagGenerator> logger)
    {
        _httpClient = httpClient;
        _options = options.Value;
        _logger = logger;

        _httpClient.BaseAddress = new Uri(_options.BaseUrl);
        _httpClient.Timeout = TimeSpan.FromSeconds(_options.TimeoutSeconds);
    }

    public async Task<List<string>> GenerateTagsAsync(
        string title,
        string content,
        CancellationToken cancellationToken = default)
    {
        if (!_options.Enabled)
        {
            _logger.LogDebug("Ollama tag generation is disabled");
            return [];
        }

        try
        {
            // Truncate content to avoid overwhelming the model
            var truncatedContent = content.Length > 3000 ? content[..3000] + "..." : content;

            var prompt = $"""
                          Analyze the following blog post and generate {_options.MaxTags} relevant category tags.

                          Rules:
                          - Return ONLY a JSON array of strings, nothing else
                          - Tags should be short (1-3 words)
                          - Tags should be relevant to the technical content
                          - Use common programming/tech categories like: .NET, C#, ASP.NET, JavaScript, Docker, Database, API, Security, DevOps, Cloud, etc.
                          - Do not include generic tags like "Blog", "Post", "Article"

                          Title: {title}

                          Content:
                          {truncatedContent}

                          Return only the JSON array:
                          """;

            var request = new OllamaGenerateRequest
            {
                Model = _options.Model,
                Prompt = prompt,
                Stream = false,
                Options = new OllamaRequestOptions
                {
                    Temperature = _options.Temperature
                }
            };

            var json = JsonSerializer.Serialize(request);
            var httpContent = new StringContent(json, Encoding.UTF8, "application/json");

            _logger.LogDebug("Sending request to Ollama for tag generation");
            var response = await _httpClient.PostAsync("/api/generate", httpContent, cancellationToken);

            if (!response.IsSuccessStatusCode)
            {
                _logger.LogWarning("Ollama request failed: {StatusCode}", response.StatusCode);
                return [];
            }

            var responseJson = await response.Content.ReadAsStringAsync(cancellationToken);
            var ollamaResponse = JsonSerializer.Deserialize<OllamaGenerateResponse>(responseJson);

            if (string.IsNullOrEmpty(ollamaResponse?.Response))
            {
                _logger.LogWarning("Empty response from Ollama");
                return [];
            }

            // Parse the JSON array from the response
            var tags = ParseTagsFromResponse(ollamaResponse.Response);
            _logger.LogInformation("Generated tags: {Tags}", string.Join(", ", tags));

            return tags;
        }
        catch (TaskCanceledException)
        {
            _logger.LogWarning("Ollama request timed out");
            return [];
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error generating tags with Ollama");
            return [];
        }
    }

    private List<string> ParseTagsFromResponse(string response)
    {
        try
        {
            // Try to extract JSON array from the response
            var trimmed = response.Trim();

            // Find the start and end of the JSON array
            var startIndex = trimmed.IndexOf('[');
            var endIndex = trimmed.LastIndexOf(']');

            if (startIndex >= 0 && endIndex > startIndex)
            {
                var jsonArray = trimmed.Substring(startIndex, endIndex - startIndex + 1);
                var tags = JsonSerializer.Deserialize<List<string>>(jsonArray);

                if (tags != null)
                    // Clean up and validate tags
                    return tags
                        .Where(t => !string.IsNullOrWhiteSpace(t))
                        .Select(t => t.Trim())
                        .Where(t => t.Length >= 2 && t.Length <= 50)
                        .Take(_options.MaxTags)
                        .ToList();
            }

            _logger.LogWarning("Could not parse JSON array from Ollama response: {Response}", response);
        }
        catch (JsonException ex)
        {
            _logger.LogWarning(ex, "Failed to parse Ollama response as JSON: {Response}", response);
        }

        return [];
    }
}

internal class OllamaGenerateRequest
{
    [JsonPropertyName("model")] public string Model { get; set; } = string.Empty;

    [JsonPropertyName("prompt")] public string Prompt { get; set; } = string.Empty;

    [JsonPropertyName("stream")] public bool Stream { get; set; }

    [JsonPropertyName("options")] public OllamaRequestOptions? Options { get; set; }
}

internal class OllamaRequestOptions
{
    [JsonPropertyName("temperature")] public float Temperature { get; set; }
}

internal class OllamaGenerateResponse
{
    [JsonPropertyName("response")] public string Response { get; set; } = string.Empty;

    [JsonPropertyName("done")] public bool Done { get; set; }
}