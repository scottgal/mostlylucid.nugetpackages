using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Mostlylucid.LlmI18nAssistant.Models;

namespace Mostlylucid.LlmI18nAssistant.Services;

/// <summary>
///     Client for interacting with Ollama LLM
/// </summary>
public class OllamaClient : IOllamaClient
{
    private readonly LlmI18nAssistantConfig _config;
    private readonly HttpClient _httpClient;
    private readonly ILogger<OllamaClient> _logger;

    public OllamaClient(
        ILogger<OllamaClient> logger,
        HttpClient httpClient,
        IOptions<LlmI18nAssistantConfig> config)
    {
        _logger = logger;
        _httpClient = httpClient;
        _config = config.Value;

        _httpClient.BaseAddress = new Uri(_config.Ollama.Endpoint);
        _httpClient.Timeout = TimeSpan.FromSeconds(_config.Ollama.TimeoutSeconds);
    }

    /// <inheritdoc />
    public async Task<string> TranslateAsync(
        string sourceText,
        string sourceLanguage,
        string targetLanguage,
        string? nmtBaseline = null,
        List<ContextEntry>? contextEntries = null,
        string? additionalContext = null,
        CancellationToken cancellationToken = default)
    {
        var prompt = BuildTranslationPrompt(
            sourceText, sourceLanguage, targetLanguage,
            nmtBaseline, contextEntries, additionalContext);

        var request = new
        {
            model = _config.Ollama.Model,
            prompt,
            stream = false,
            options = new
            {
                temperature = _config.Ollama.Temperature,
                num_predict = _config.Ollama.MaxTokens
            }
        };

        _logger.LogDebug("Sending translation request to Ollama: {Text}",
            sourceText.Length > 50 ? sourceText[..50] + "..." : sourceText);

        try
        {
            var response = await _httpClient.PostAsJsonAsync("/api/generate", request, cancellationToken);
            response.EnsureSuccessStatusCode();

            var result = await response.Content.ReadFromJsonAsync<OllamaResponse>(cancellationToken: cancellationToken);

            if (result?.Response == null)
                throw new InvalidOperationException("Empty response from Ollama");

            var translation = ExtractTranslation(result.Response);

            _logger.LogDebug("Received translation: {Translation}",
                translation.Length > 50 ? translation[..50] + "..." : translation);

            return translation;
        }
        catch (HttpRequestException ex)
        {
            _logger.LogError(ex, "HTTP error communicating with Ollama");
            throw new InvalidOperationException($"Failed to communicate with Ollama: {ex.Message}", ex);
        }
        catch (TaskCanceledException ex) when (ex.InnerException is TimeoutException)
        {
            _logger.LogError("Ollama request timed out");
            throw new TimeoutException("Ollama request timed out", ex);
        }
    }

    /// <inheritdoc />
    public async Task<bool> IsAvailableAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            var response = await _httpClient.GetAsync("/api/tags", cancellationToken);
            return response.IsSuccessStatusCode;
        }
        catch
        {
            return false;
        }
    }

    /// <inheritdoc />
    public async Task<List<string>> GetModelsAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            var response = await _httpClient.GetAsync("/api/tags", cancellationToken);
            response.EnsureSuccessStatusCode();

            var json = await response.Content.ReadAsStringAsync(cancellationToken);
            var doc = JsonDocument.Parse(json);

            var models = new List<string>();
            if (doc.RootElement.TryGetProperty("models", out var modelsArray))
                foreach (var model in modelsArray.EnumerateArray())
                    if (model.TryGetProperty("name", out var name))
                        models.Add(name.GetString() ?? "");

            return models;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to get Ollama models");
            return [];
        }
    }

    private string BuildTranslationPrompt(
        string sourceText,
        string sourceLanguage,
        string targetLanguage,
        string? nmtBaseline,
        List<ContextEntry>? contextEntries,
        string? additionalContext)
    {
        var sb = new StringBuilder();

        sb.AppendLine($"You are a professional translator. Translate the following text from {sourceLanguage} to {targetLanguage}.");
        sb.AppendLine("IMPORTANT: Only output the translation, nothing else. Do not include explanations or notes.");
        sb.AppendLine("Maintain the original formatting, including any placeholders like {0}, {{name}}, or HTML tags.");

        if (!string.IsNullOrEmpty(additionalContext))
        {
            sb.AppendLine();
            sb.AppendLine($"Context: {additionalContext}");
        }

        if (contextEntries is { Count: > 0 })
        {
            sb.AppendLine();
            sb.AppendLine("Use these consistent translations for similar terms:");
            foreach (var entry in contextEntries)
                sb.AppendLine($"- \"{entry.SourceText}\" → \"{entry.TranslatedText}\"");
        }

        if (!string.IsNullOrEmpty(nmtBaseline))
        {
            sb.AppendLine();
            sb.AppendLine("Here is a machine translation baseline. Improve it for naturalness and accuracy:");
            sb.AppendLine($"Machine translation: {nmtBaseline}");
        }

        sb.AppendLine();
        sb.AppendLine($"Text to translate: {sourceText}");
        sb.AppendLine();
        sb.AppendLine("Translation:");

        return sb.ToString();
    }

    private static string ExtractTranslation(string response)
    {
        // Clean up the response - remove any leading/trailing whitespace
        var translation = response.Trim();

        // Remove common prefixes that LLMs sometimes add
        var prefixes = new[]
        {
            "Translation:", "Here is the translation:", "The translation is:",
            "Translated text:", "Here's the translation:"
        };

        foreach (var prefix in prefixes)
            if (translation.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
            {
                translation = translation[prefix.Length..].TrimStart();
                break;
            }

        // Remove quotes if the entire response is quoted
        if (translation.StartsWith('"') && translation.EndsWith('"'))
            translation = translation[1..^1];

        return translation;
    }

    private class OllamaResponse
    {
        public string? Model { get; set; }
        public string? Response { get; set; }
        public bool Done { get; set; }
    }
}
