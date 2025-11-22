using System.Net.Http.Json;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Mostlylucid.LlmLogSummarizer.Models;

namespace Mostlylucid.LlmLogSummarizer.Outputs;

/// <summary>
/// Outputs summary reports to a generic webhook.
/// </summary>
public class WebhookOutputProvider : IOutputProvider
{
    private readonly WebhookOutputConfig? _config;
    private readonly HttpClient _httpClient;
    private readonly ILogger<WebhookOutputProvider> _logger;

    public WebhookOutputProvider(
        IOptions<LogSummarizerOptions> options,
        HttpClient httpClient,
        ILogger<WebhookOutputProvider> logger)
    {
        _config = options.Value.Output.Webhook;
        _httpClient = httpClient;
        _logger = logger;
    }

    public string Name => "Webhook";

    public bool IsEnabled => _config?.Enabled ?? false;

    public async Task OutputAsync(SummaryReport report, CancellationToken cancellationToken = default)
    {
        if (_config == null || !IsEnabled || string.IsNullOrEmpty(_config.Url))
            return;

        if (_config.OnlyOnErrors && report.ErrorCount == 0)
        {
            _logger.LogDebug("Skipping webhook - no errors and OnlyOnErrors is enabled");
            return;
        }

        try
        {
            var payload = BuildPayload(report);

            var request = new HttpRequestMessage(
                new HttpMethod(_config.Method),
                _config.Url)
            {
                Content = JsonContent.Create(payload)
            };

            // Add custom headers
            foreach (var header in _config.Headers)
            {
                request.Headers.TryAddWithoutValidation(header.Key, header.Value);
            }

            var response = await _httpClient.SendAsync(request, cancellationToken);
            response.EnsureSuccessStatusCode();

            _logger.LogInformation("Sent webhook notification to {Url}", _config.Url);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to send webhook notification");
        }
    }

    private static object BuildPayload(SummaryReport report)
    {
        return new
        {
            id = report.Id,
            generatedAt = report.GeneratedAt,
            period = new
            {
                start = report.PeriodStart,
                end = report.PeriodEnd
            },
            health = report.OverallHealth.ToString(),
            stats = new
            {
                totalLogs = report.TotalLogsAnalyzed,
                errors = report.ErrorCount,
                warnings = report.WarningCount,
                critical = report.CriticalCount,
                uniquePatterns = report.AllClusters.Count,
                newErrorTypes = report.NewErrorTypes.Count
            },
            executiveSummary = report.ExecutiveSummary,
            keyInsights = report.KeyInsights,
            topErrors = report.TopErrorPatterns.Take(10).Select(c => new
            {
                id = c.Id,
                title = c.Title,
                exceptionType = c.ExceptionType,
                count = c.Count,
                severity = c.Severity.ToString(),
                isNew = c.IsNew,
                trendPercent = c.TrendPercent,
                summary = c.LlmSummary,
                suggestedAction = c.SuggestedAction,
                firstOccurrence = c.FirstOccurrence,
                lastOccurrence = c.LastOccurrence
            }),
            newErrors = report.NewErrorTypes.Select(c => new
            {
                id = c.Id,
                title = c.Title,
                exceptionType = c.ExceptionType,
                count = c.Count
            }),
            processingStats = new
            {
                collectionDuration = report.ProcessingStats.CollectionDuration.TotalSeconds,
                clusteringDuration = report.ProcessingStats.ClusteringDuration.TotalSeconds,
                llmDuration = report.ProcessingStats.LlmSummarizationDuration.TotalSeconds,
                totalDuration = report.ProcessingStats.TotalDuration.TotalSeconds,
                llmCalls = report.ProcessingStats.LlmCallCount
            }
        };
    }
}
