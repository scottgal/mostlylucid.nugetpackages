namespace Mostlylucid.LlmLogSummarizer.Models;

/// <summary>
///     A complete summary report of logs for a time period.
/// </summary>
public class SummaryReport
{
    /// <summary>
    ///     Unique identifier for this report.
    /// </summary>
    public string Id { get; set; } = Guid.NewGuid().ToString();

    /// <summary>
    ///     When this report was generated.
    /// </summary>
    public DateTimeOffset GeneratedAt { get; set; } = DateTimeOffset.UtcNow;

    /// <summary>
    ///     Start of the time period covered.
    /// </summary>
    public DateTimeOffset PeriodStart { get; set; }

    /// <summary>
    ///     End of the time period covered.
    /// </summary>
    public DateTimeOffset PeriodEnd { get; set; }

    /// <summary>
    ///     Total number of log entries analyzed.
    /// </summary>
    public int TotalLogsAnalyzed { get; set; }

    /// <summary>
    ///     Number of error-level logs.
    /// </summary>
    public int ErrorCount { get; set; }

    /// <summary>
    ///     Number of warning-level logs.
    /// </summary>
    public int WarningCount { get; set; }

    /// <summary>
    ///     Number of critical-level logs.
    /// </summary>
    public int CriticalCount { get; set; }

    /// <summary>
    ///     Top error patterns (clusters) sorted by frequency.
    /// </summary>
    public List<ExceptionCluster> TopErrorPatterns { get; set; } = new();

    /// <summary>
    ///     New error types first seen in this period.
    /// </summary>
    public List<ExceptionCluster> NewErrorTypes { get; set; } = new();

    /// <summary>
    ///     Error patterns with increasing trend.
    /// </summary>
    public List<ExceptionCluster> TrendingUp { get; set; } = new();

    /// <summary>
    ///     Error patterns with decreasing trend (good news!).
    /// </summary>
    public List<ExceptionCluster> TrendingDown { get; set; } = new();

    /// <summary>
    ///     All exception clusters found.
    /// </summary>
    public List<ExceptionCluster> AllClusters { get; set; } = new();

    /// <summary>
    ///     LLM-generated executive summary.
    /// </summary>
    public string? ExecutiveSummary { get; set; }

    /// <summary>
    ///     LLM-generated health assessment.
    /// </summary>
    public HealthStatus OverallHealth { get; set; } = HealthStatus.Unknown;

    /// <summary>
    ///     Key insights extracted by LLM.
    /// </summary>
    public List<string> KeyInsights { get; set; } = new();

    /// <summary>
    ///     Recommended actions based on analysis.
    /// </summary>
    public List<string> RecommendedActions { get; set; } = new();

    /// <summary>
    ///     Sources that were analyzed.
    /// </summary>
    public List<string> SourcesAnalyzed { get; set; } = new();

    /// <summary>
    ///     Statistics about the summarization process.
    /// </summary>
    public SummarizationStats ProcessingStats { get; set; } = new();
}

/// <summary>
///     Overall health status of the system.
/// </summary>
public enum HealthStatus
{
    Unknown,
    Healthy,
    Degraded,
    Unhealthy,
    Critical
}

/// <summary>
///     Statistics about the summarization process.
/// </summary>
public class SummarizationStats
{
    /// <summary>
    ///     Time taken to collect logs.
    /// </summary>
    public TimeSpan CollectionDuration { get; set; }

    /// <summary>
    ///     Time taken to cluster logs.
    /// </summary>
    public TimeSpan ClusteringDuration { get; set; }

    /// <summary>
    ///     Time taken for LLM summarization.
    /// </summary>
    public TimeSpan LlmSummarizationDuration { get; set; }

    /// <summary>
    ///     Total processing time.
    /// </summary>
    public TimeSpan TotalDuration { get; set; }

    /// <summary>
    ///     Number of LLM API calls made.
    /// </summary>
    public int LlmCallCount { get; set; }

    /// <summary>
    ///     Total tokens used by LLM.
    /// </summary>
    public int TotalTokensUsed { get; set; }
}