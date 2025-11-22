using System.Text;
using MailKit.Net.Smtp;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using MimeKit;
using Mostlylucid.LlmLogSummarizer.Models;

namespace Mostlylucid.LlmLogSummarizer.Outputs;

/// <summary>
/// Outputs summary reports via email.
/// </summary>
public class EmailOutputProvider : IOutputProvider
{
    private readonly EmailOutputConfig? _config;
    private readonly ILogger<EmailOutputProvider> _logger;

    public EmailOutputProvider(
        IOptions<LogSummarizerOptions> options,
        ILogger<EmailOutputProvider> logger)
    {
        _config = options.Value.Output.Email;
        _logger = logger;
    }

    public string Name => "Email";

    public bool IsEnabled => _config?.Enabled ?? false;

    public async Task OutputAsync(SummaryReport report, CancellationToken cancellationToken = default)
    {
        if (_config == null || !IsEnabled)
            return;

        // Check if we should skip (only on errors mode)
        if (_config.OnlyOnErrors && report.ErrorCount == 0)
        {
            _logger.LogDebug("Skipping email - no errors and OnlyOnErrors is enabled");
            return;
        }

        try
        {
            var message = CreateMessage(report);

            using var client = new SmtpClient();
            await client.ConnectAsync(_config.SmtpHost, _config.SmtpPort, _config.UseSsl, cancellationToken);

            if (!string.IsNullOrEmpty(_config.Username))
            {
                await client.AuthenticateAsync(_config.Username, _config.Password, cancellationToken);
            }

            await client.SendAsync(message, cancellationToken);
            await client.DisconnectAsync(true, cancellationToken);

            _logger.LogInformation("Sent email report to {Count} recipients", _config.ToAddresses.Count);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to send email report");
        }
    }

    private MimeMessage CreateMessage(SummaryReport report)
    {
        var message = new MimeMessage();
        message.From.Add(new MailboxAddress(_config!.FromName, _config.FromAddress));

        foreach (var to in _config.ToAddresses)
        {
            message.To.Add(MailboxAddress.Parse(to));
        }

        // Generate subject
        message.Subject = _config.SubjectPattern
            .Replace("{date}", report.PeriodEnd.ToString("yyyy-MM-dd"))
            .Replace("{errorCount}", report.ErrorCount.ToString())
            .Replace("{health}", report.OverallHealth.ToString());

        // Create HTML body
        var htmlBody = GenerateHtmlBody(report);
        var textBody = GenerateTextBody(report);

        var builder = new BodyBuilder
        {
            HtmlBody = htmlBody,
            TextBody = textBody
        };

        message.Body = builder.ToMessageBody();

        return message;
    }

