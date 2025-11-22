using System.Diagnostics;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Mostlylucid.LlmLogSummarizer.Clustering;
using Mostlylucid.LlmLogSummarizer.Models;
using Mostlylucid.LlmLogSummarizer.Outputs;
using Mostlylucid.LlmLogSummarizer.Sources;
using Mostlylucid.LlmLogSummarizer.Telemetry;

namespace Mostlylucid.LlmLogSummarizer.Services;

/// <summary>
/// Orchestrates the log summarization process.
/// </summary>
public class LogSummarizationOrchestrator : ILogSummarizationOrchestrator
{
    private readonly ILogSourceAggregator _sourceAggregator;
    private readonly IExceptionClusterer _clusterer;
    private readonly ILogSummarizer _summarizer;
    private readonly IEnumerable<IOutputProvider> _outputProviders;
    private readonly LogSummarizerOptions _options;
    private readonly ILogger<LogSummarizationOrchestrator> _logger;

    // Store historical clusters for trend analysis
    private List<ExceptionCluster> _previousPeriodClusters = new();

    public LogSummarizationOrchestrator(
        ILogSourceAggregator sourceAggregator,
        IExceptionClusterer clusterer,
        ILogSummarizer summarizer,
        IEnumerable<IOutputProvider> outputProviders,
        IOptions<LogSummarizerOptions> options,
        ILogger<LogSummarizationOrchestrator> logger)
    {
        _sourceAggregator = sourceAggregator;
        _clusterer = clusterer;
        _summarizer = summarizer;
        _outputProviders = outputProviders;
        _options = options.Value;
        _logger = logger;
    }

    public async Task<SummaryReport> RunSummarizationAsync(CancellationToken cancellationToken = default)
    {
        var totalStopwatch = Stopwatch.StartNew();
        var report = new SummaryReport();

        var now = DateTimeOffset.UtcNow;
        report.PeriodEnd = now;
        report.PeriodStart = now - _options.LookbackPeriod;

        using var activity = LogSummarizerTelemetry.StartSummarizationActivity(report.PeriodStart, report.PeriodEnd);

        try
        {
            _logger.LogInformation(
                "Starting log summarization for period {Start} to {End}",
                report.PeriodStart, report.PeriodEnd);

            // Step 1: Collect logs
            List<LogEntry> entries;
            using (var collectionActivity = LogSummarizerTelemetry.StartCollectionActivity())
            {
                var collectionStopwatch = Stopwatch.StartNew();
                entries = await CollectLogsAsync(report, cancellationToken);
                report.ProcessingStats.CollectionDuration = collectionStopwatch.Elapsed;
                LogSummarizerTelemetry.RecordCollectionResult(
                    collectionActivity,
                    entries.Count,
                    report.SourcesAnalyzed.Count,
                    report.ProcessingStats.CollectionDuration);
            }

            // Step 2: Cluster exceptions
            List<ExceptionCluster> clusters;
            using (var clusteringActivity = LogSummarizerTelemetry.StartClusteringActivity())
            {
                var clusteringStopwatch = Stopwatch.StartNew();
                clusters = ClusterLogs(entries, report);
                report.ProcessingStats.ClusteringDuration = clusteringStopwatch.Elapsed;
                LogSummarizerTelemetry.RecordClusteringResult(
                    clusteringActivity,
                    clusters.Count,
                    report.ProcessingStats.ClusteringDuration);
            }

            // Step 3: Calculate trends
            _clusterer.CalculateTrends(clusters, _previousPeriodClusters);

            // Store clusters for next run's trend analysis
            _previousPeriodClusters = clusters;

            // Categorize clusters
            CategorizeReport(report, clusters);

            // Step 4: LLM Summarization
            using (var llmActivity = LogSummarizerTelemetry.StartLlmEnrichmentActivity())
            {
                var llmStopwatch = Stopwatch.StartNew();
                var llmAvailable = await _summarizer.IsAvailableAsync(cancellationToken);
                await EnrichWithLlmAsync(report, cancellationToken);
                report.ProcessingStats.LlmSummarizationDuration = llmStopwatch.Elapsed;
                LogSummarizerTelemetry.RecordLlmEnrichmentResult(
                    llmActivity,
                    report.ProcessingStats.LlmCallCount,
                    report.ProcessingStats.LlmSummarizationDuration,
                    llmAvailable);
            }

            // Step 5: Output results
            using (var outputActivity = LogSummarizerTelemetry.StartOutputActivity())
            {
                var enabledProviderCount = await OutputReportAsync(report, cancellationToken);
                LogSummarizerTelemetry.RecordOutputResult(outputActivity, enabledProviderCount);
            }

            totalStopwatch.Stop();
            report.ProcessingStats.TotalDuration = totalStopwatch.Elapsed;

            LogSummarizerTelemetry.RecordResult(activity, report);

            _logger.LogInformation(
                "Log summarization completed in {Duration:F2}s - {Errors} errors, {Clusters} patterns, Health: {Health}",
                totalStopwatch.Elapsed.TotalSeconds,
                report.ErrorCount,
                report.AllClusters.Count,
                report.OverallHealth);

            return report;
        }
        catch (Exception ex)
        {
            LogSummarizerTelemetry.RecordException(activity, ex);
            _logger.LogError(ex, "Log summarization failed");
            throw;
        }
    }

