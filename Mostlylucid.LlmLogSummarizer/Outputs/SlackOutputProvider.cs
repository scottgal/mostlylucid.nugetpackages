using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Mostlylucid.LlmLogSummarizer.Models;

namespace Mostlylucid.LlmLogSummarizer.Outputs;

/// <summary>
/// Outputs summary reports to Slack via webhook.
/// </summary>
public class SlackOutputProvider : IOutputProvider
{
    private readonly SlackOutputConfig? _config;
    private readonly HttpClient _httpClient;
    private readonly ILogger<SlackOutputProvider> _logger;

    public SlackOutputProvider(
        IOptions<LogSummarizerOptions> options,
        HttpClient httpClient,
        ILogger<SlackOutputProvider> logger)
    {
        _config = options.Value.Output.Slack;
        _httpClient = httpClient;
        _logger = logger;
    }

    public string Name => "Slack";

    public bool IsEnabled => _config?.Enabled ?? false;

    public async Task OutputAsync(SummaryReport report, CancellationToken cancellationToken = default)
    {
        if (_config == null || !IsEnabled || string.IsNullOrEmpty(_config.WebhookUrl))
            return;

        if (_config.OnlyOnErrors && report.ErrorCount == 0)
        {
            _logger.LogDebug("Skipping Slack notification - no errors and OnlyOnErrors is enabled");
            return;
        }

        try
        {
            var payload = BuildSlackPayload(report);

            var response = await _httpClient.PostAsJsonAsync(
                _config.WebhookUrl,
                payload,
                cancellationToken);

            response.EnsureSuccessStatusCode();

            _logger.LogInformation("Sent Slack notification");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to send Slack notification");
        }
    }

    private object BuildSlackPayload(SummaryReport report)
    {
        var healthEmoji = report.OverallHealth switch
        {
            HealthStatus.Healthy => ":large_green_circle:",
            HealthStatus.Degraded => ":large_yellow_circle:",
            HealthStatus.Unhealthy => ":large_orange_circle:",
            HealthStatus.Critical => ":red_circle:",
            _ => ":white_circle:"
        };

        var blocks = new List<object>
        {
            // Header
            new
            {
                type = "header",
                text = new
                {
                    type = "plain_text",
                    text = $"{healthEmoji} Log Summary Report",
                    emoji = true
                }
            },
            // Context
            new
            {
                type = "context",
                elements = new object[]
                {
                    new
                    {
                        type = "mrkdwn",
                        text = $"*Period:* {report.PeriodStart:MMM dd HH:mm} - {report.PeriodEnd:MMM dd HH:mm}"
                    }
                }
            },
            // Divider
            new { type = "divider" },
            // Stats section
            new
            {
                type = "section",
                fields = new object[]
                {
                    new { type = "mrkdwn", text = $"*Status:*\n{report.OverallHealth}" },
                    new { type = "mrkdwn", text = $"*Total Logs:*\n{report.TotalLogsAnalyzed:N0}" },
                    new { type = "mrkdwn", text = $"*Errors:*\n{report.ErrorCount:N0}" },
                    new { type = "mrkdwn", text = $"*New Error Types:*\n{report.NewErrorTypes.Count}" }
                }
            }
        };

        // Executive Summary
        if (!string.IsNullOrEmpty(report.ExecutiveSummary))
        {
            blocks.Add(new { type = "divider" });
            blocks.Add(new
            {
                type = "section",
                text = new
                {
                    type = "mrkdwn",
                    text = $"*Summary:*\n{report.ExecutiveSummary}"
                }
            });
        }

        // Top Errors
        if (report.TopErrorPatterns.Any())
        {
            blocks.Add(new { type = "divider" });
            blocks.Add(new
            {
                type = "section",
                text = new
                {
                    type = "mrkdwn",
                    text = "*Top Error Patterns:*"
                }
            });

            var errorList = string.Join("\n", report.TopErrorPatterns.Take(5)
                .Select(c =>
                {
                    var emoji = c.Severity switch
                    {
                        ClusterSeverity.Critical => ":rotating_light:",
                        ClusterSeverity.High => ":warning:",
                        ClusterSeverity.Medium => ":large_yellow_circle:",
                        _ => ":small_blue_diamond:"
                    };
                    return $"{emoji} *{c.Count}x* {Truncate(c.Title, 60)}";
                }));

            blocks.Add(new
            {
                type = "section",
                text = new
                {
                    type = "mrkdwn",
                    text = errorList
                }
            });
        }

        // Key Insights
        if (report.KeyInsights.Any())
        {
            blocks.Add(new { type = "divider" });
            var insights = string.Join("\n", report.KeyInsights.Take(3).Select(i => $"• {i}"));
            blocks.Add(new
            {
                type = "section",
                text = new
                {
                    type = "mrkdwn",
                    text = $"*Key Insights:*\n{insights}"
                }
            });
        }

        var payload = new Dictionary<string, object>
        {
            ["username"] = _config!.Username,
            ["icon_emoji"] = _config.IconEmoji,
            ["blocks"] = blocks
        };

        if (!string.IsNullOrEmpty(_config.Channel))
        {
            payload["channel"] = _config.Channel;
        }

        return payload;
    }

    private static string Truncate(string text, int maxLength)
    {
        if (string.IsNullOrEmpty(text) || text.Length <= maxLength)
            return text;
        return text[..(maxLength - 3)] + "...";
    }
}