    private static string GenerateHtmlBody(SummaryReport report)
    {
        var sb = new StringBuilder();
        sb.AppendLine("<!DOCTYPE html>");
        sb.AppendLine("<html><head><style>");
        sb.AppendLine("body { font-family: -apple-system, BlinkMacSystemFont, 'Segoe UI', Roboto, sans-serif; }");
        sb.AppendLine(".healthy { color: #22c55e; }");
        sb.AppendLine(".degraded { color: #eab308; }");
        sb.AppendLine(".unhealthy { color: #f97316; }");
        sb.AppendLine(".critical { color: #ef4444; }");
        sb.AppendLine(".stat-box { display: inline-block; padding: 10px 20px; margin: 5px; background: #f3f4f6; border-radius: 8px; }");
        sb.AppendLine(".stat-value { font-size: 24px; font-weight: bold; }");
        sb.AppendLine(".stat-label { font-size: 12px; color: #6b7280; }");
        sb.AppendLine("table { border-collapse: collapse; width: 100%; margin: 20px 0; }");
        sb.AppendLine("th, td { padding: 12px; text-align: left; border-bottom: 1px solid #e5e7eb; }");
        sb.AppendLine("th { background: #f9fafb; }");
        sb.AppendLine("</style></head><body>");

        // Header
        sb.AppendLine($"<h1>Log Summary Report</h1>");
        sb.AppendLine($"<p><strong>Period:</strong> {report.PeriodStart:yyyy-MM-dd HH:mm} to {report.PeriodEnd:yyyy-MM-dd HH:mm}</p>");

        // Health Status
        var healthClass = report.OverallHealth.ToString().ToLowerInvariant();
        sb.AppendLine($"<h2>Status: <span class=\"{healthClass}\">{report.OverallHealth}</span></h2>");

        // Stats
        sb.AppendLine("<div>");
        sb.AppendLine($"<div class=\"stat-box\"><div class=\"stat-value\">{report.TotalLogsAnalyzed:N0}</div><div class=\"stat-label\">Total Logs</div></div>");
        sb.AppendLine($"<div class=\"stat-box\"><div class=\"stat-value critical\">{report.ErrorCount:N0}</div><div class=\"stat-label\">Errors</div></div>");
        sb.AppendLine($"<div class=\"stat-box\"><div class=\"stat-value\">{report.WarningCount:N0}</div><div class=\"stat-label\">Warnings</div></div>");
        sb.AppendLine($"<div class=\"stat-box\"><div class=\"stat-value\">{report.NewErrorTypes.Count}</div><div class=\"stat-label\">New Error Types</div></div>");
        sb.AppendLine("</div>");

        // Executive Summary
        if (!string.IsNullOrEmpty(report.ExecutiveSummary))
        {
            sb.AppendLine("<h3>Summary</h3>");
            sb.AppendLine($"<p>{System.Net.WebUtility.HtmlEncode(report.ExecutiveSummary)}</p>");
        }

        // Top Errors Table
        if (report.TopErrorPatterns.Any())
        {
            sb.AppendLine("<h3>Top Error Patterns</h3>");
            sb.AppendLine("<table>");
            sb.AppendLine("<tr><th>Error</th><th>Count</th><th>Severity</th></tr>");
            foreach (var cluster in report.TopErrorPatterns.Take(10))
            {
                var severityClass = cluster.Severity.ToString().ToLowerInvariant();
                sb.AppendLine($"<tr><td>{System.Net.WebUtility.HtmlEncode(cluster.Title)}</td><td>{cluster.Count}</td><td class=\"{severityClass}\">{cluster.Severity}</td></tr>");
            }
            sb.AppendLine("</table>");
        }

        // Key Insights
        if (report.KeyInsights.Any())
        {
            sb.AppendLine("<h3>Key Insights</h3>");
            sb.AppendLine("<ul>");
            foreach (var insight in report.KeyInsights)
            {
                sb.AppendLine($"<li>{System.Net.WebUtility.HtmlEncode(insight)}</li>");
            }
            sb.AppendLine("</ul>");
        }

        sb.AppendLine("</body></html>");
        return sb.ToString();
    }

    private static string GenerateTextBody(SummaryReport report)
    {
        var sb = new StringBuilder();
        sb.AppendLine("LOG SUMMARY REPORT");
        sb.AppendLine("==================");
        sb.AppendLine();
        sb.AppendLine($"Period: {report.PeriodStart:yyyy-MM-dd HH:mm} to {report.PeriodEnd:yyyy-MM-dd HH:mm}");
        sb.AppendLine($"Status: {report.OverallHealth}");
        sb.AppendLine();
        sb.AppendLine("STATS");
        sb.AppendLine("-----");
        sb.AppendLine($"Total Logs: {report.TotalLogsAnalyzed:N0}");
        sb.AppendLine($"Errors: {report.ErrorCount:N0}");
        sb.AppendLine($"Warnings: {report.WarningCount:N0}");
        sb.AppendLine($"New Error Types: {report.NewErrorTypes.Count}");
        sb.AppendLine();

        if (!string.IsNullOrEmpty(report.ExecutiveSummary))
        {
            sb.AppendLine("SUMMARY");
            sb.AppendLine("-------");
            sb.AppendLine(report.ExecutiveSummary);
            sb.AppendLine();
        }

        if (report.TopErrorPatterns.Any())
        {
            sb.AppendLine("TOP ERRORS");
            sb.AppendLine("----------");
            foreach (var cluster in report.TopErrorPatterns.Take(5))
            {
                sb.AppendLine($"- [{cluster.Severity}] {cluster.Title} ({cluster.Count} occurrences)");
            }
        }

        return sb.ToString();
    }
}
