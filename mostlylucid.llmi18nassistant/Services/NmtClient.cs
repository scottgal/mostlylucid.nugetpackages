using System.Net.Http.Json;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Mostlylucid.LlmI18nAssistant.Models;

namespace Mostlylucid.LlmI18nAssistant.Services;

/// <summary>
///     Client for Neural Machine Translation service
/// </summary>
public class NmtClient : INmtClient
{
    private readonly LlmI18nAssistantConfig _config;
    private readonly HttpClient _httpClient;
    private readonly ILogger<NmtClient> _logger;

    public NmtClient(
        ILogger<NmtClient> logger,
        HttpClient httpClient,
        IOptions<LlmI18nAssistantConfig> config)
    {
        _logger = logger;
        _httpClient = httpClient;
        _config = config.Value;

        // Use first endpoint as default
        if (_config.Nmt.ServiceEndpoints.Count > 0)
            _httpClient.BaseAddress = new Uri(_config.Nmt.ServiceEndpoints[0]);

        _httpClient.Timeout = TimeSpan.FromSeconds(30);
    }

    /// <inheritdoc />
    public async Task<string?> TranslateAsync(
        string text,
        string sourceLanguage,
        string targetLanguage,
        CancellationToken cancellationToken = default)
    {
        if (!_config.Nmt.Enabled)
            return null;

        try
        {
            var request = new
            {
                q = text,
                source = sourceLanguage,
                target = targetLanguage
            };

            var response = await _httpClient.PostAsJsonAsync("/translate", request, cancellationToken);

            if (!response.IsSuccessStatusCode)
            {
                _logger.LogWarning("NMT service returned {StatusCode}", response.StatusCode);
                return null;
            }

            var result = await response.Content.ReadFromJsonAsync<NmtResponse>(cancellationToken);
            return result?.TranslatedText;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "NMT translation failed");
            return null;
        }
    }

    /// <inheritdoc />
    public async Task<List<string>> TranslateBatchAsync(
        List<string> texts,
        string sourceLanguage,
        string targetLanguage,
        CancellationToken cancellationToken = default)
    {
        if (!_config.Nmt.Enabled)
            return texts;

        var results = new List<string>();

        foreach (var text in texts)
        {
            var translated = await TranslateAsync(text, sourceLanguage, targetLanguage, cancellationToken);
            results.Add(translated ?? text);
        }

        return results;
    }

    /// <inheritdoc />
    public async Task<bool> IsAvailableAsync(CancellationToken cancellationToken = default)
    {
        if (!_config.Nmt.Enabled)
            return false;

        try
        {
            var response = await _httpClient.GetAsync("/", cancellationToken);
            return response.IsSuccessStatusCode;
        }
        catch
        {
            return false;
        }
    }

    private class NmtResponse
    {
        public string? TranslatedText { get; set; }
    }
}