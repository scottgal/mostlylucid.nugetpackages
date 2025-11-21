using System.Net.Http.Json;
using System.Text;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using mostlylucid.llmslidetranslator.Models;

namespace mostlylucid.llmslidetranslator.Services;

/// <summary>
///     Client for Ollama LLM API
/// </summary>
public class OllamaClient : IOllamaClient
{
    private readonly LlmSlideTranslatorConfig _config;
    private readonly HttpClient _httpClient;
    private readonly ILogger<OllamaClient> _logger;

    public OllamaClient(
        ILogger<OllamaClient> logger,
        HttpClient httpClient,
        IOptions<LlmSlideTranslatorConfig> config)
    {
        _logger = logger;
        _httpClient = httpClient;
        _config = config.Value;

        _httpClient.BaseAddress = new Uri(_config.Ollama.Endpoint);
        _httpClient.Timeout = TimeSpan.FromSeconds(_config.Ollama.TimeoutSeconds);
    }

    public async Task<string> TranslateWithContextAsync(
        TranslationContext context,
        CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Translating block {BlockId} with Ollama",
            context.CurrentBlock.BlockId);

        var prompt = BuildPrompt(context);

        var requestBody = new
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

        try
        {
            var response = await _httpClient.PostAsJsonAsync(
                "/api/generate",
                requestBody,
                cancellationToken);

            response.EnsureSuccessStatusCode();

            var result = await response.Content.ReadFromJsonAsync<OllamaResponse>(
                cancellationToken);

            if (result?.Response == null) throw new InvalidOperationException("Ollama returned null response");

            _logger.LogDebug("Ollama translation completed for block {BlockId}",
                context.CurrentBlock.BlockId);

            return result.Response.Trim();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error translating with Ollama for block {BlockId}",
                context.CurrentBlock.BlockId);
            throw;
        }
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
            _logger.LogWarning(ex, "Ollama service not available at {Endpoint}",
                _config.Ollama.Endpoint);
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
                cancellationToken);

            return result?.Models?.Select(m => m.Name).ToList() ?? new List<string>();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting models from Ollama");
            return new List<string>();
        }
    }

    private string BuildPrompt(TranslationContext context)
    {
        var sb = new StringBuilder();

        // System prompt
        var systemPrompt = context.SystemPrompt ?? GetDefaultSystemPrompt(context.CurrentBlock);
        sb.AppendLine(systemPrompt);
        sb.AppendLine();

        // Add context from previous block (sliding window)
        if (context.PreviousBlock != null && !string.IsNullOrEmpty(context.PreviousBlock.TranslatedText))
        {
            sb.AppendLine("Previous block from the same document:");
            sb.AppendLine("[CTX-PREV]");
            sb.AppendLine($"EN: {context.PreviousBlock.Text}");
            sb.AppendLine($"{context.PreviousBlock.TargetLanguage.ToUpper()}: {context.PreviousBlock.TranslatedText}");
            sb.AppendLine();
        }

        // Add RAG-retrieved similar blocks
        if (context.SimilarBlocks.Count > 0)
        {
            sb.AppendLine("Context from earlier in the same document (use the same terminology):");
            for (var i = 0; i < context.SimilarBlocks.Count; i++)
            {
                var similarBlock = context.SimilarBlocks[i];
                if (!string.IsNullOrEmpty(similarBlock.TranslatedText))
                {
                    sb.AppendLine($"[CTX-{i + 1}]");
                    sb.AppendLine($"EN: {similarBlock.Text}");
                    sb.AppendLine($"{similarBlock.TargetLanguage.ToUpper()}: {similarBlock.TranslatedText}");
                    sb.AppendLine();
                }
            }
        }

        // Additional context if provided
        if (!string.IsNullOrEmpty(context.AdditionalContext))
        {
            sb.AppendLine(context.AdditionalContext);
            sb.AppendLine();
        }

        // The actual text to translate
        sb.AppendLine("Translate the following text:");
        sb.AppendLine("<<<SOURCE");
        sb.AppendLine(context.CurrentBlock.Text);
        sb.AppendLine("SOURCE>>>");
        sb.AppendLine();
        sb.AppendLine($"Return only the {context.CurrentBlock.TargetLanguage.ToUpper()} translation:");

        return sb.ToString();
    }

    private string GetDefaultSystemPrompt(TranslationBlock block)
    {
        return $@"You are a professional translator for technical documentation.
Translate *only* the [SOURCE] text from {block.SourceLanguage.ToUpper()} to {block.TargetLanguage.ToUpper()}.

Rules:
- Do NOT translate code, URLs, or technical identifiers
- Preserve markdown formatting
- Use the same terminology as shown in the context blocks
- Maintain the same tone and style
- Return ONLY the translation, no explanations";
    }

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
}