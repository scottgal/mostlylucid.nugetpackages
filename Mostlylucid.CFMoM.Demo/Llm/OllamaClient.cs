using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Mostlylucid.CFMoM.Demo.Llm;

/// <summary>
///     HTTP client for Ollama LLM API.
/// </summary>
public sealed class OllamaClient : IOllamaClient
{
    private readonly HttpClient _httpClient;
    private readonly JsonSerializerOptions _jsonOptions;

    public OllamaClient(HttpClient httpClient)
    {
        _httpClient = httpClient;
        _jsonOptions = new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            PropertyNameCaseInsensitive = true,
            DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
        };
    }

    public async Task<string> GenerateAsync(
        string prompt,
        string model = "llama3.2:3b",
        string? systemPrompt = null,
        CancellationToken cancellationToken = default)
    {
        var request = new OllamaGenerateRequest
        {
            Model = model,
            Prompt = prompt,
            System = systemPrompt,
            Stream = false
        };

        try
        {
            var response = await _httpClient.PostAsJsonAsync(
                "api/generate",
                request,
                _jsonOptions,
                cancellationToken);

            response.EnsureSuccessStatusCode();

            var result = await response.Content.ReadFromJsonAsync<OllamaGenerateResponse>(
                _jsonOptions,
                cancellationToken);

            return result?.Response ?? string.Empty;
        }
        catch (Exception ex)
        {
            // Return empty on failure, let the caller handle fallback
            Console.Error.WriteLine($"Ollama error: {ex.Message}");
            return string.Empty;
        }
    }

    public async Task<T?> GenerateJsonAsync<T>(
        string prompt,
        string model = "llama3.2:3b",
        string? systemPrompt = null,
        CancellationToken cancellationToken = default) where T : class
    {
        var jsonSystemPrompt = $"""
            {systemPrompt ?? ""}

            IMPORTANT: You must respond with valid JSON only. No markdown, no explanation, just the JSON object.
            """;

        var response = await GenerateAsync(prompt, model, jsonSystemPrompt, cancellationToken);

        if (string.IsNullOrWhiteSpace(response))
            return null;

        try
        {
            // Try to extract JSON from the response (in case there's extra text)
            var jsonStart = response.IndexOf('{');
            var jsonEnd = response.LastIndexOf('}');

            if (jsonStart >= 0 && jsonEnd > jsonStart)
            {
                var jsonStr = response.Substring(jsonStart, jsonEnd - jsonStart + 1);
                return JsonSerializer.Deserialize<T>(jsonStr, _jsonOptions);
            }

            return JsonSerializer.Deserialize<T>(response, _jsonOptions);
        }
        catch (JsonException)
        {
            return null;
        }
    }

    public async Task<bool> IsAvailableAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            var response = await _httpClient.GetAsync("api/tags", cancellationToken);
            return response.IsSuccessStatusCode;
        }
        catch
        {
            return false;
        }
    }

    private sealed class OllamaGenerateRequest
    {
        public required string Model { get; init; }
        public required string Prompt { get; init; }
        public string? System { get; init; }
        public bool Stream { get; init; }
    }

    private sealed class OllamaGenerateResponse
    {
        public string? Response { get; init; }
        public bool Done { get; init; }
    }
}
