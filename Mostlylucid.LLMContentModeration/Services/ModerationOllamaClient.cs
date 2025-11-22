using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Mostlylucid.LLMContentModeration.Models;

namespace Mostlylucid.LLMContentModeration.Services;

/// <summary>
/// Ollama client for content moderation LLM calls
/// </summary>
public class ModerationOllamaClient : IModerationOllamaClient
{
    private readonly ModerationOptions _config;
    private readonly HttpClient _httpClient;
    private readonly ILogger<ModerationOllamaClient> _logger;

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    public ModerationOllamaClient(
        ILogger<ModerationOllamaClient> logger,
        HttpClient httpClient,
        IOptions<ModerationOptions> config)
    {
        _logger = logger;
        _httpClient = httpClient;
        _config = config.Value;

        _httpClient.BaseAddress = new Uri(_config.Ollama.Endpoint);
        _httpClient.Timeout = TimeSpan.FromSeconds(_config.Ollama.TimeoutSeconds);
    }

    public async Task<List<ContentFlag>> ClassifyContentAsync(
        string content,
        ContentClassificationOptions options,
        CancellationToken cancellationToken = default)
    {
        _logger.LogDebug("Classifying content ({Length} chars) with Ollama", content.Length);

        var prompt = BuildClassificationPrompt(content, options);
        var response = await SendPromptAsync(prompt, cancellationToken);

        return ParseClassificationResponse(response, options);
    }

    public async Task<List<PiiMatch>> EnhancePiiDetectionAsync(
        string content,
        List<PiiMatch> regexMatches,
        CancellationToken cancellationToken = default)
    {
        _logger.LogDebug("Enhancing PII detection with LLM for content ({Length} chars)", content.Length);

        var prompt = BuildPiiEnhancementPrompt(content, regexMatches);
        var response = await SendPromptAsync(prompt, cancellationToken);

        return ParsePiiEnhancementResponse(response, regexMatches);
    }

