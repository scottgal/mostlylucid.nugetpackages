using System.Net.Http.Json;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using mostlylucid.llmslidetranslator.Models;

namespace mostlylucid.llmslidetranslator.Services;

/// <summary>
///     Client for NMT (Neural Machine Translation) service
///     Based on https://github.com/scottgal/mostlyucid-nmt (Opus-MT)
/// </summary>
public class NmtClient : INmtClient
{
    private readonly LlmSlideTranslatorConfig _config;
    private readonly object _endpointLock = new();
    private readonly HttpClient _httpClient;
    private readonly ILogger<NmtClient> _logger;
    private int _currentEndpointIndex;

    public NmtClient(
        ILogger<NmtClient> logger,
        HttpClient httpClient,
        IOptions<LlmSlideTranslatorConfig> config)
    {
        _logger = logger;
        _httpClient = httpClient;
        _config = config.Value;
    }

    public async Task<string> TranslateAsync(
        string text,
        string sourceLanguage,
        string targetLanguage,
        CancellationToken cancellationToken = default)
    {
        if (!_config.Nmt.Enabled) throw new InvalidOperationException("NMT is not enabled in configuration");

        _logger.LogDebug("Translating text from {Source} to {Target} using NMT",
            sourceLanguage, targetLanguage);

        var endpoint = GetNextEndpoint();

        var requestBody = new
        {
            text,
            source_lang = sourceLanguage,
            target_lang = targetLanguage
        };

        try
        {
            var response = await _httpClient.PostAsJsonAsync(
                $"{endpoint}/translate",
                requestBody,
                cancellationToken);

            response.EnsureSuccessStatusCode();

            var result = await response.Content.ReadFromJsonAsync<NmtResponse>(
                cancellationToken);

            if (result?.TranslatedText == null) throw new InvalidOperationException("NMT returned null translation");

            return result.TranslatedText;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error translating with NMT at endpoint {Endpoint}", endpoint);
            throw;
        }
    }

    public async Task<List<TranslationBlock>> TranslateBatchAsync(
        List<TranslationBlock> blocks,
        CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Translating {Count} blocks with NMT", blocks.Count);

        var translatedBlocks = new List<TranslationBlock>();

        foreach (var block in blocks)
        {
            if (!block.ShouldTranslate)
            {
                translatedBlocks.Add(block);
                continue;
            }

            try
            {
                var translatedText = await TranslateAsync(
                    block.Text,
                    block.SourceLanguage,
                    block.TargetLanguage,
                    cancellationToken);

                block.TranslatedText = translatedText;
                translatedBlocks.Add(block);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error translating block {BlockId}", block.BlockId);
                // Keep original text if translation fails
                block.TranslatedText = block.Text;
                translatedBlocks.Add(block);
            }
        }

        return translatedBlocks;
    }

    public async Task<bool> IsAvailableAsync(CancellationToken cancellationToken = default)
    {
        if (!_config.Nmt.Enabled || _config.Nmt.ServiceEndpoints.Count == 0) return false;

        try
        {
            var endpoint = _config.Nmt.ServiceEndpoints[0];
            var response = await _httpClient.GetAsync($"{endpoint}/health", cancellationToken);
            return response.IsSuccessStatusCode;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "NMT service not available");
            return false;
        }
    }

    private string GetNextEndpoint()
    {
        if (_config.Nmt.ServiceEndpoints.Count == 0)
            throw new InvalidOperationException("No NMT service endpoints configured");

        lock (_endpointLock)
        {
            var endpoint = _config.Nmt.ServiceEndpoints[_currentEndpointIndex];
            _currentEndpointIndex = (_currentEndpointIndex + 1) % _config.Nmt.ServiceEndpoints.Count;
            return endpoint;
        }
    }

    private class NmtResponse
    {
        public string? TranslatedText { get; set; }
    }
}