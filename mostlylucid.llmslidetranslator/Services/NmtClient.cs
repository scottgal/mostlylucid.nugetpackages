using System.Net.Http.Json;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using mostlylucid.llmslidetranslator.Models;

namespace mostlylucid.llmslidetranslator.Services;

/// <summary>
///     Client for NMT (Neural Machine Translation) service
///     Based on https://github.com/scottgal/mostlyucid-nmt (Opus-MT)
/// </summary>
public class NmtClient(
    ILogger<NmtClient> logger,
    HttpClient httpClient,
    IOptions<LlmSlideTranslatorConfig> config) : INmtClient
{
    private readonly LlmSlideTranslatorConfig config = config.Value;
    private readonly object endpointLock = new();
    private int _currentEndpointIndex;

    public async Task<string> TranslateAsync(
        string text,
        string sourceLanguage,
        string targetLanguage,
        CancellationToken cancellationToken = default)
    {
        if (!config.Nmt.Enabled) throw new InvalidOperationException("NMT is not enabled in configuration");

        logger.LogDebug("Translating text from {Source} to {Target} using NMT",
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
            var response = await httpClient.PostAsJsonAsync(
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
            logger.LogError(ex, "Error translating with NMT at endpoint {Endpoint}", endpoint);
            throw;
        }
    }

    public async Task<List<TranslationBlock>> TranslateBatchAsync(
        List<TranslationBlock> blocks,
        CancellationToken cancellationToken = default)
    {
        logger.LogInformation("Translating {Count} blocks with NMT", blocks.Count);

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
                logger.LogError(ex, "Error translating block {BlockId}", block.BlockId);
                // Keep original text if translation fails
                block.TranslatedText = block.Text;
                translatedBlocks.Add(block);
            }
        }

        return translatedBlocks;
    }

    public async Task<bool> IsAvailableAsync(CancellationToken cancellationToken = default)
    {
        if (!config.Nmt.Enabled || config.Nmt.ServiceEndpoints.Count == 0) return false;

        try
        {
            var endpoint = config.Nmt.ServiceEndpoints[0];
            var response = await httpClient.GetAsync($"{endpoint}/health", cancellationToken);
            return response.IsSuccessStatusCode;
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "NMT service not available");
            return false;
        }
    }

    private string GetNextEndpoint()
    {
        if (config.Nmt.ServiceEndpoints.Count == 0)
            throw new InvalidOperationException("No NMT service endpoints configured");

        lock (endpointLock)
        {
            var endpoint = config.Nmt.ServiceEndpoints[_currentEndpointIndex];
            _currentEndpointIndex = (_currentEndpointIndex + 1) % config.Nmt.ServiceEndpoints.Count;
            return endpoint;
        }
    }

    private class NmtResponse
    {
        public string? TranslatedText { get; set; }
    }
}