    public async Task<bool> IsAvailableAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            var response = await _httpClient.GetAsync("/api/tags", cancellationToken);
            return response.IsSuccessStatusCode;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Ollama service not available at {Endpoint}", _config.Ollama.Endpoint);
            return false;
        }
    }

    public async Task<List<string>> GetModelsAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            var response = await _httpClient.GetAsync("/api/tags", cancellationToken);
            response.EnsureSuccessStatusCode();

            var result = await response.Content.ReadFromJsonAsync<OllamaModelsResponse>(
                JsonOptions, cancellationToken);

            return result?.Models?.Select(m => m.Name).ToList() ?? [];
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting models from Ollama");
            return [];
        }
    }

    private async Task<string> SendPromptAsync(string prompt, CancellationToken cancellationToken)
    {
        var requestBody = new
        {
            model = _config.Ollama.Model,
            prompt,
            stream = false,
            format = "json",
            options = new
            {
                temperature = _config.Ollama.Temperature,
                num_predict = _config.Ollama.MaxTokens
            }
        };

        try
        {
            var response = await _httpClient.PostAsJsonAsync("/api/generate", requestBody, cancellationToken);
            response.EnsureSuccessStatusCode();

            var result = await response.Content.ReadFromJsonAsync<OllamaResponse>(JsonOptions, cancellationToken);

            if (result?.Response == null)
                throw new InvalidOperationException("Ollama returned null response");

            return result.Response.Trim();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error calling Ollama API");
            throw;
        }
    }

    private static string BuildClassificationPrompt(string content, ContentClassificationOptions options)
    {
        var categories = new List<string>();
        if (options.EnableToxicity) categories.Add("toxicity");
        if (options.EnableSpam) categories.Add("spam");
        if (options.EnableSelfHarm) categories.Add("self_harm");
        if (options.EnableNsfw) categories.Add("nsfw");

        return $$"""
            You are a content moderation assistant. Analyze the following text and classify it for: {{string.Join(", ", categories)}}.

            For each category, provide a confidence score from 0.0 to 1.0 and a brief explanation if flagged.

            Respond ONLY with valid JSON in this exact format:
            {
              "classifications": [
                {"category": "toxicity", "confidence": 0.0, "explanation": null},
                {"category": "spam", "confidence": 0.0, "explanation": null},
                {"category": "self_harm", "confidence": 0.0, "explanation": null},
                {"category": "nsfw", "confidence": 0.0, "explanation": null}
              ]
            }

            Content to analyze:
            ---
            {{content}}
            ---
            """;
    }

    private static string BuildPiiEnhancementPrompt(string content, List<PiiMatch> regexMatches)
    {
        var existingFindings = regexMatches.Any()
            ? $"Already detected: {string.Join(", ", regexMatches.Select(m => $"{m.Type}: {m.OriginalValue}"))}"
            : "No PII detected by regex patterns.";

        return $$"""
            You are a PII detection assistant. Review the following text for any personal identifiable information that may have been missed.

            Look for:
            - Email addresses
            - Phone numbers (any format/country)
            - Physical addresses
            - IBAN numbers
            - Credit card numbers
            - Social security numbers
            - Other sensitive personal data

            {{existingFindings}}

            Respond ONLY with valid JSON in this exact format:
            {
              "additional_pii": [
                {"type": "email|phone|address|iban|credit_card|ssn|other", "value": "the PII found", "confidence": 0.0}
              ]
            }

            If no additional PII is found, return: {"additional_pii": []}

            Content to analyze:
            ---
            {{content}}
            ---
            """;
    }

    private List<ContentFlag> ParseClassificationResponse(string response, ContentClassificationOptions options)
    {
        var flags = new List<ContentFlag>();

        try
        {
            var result = JsonSerializer.Deserialize<ClassificationResponse>(response, JsonOptions);

            if (result?.Classifications == null)
                return flags;

            foreach (var classification in result.Classifications)
            {
                var category = classification.Category?.ToLowerInvariant() switch
                {
                    "toxicity" => ContentCategory.Toxicity,
                    "spam" => ContentCategory.Spam,
                    "self_harm" => ContentCategory.SelfHarm,
                    "nsfw" => ContentCategory.Nsfw,
                    _ => (ContentCategory?)null
                };

                if (category == null) continue;

                var threshold = category.Value switch
                {
                    ContentCategory.Toxicity => options.ToxicityThreshold,
                    ContentCategory.Spam => options.SpamThreshold,
                    ContentCategory.SelfHarm => options.SelfHarmThreshold,
                    ContentCategory.Nsfw => options.NsfwThreshold,
                    _ => 0.7f
                };

                if (classification.Confidence >= threshold)
                {
                    flags.Add(new ContentFlag
                    {
                        Category = category.Value,
                        Confidence = classification.Confidence,
                        Threshold = threshold,
                        Explanation = classification.Explanation
                    });
                }
            }
        }
        catch (JsonException ex)
        {
            _logger.LogWarning(ex, "Failed to parse classification response: {Response}", response);
        }

        return flags;
    }

    private List<PiiMatch> ParsePiiEnhancementResponse(string response, List<PiiMatch> existingMatches)
    {
        var allMatches = new List<PiiMatch>(existingMatches);

        try
        {
            var result = JsonSerializer.Deserialize<PiiEnhancementResponse>(response, JsonOptions);

            if (result?.AdditionalPii == null)
                return allMatches;

            foreach (var pii in result.AdditionalPii)
            {
                var type = pii.Type?.ToLowerInvariant() switch
                {
                    "email" => PiiType.Email,
                    "phone" => PiiType.Phone,
                    "address" => PiiType.Address,
                    "iban" => PiiType.Iban,
                    "credit_card" => PiiType.CreditCard,
                    "ssn" => PiiType.SocialSecurityNumber,
                    _ => PiiType.Other
                };

                // Skip if we already found this exact value
                if (existingMatches.Any(m => m.OriginalValue == pii.Value))
                    continue;

                allMatches.Add(new PiiMatch
                {
                    Type = type,
                    OriginalValue = pii.Value ?? string.Empty,
                    Confidence = pii.Confidence
                });
            }
        }
        catch (JsonException ex)
        {
            _logger.LogWarning(ex, "Failed to parse PII enhancement response: {Response}", response);
        }

        return allMatches;
    }

    #region Response DTOs

    private class OllamaResponse
    {
        public string? Response { get; set; }
    }

    private class OllamaModelsResponse
    {
        public List<OllamaModel>? Models { get; set; }
    }

    private class OllamaModel
    {
        public required string Name { get; set; }
    }

    private class ClassificationResponse
    {
        public List<ClassificationItem>? Classifications { get; set; }
    }

    private class ClassificationItem
    {
        public string? Category { get; set; }
        public float Confidence { get; set; }
        public string? Explanation { get; set; }
    }

    private class PiiEnhancementResponse
    {
        [JsonPropertyName("additional_pii")]
        public List<PiiItem>? AdditionalPii { get; set; }
    }

    private class PiiItem
    {
        public string? Type { get; set; }
        public string? Value { get; set; }
        public float Confidence { get; set; }
    }

    #endregion
}
