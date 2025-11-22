using System.Diagnostics;
using Mostlylucid.LlmLogSummarizer.Models;

namespace Mostlylucid.LlmLogSummarizer.Telemetry;

/// <summary>
/// Telemetry instrumentation for log summarization operations
/// </summary>
public static class LogSummarizerTelemetry
{
    /// <summary>
    /// Activity source name for log summarization
    /// </summary>
    public const string ActivitySourceName = "Mostlylucid.LlmLogSummarizer";

    /// <summary>
    /// Activity source for log summarization telemetry
    /// </summary>
    public static readonly ActivitySource ActivitySource = new(ActivitySourceName, GetVersion());

    private static string GetVersion()
    {
        return typeof(LogSummarizerTelemetry).Assembly.GetName().Version?.ToString() ?? "1.0.0";
    }

    /// <summary>
    /// Starts an activity for log summarization
    /// </summary>
    public static Activity? StartSummarizationActivity(DateTimeOffset? periodStart = null, DateTimeOffset? periodEnd = null)
    {
        var activity = ActivitySource.StartActivity("LogSummarizer.Summarize", ActivityKind.Internal);

        if (activity != null)
        {
            if (periodStart.HasValue)
                activity.SetTag("mostlylucid.logsummarizer.period_start", periodStart.Value.ToString("O"));
            if (periodEnd.HasValue)
                activity.SetTag("mostlylucid.logsummarizer.period_end", periodEnd.Value.ToString("O"));
        }

        return activity;
    }

    /// <summary>
    /// Starts an activity for log collection
    /// </summary>
    public static Activity? StartCollectionActivity()
    {
        return ActivitySource.StartActivity("LogSummarizer.CollectLogs", ActivityKind.Internal);
    }

    /// <summary>
    /// Starts an activity for log clustering
    /// </summary>
    public static Activity? StartClusteringActivity()
    {
        return ActivitySource.StartActivity("LogSummarizer.ClusterLogs", ActivityKind.Internal);
    }

    /// <summary>
    /// Starts an activity for LLM enrichment
    /// </summary>
    public static Activity? StartLlmEnrichmentActivity()
    {
        return ActivitySource.StartActivity("LogSummarizer.LlmEnrichment", ActivityKind.Internal);
    }

    /// <summary>
    /// Starts an activity for output generation
    /// </summary>
    public static Activity? StartOutputActivity()
    {
        return ActivitySource.StartActivity("LogSummarizer.OutputReport", ActivityKind.Internal);
    }

    /// <summary>
    /// Records summarization result on the activity
    /// </summary>
    public static void RecordResult(Activity? activity, SummaryReport report)
    {
        if (activity == null)
            return;

        activity.SetTag("mostlylucid.logsummarizer.total_logs", report.TotalLogsAnalyzed);
        activity.SetTag("mostlylucid.logsummarizer.error_count", report.ErrorCount);
        activity.SetTag("mostlylucid.logsummarizer.warning_count", report.WarningCount);
        activity.SetTag("mostlylucid.logsummarizer.critical_count", report.CriticalCount);
        activity.SetTag("mostlylucid.logsummarizer.cluster_count", report.AllClusters.Count);
        activity.SetTag("mostlylucid.logsummarizer.new_error_types", report.NewErrorTypes.Count);
        activity.SetTag("mostlylucid.logsummarizer.sources_count", report.SourcesAnalyzed.Count);
        activity.SetTag("mostlylucid.logsummarizer.health_status", report.OverallHealth.ToString());
        activity.SetTag("mostlylucid.logsummarizer.total_duration_ms", report.ProcessingStats.TotalDuration.TotalMilliseconds);
        activity.SetTag("mostlylucid.logsummarizer.llm_call_count", report.ProcessingStats.LlmCallCount);

        if (report.PeriodStart != default)
            activity.SetTag("mostlylucid.logsummarizer.period_start", report.PeriodStart.ToString("O"));
        if (report.PeriodEnd != default)
            activity.SetTag("mostlylucid.logsummarizer.period_end", report.PeriodEnd.ToString("O"));

        activity.SetStatus(ActivityStatusCode.Ok);
    }

    /// <summary>
    /// Records collection metrics on the activity
    /// </summary>
    public static void RecordCollectionResult(Activity? activity, int logCount, int sourceCount, TimeSpan duration)
    {
        if (activity == null)
            return;

        activity.SetTag("mostlylucid.logsummarizer.logs_collected", logCount);
        activity.SetTag("mostlylucid.logsummarizer.sources_count", sourceCount);
        activity.SetTag("mostlylucid.logsummarizer.collection_duration_ms", duration.TotalMilliseconds);
        activity.SetStatus(ActivityStatusCode.Ok);
    }

    /// <summary>
    /// Records clustering metrics on the activity
    /// </summary>
    public static void RecordClusteringResult(Activity? activity, int clusterCount, TimeSpan duration)
    {
        if (activity == null)
            return;

        activity.SetTag("mostlylucid.logsummarizer.cluster_count", clusterCount);
        activity.SetTag("mostlylucid.logsummarizer.clustering_duration_ms", duration.TotalMilliseconds);
        activity.SetStatus(ActivityStatusCode.Ok);
    }

    /// <summary>
    /// Records LLM enrichment metrics on the activity
    /// </summary>
    public static void RecordLlmEnrichmentResult(Activity? activity, int llmCallCount, TimeSpan duration, bool llmAvailable)
    {
        if (activity == null)
            return;

        activity.SetTag("mostlylucid.logsummarizer.llm_available", llmAvailable);
        activity.SetTag("mostlylucid.logsummarizer.llm_call_count", llmCallCount);
        activity.SetTag("mostlylucid.logsummarizer.llm_duration_ms", duration.TotalMilliseconds);
        activity.SetStatus(ActivityStatusCode.Ok);
    }

    /// <summary>
    /// Records output metrics on the activity
    /// </summary>
    public static void RecordOutputResult(Activity? activity, int providerCount)
    {
        if (activity == null)
            return;

        activity.SetTag("mostlylucid.logsummarizer.output_providers", providerCount);
        activity.SetStatus(ActivityStatusCode.Ok);
    }

    /// <summary>
    /// Records an exception on the activity
    /// </summary>
    public static void RecordException(Activity? activity, Exception ex)
    {
        if (activity == null)
            return;

        activity.SetStatus(ActivityStatusCode.Error, ex.Message);
        activity.SetTag("exception.type", ex.GetType().FullName);
        activity.SetTag("exception.message", ex.Message);
    }
}