    private async Task<List<LogEntry>> CollectLogsAsync(SummaryReport report, CancellationToken cancellationToken)
    {
        var entries = new List<LogEntry>();
        var sources = new HashSet<string>();

        await foreach (var entry in _sourceAggregator.GetAllEntriesAsync(
            report.PeriodStart,
            report.PeriodEnd,
            _options.MaxEntriesPerRun,
            cancellationToken))
        {
            entries.Add(entry);
            if (!string.IsNullOrEmpty(entry.SourceName))
                sources.Add(entry.SourceName);
        }

        report.TotalLogsAnalyzed = entries.Count;
        report.SourcesAnalyzed = sources.ToList();

        // Count by level
        report.ErrorCount = entries.Count(e => e.Level == Models.LogLevel.Error);
        report.WarningCount = entries.Count(e => e.Level == Models.LogLevel.Warning);
        report.CriticalCount = entries.Count(e => e.Level == Models.LogLevel.Critical);

        _logger.LogDebug(
            "Collected {Total} logs: {Errors} errors, {Warnings} warnings, {Critical} critical",
            entries.Count, report.ErrorCount, report.WarningCount, report.CriticalCount);

        return entries;
    }

    private List<ExceptionCluster> ClusterLogs(List<LogEntry> entries, SummaryReport report)
    {
        var clusters = _clusterer.ClusterExceptions(entries, _options.Clustering);

        // Filter by minimum occurrences
        return clusters
            .Where(c => c.Count >= _options.MinOccurrencesForReporting)
            .ToList();
    }

    private static void CategorizeReport(SummaryReport report, List<ExceptionCluster> clusters)
    {
        report.AllClusters = clusters;

        // Top patterns by count
        report.TopErrorPatterns = clusters
            .OrderByDescending(c => c.Count)
            .ToList();

        // New error types
        report.NewErrorTypes = clusters
            .Where(c => c.IsNew)
            .OrderByDescending(c => c.Count)
            .ToList();

        // Trending up (increasing by more than 20%)
        report.TrendingUp = clusters
            .Where(c => !c.IsNew && c.TrendPercent > 20)
            .OrderByDescending(c => c.TrendPercent)
            .ToList();

        // Trending down (decreasing by more than 20%)
        report.TrendingDown = clusters
            .Where(c => !c.IsNew && c.TrendPercent < -20)
            .OrderBy(c => c.TrendPercent)
            .ToList();
    }

    private async Task EnrichWithLlmAsync(SummaryReport report, CancellationToken cancellationToken)
    {
        var llmAvailable = await _summarizer.IsAvailableAsync(cancellationToken);

        if (!llmAvailable)
        {
            _logger.LogWarning("LLM service is not available, skipping AI-powered summarization");
            report.OverallHealth = InferHealthWithoutLlm(report);
            return;
        }

        var llmCallCount = 0;

        // Summarize top clusters
        foreach (var cluster in report.TopErrorPatterns.Take(_options.TopPatternsCount))
        {
            try
            {
                cluster.LlmSummary = await _summarizer.SummarizeClusterAsync(cluster, cancellationToken);
                llmCallCount++;

                // Only generate suggested actions for high-severity issues
                if (cluster.Severity >= ClusterSeverity.Medium)
                {
                    cluster.SuggestedAction = await _summarizer.GenerateSuggestedActionAsync(cluster, cancellationToken);
                    llmCallCount++;
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to summarize cluster {Title}", cluster.Title);
            }
        }

        // Generate executive summary
        try
        {
            report.ExecutiveSummary = await _summarizer.GenerateExecutiveSummaryAsync(report, cancellationToken);
            llmCallCount++;

            report.KeyInsights = await _summarizer.GenerateKeyInsightsAsync(report, cancellationToken);
            llmCallCount++;

            report.OverallHealth = await _summarizer.AssessHealthStatusAsync(report, cancellationToken);
            llmCallCount++;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to generate executive summary");
            report.OverallHealth = InferHealthWithoutLlm(report);
        }

        // Generate recommended actions from cluster actions
        report.RecommendedActions = report.TopErrorPatterns
            .Where(c => !string.IsNullOrEmpty(c.SuggestedAction))
            .Take(5)
            .Select(c => $"[{c.Title}] {c.SuggestedAction!.Split('\n').FirstOrDefault()?.TrimStart('-', '*', ' ')}")
            .Where(a => !string.IsNullOrEmpty(a))
            .ToList()!;

        report.ProcessingStats.LlmCallCount = llmCallCount;
    }

    private static HealthStatus InferHealthWithoutLlm(SummaryReport report)
    {
        var errorRate = report.TotalLogsAnalyzed > 0
            ? (double)report.ErrorCount / report.TotalLogsAnalyzed
            : 0;

        return (report.CriticalCount, errorRate, report.NewErrorTypes.Count) switch
        {
            ( > 0, _, _) => HealthStatus.Critical,
            (_, > 0.1, _) => HealthStatus.Unhealthy,
            (_, > 0.05, _) or (_, _, > 5) => HealthStatus.Degraded,
            _ => HealthStatus.Healthy
        };
    }

    private async Task<int> OutputReportAsync(SummaryReport report, CancellationToken cancellationToken)
    {
        var enabledProviders = _outputProviders.Where(p => p.IsEnabled).ToList();

        _logger.LogDebug("Outputting report to {Count} providers: {Providers}",
            enabledProviders.Count,
            string.Join(", ", enabledProviders.Select(p => p.Name)));

        foreach (var provider in enabledProviders)
        {
            try
            {
                await provider.OutputAsync(report, cancellationToken);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to output to {Provider}", provider.Name);
            }
        }

        return enabledProviders.Count;
    }
}

/// <summary>
/// Interface for the log summarization orchestrator.
/// </summary>
public interface ILogSummarizationOrchestrator
{
    /// <summary>
    /// Runs the complete log summarization process.
    /// </summary>
    Task<SummaryReport> RunSummarizationAsync(CancellationToken cancellationToken = default);
}
