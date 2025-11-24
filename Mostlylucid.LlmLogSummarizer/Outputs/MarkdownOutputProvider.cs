using System.Text;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Mostlylucid.LlmLogSummarizer.Models;

namespace Mostlylucid.LlmLogSummarizer.Outputs;

/// <summary>
///     Outputs summary reports as Markdown files.
/// </summary>
public class MarkdownOutputProvider : IOutputProvider
{
    private readonly MarkdownOutputConfig? _config;
    private readonly ILogger<MarkdownOutputProvider> _logger;

    public MarkdownOutputProvider(
        IOptions<LogSummarizerOptions> options,
        ILogger<MarkdownOutputProvider> logger)
    {
        _config = options.Value.Output.Markdown;
        _logger = logger;
    }

    public string Name => "Markdown";

    public bool IsEnabled => _config?.Enabled ?? false;

    public async Task OutputAsync(SummaryReport report, CancellationToken cancellationToken = default)
    {
        if (_config == null || !IsEnabled)
            return;

        try
        {
            // Ensure output directory exists
            Directory.CreateDirectory(_config.OutputDirectory);

            // Generate filename
            var fileName = _config.FileNamePattern
                .Replace("{date}", report.PeriodEnd.ToString("yyyy-MM-dd"))
                .Replace("{time}", report.PeriodEnd.ToString("HH-mm"))
                .Replace("{period}", $"{report.PeriodStart:MMdd}-{report.PeriodEnd:MMdd}");

            var filePath = Path.Combine(_config.OutputDirectory, fileName);

            // Generate markdown content
            var content = GenerateMarkdown(report);

            // Write file
            await File.WriteAllTextAsync(filePath, content, cancellationToken);

            _logger.LogInformation("Wrote summary report to {FilePath}", filePath);

            // Cleanup old files
            await CleanupOldFilesAsync(cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to write markdown output");
        }
    }

    private string GenerateMarkdown(SummaryReport report)
    {
        var sb = new StringBuilder();

        // Header
        sb.AppendLine("# Log Summary Report");
        sb.AppendLine();
        sb.AppendLine($"**Generated:** {report.GeneratedAt:yyyy-MM-dd HH:mm:ss} UTC");
        sb.AppendLine($"**Period:** {report.PeriodStart:yyyy-MM-dd HH:mm} to {report.PeriodEnd:yyyy-MM-dd HH:mm}");
        sb.AppendLine($"**Sources:** {string.Join(", ", report.SourcesAnalyzed)}");
        sb.AppendLine();

        // Health Status Badge
        var healthEmoji = report.OverallHealth switch
        {
            HealthStatus.Healthy => ":green_circle:",
            HealthStatus.Degraded => ":yellow_circle:",
            HealthStatus.Unhealthy => ":orange_circle:",
            HealthStatus.Critical => ":red_circle:",
            _ => ":white_circle:"
        };
        sb.AppendLine($"## Overall Status: {healthEmoji} {report.OverallHealth}");
        sb.AppendLine();

        // Executive Summary
        if (!string.IsNullOrEmpty(report.ExecutiveSummary))
        {
            sb.AppendLine("## Executive Summary");
            sb.AppendLine();
            sb.AppendLine(report.ExecutiveSummary);
            sb.AppendLine();
        }

        // Quick Stats
        sb.AppendLine("## Quick Stats");
        sb.AppendLine();
        sb.AppendLine("| Metric | Count |");
        sb.AppendLine("|--------|-------|");
        sb.AppendLine($"| Total Logs Analyzed | {report.TotalLogsAnalyzed:N0} |");
        sb.AppendLine($"| Errors | {report.ErrorCount:N0} |");
        sb.AppendLine($"| Warnings | {report.WarningCount:N0} |");
        sb.AppendLine($"| Critical | {report.CriticalCount:N0} |");
        sb.AppendLine($"| Unique Error Patterns | {report.AllClusters.Count} |");
        sb.AppendLine($"| New Error Types | {report.NewErrorTypes.Count} |");
        sb.AppendLine();

        // Key Insights
        if (report.KeyInsights.Any())
        {
            sb.AppendLine("## Key Insights");
            sb.AppendLine();
            foreach (var insight in report.KeyInsights) sb.AppendLine($"- {insight}");
            sb.AppendLine();
        }

        // New Error Types (Important!)
        if (report.NewErrorTypes.Any())
        {
            sb.AppendLine("## :warning: New Error Types");
            sb.AppendLine();
            sb.AppendLine("These errors were first seen in this period:");
            sb.AppendLine();
            foreach (var cluster in report.NewErrorTypes.Take(10)) WriteClusterSummary(sb, cluster);
        }

        // Trending Up (Warning)
        if (report.TrendingUp.Any())
        {
            sb.AppendLine("## :chart_with_upwards_trend: Trending Up");
            sb.AppendLine();
            sb.AppendLine("These errors are increasing compared to the previous period:");
            sb.AppendLine();
            foreach (var cluster in report.TrendingUp.Take(5))
            {
                sb.AppendLine($"### {cluster.Title}");
                sb.AppendLine(
                    $"**Occurrences:** {cluster.Count} (+{cluster.TrendPercent:F0}% from {cluster.PreviousPeriodCount})");
                sb.AppendLine();
            }
        }

        // Top Error Patterns
        sb.AppendLine("## Top Error Patterns");
        sb.AppendLine();
        foreach (var cluster in report.TopErrorPatterns.Take(10)) WriteClusterDetail(sb, cluster);

        // Recommended Actions
        if (report.RecommendedActions.Any())
        {
            sb.AppendLine("## Recommended Actions");
            sb.AppendLine();
            for (var i = 0; i < report.RecommendedActions.Count; i++)
                sb.AppendLine($"{i + 1}. {report.RecommendedActions[i]}");
            sb.AppendLine();
        }

        // Processing Stats
        sb.AppendLine("---");
        sb.AppendLine();
        sb.AppendLine("## Processing Statistics");
        sb.AppendLine();
        sb.AppendLine($"- **Collection Time:** {report.ProcessingStats.CollectionDuration.TotalSeconds:F2}s");
        sb.AppendLine($"- **Clustering Time:** {report.ProcessingStats.ClusteringDuration.TotalSeconds:F2}s");
        sb.AppendLine(
            $"- **LLM Summarization Time:** {report.ProcessingStats.LlmSummarizationDuration.TotalSeconds:F2}s");
        sb.AppendLine($"- **Total Time:** {report.ProcessingStats.TotalDuration.TotalSeconds:F2}s");
        sb.AppendLine($"- **LLM Calls:** {report.ProcessingStats.LlmCallCount}");

        return sb.ToString();
    }

    private static void WriteClusterSummary(StringBuilder sb, ExceptionCluster cluster)
    {
        sb.AppendLine($"### {cluster.Title}");
        sb.AppendLine($"- **Occurrences:** {cluster.Count}");
        sb.AppendLine($"- **Severity:** {cluster.Severity}");
        sb.AppendLine($"- **First Seen:** {cluster.FirstOccurrence:yyyy-MM-dd HH:mm}");
        sb.AppendLine();
    }

    private static void WriteClusterDetail(StringBuilder sb, ExceptionCluster cluster)
    {
        var severityBadge = cluster.Severity switch
        {
            ClusterSeverity.Critical => ":red_circle:",
            ClusterSeverity.High => ":orange_circle:",
            ClusterSeverity.Medium => ":yellow_circle:",
            _ => ":white_circle:"
        };

        sb.AppendLine($"### {severityBadge} {cluster.Title}");
        sb.AppendLine();
        sb.AppendLine($"**Occurrences:** {cluster.Count} | **Severity:** {cluster.Severity}");
        sb.AppendLine($"**Time Range:** {cluster.FirstOccurrence:HH:mm} - {cluster.LastOccurrence:HH:mm}");

        if (cluster.SourceContexts.Any())
            sb.AppendLine($"**Sources:** {string.Join(", ", cluster.SourceContexts.Take(3))}");
        sb.AppendLine();

        if (!string.IsNullOrEmpty(cluster.LlmSummary))
        {
            sb.AppendLine("**Summary:**");
            sb.AppendLine(cluster.LlmSummary);
            sb.AppendLine();
        }

        if (!string.IsNullOrEmpty(cluster.RepresentativeStackTrace))
        {
            sb.AppendLine("<details>");
            sb.AppendLine("<summary>Stack Trace</summary>");
            sb.AppendLine();
            sb.AppendLine("```");
            var stackLines = cluster.RepresentativeStackTrace.Split('\n').Take(10);
            sb.AppendLine(string.Join("\n", stackLines));
            if (cluster.RepresentativeStackTrace.Split('\n').Length > 10)
                sb.AppendLine("...");
            sb.AppendLine("```");
            sb.AppendLine();
            sb.AppendLine("</details>");
            sb.AppendLine();
        }

        if (!string.IsNullOrEmpty(cluster.SuggestedAction))
        {
            sb.AppendLine("**Suggested Actions:**");
            sb.AppendLine(cluster.SuggestedAction);
            sb.AppendLine();
        }

        sb.AppendLine("---");
        sb.AppendLine();
    }

    private async Task CleanupOldFilesAsync(CancellationToken cancellationToken)
    {
        if (_config?.MaxFilesToKeep <= 0)
            return;

        try
        {
            var files = Directory.GetFiles(_config!.OutputDirectory, "*.md")
                .Select(f => new FileInfo(f))
                .OrderByDescending(f => f.CreationTimeUtc)
                .Skip(_config.MaxFilesToKeep)
                .ToList();

            foreach (var file in files)
            {
                file.Delete();
                _logger.LogDebug("Cleaned up old report: {FileName}", file.Name);
            }

            await Task.CompletedTask;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to cleanup old markdown files");
        }
    }
}