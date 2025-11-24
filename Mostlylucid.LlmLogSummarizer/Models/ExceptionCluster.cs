namespace Mostlylucid.LlmLogSummarizer.Models;

/// <summary>
///     Represents a cluster of similar exceptions/errors.
/// </summary>
public class ExceptionCluster
{
    /// <summary>
    ///     Unique identifier for this cluster.
    /// </summary>
    public string Id { get; set; } = Guid.NewGuid().ToString();

    /// <summary>
    ///     The fingerprint used to group these entries.
    /// </summary>
    public string Fingerprint { get; set; } = string.Empty;

    /// <summary>
    ///     Human-readable title for this cluster.
    /// </summary>
    public string Title { get; set; } = string.Empty;

    /// <summary>
    ///     The exception type (if applicable).
    /// </summary>
    public string? ExceptionType { get; set; }

    /// <summary>
    ///     Representative message from this cluster.
    /// </summary>
    public string RepresentativeMessage { get; set; } = string.Empty;

    /// <summary>
    ///     Representative stack trace from this cluster.
    /// </summary>
    public string? RepresentativeStackTrace { get; set; }

    /// <summary>
    ///     All log entries in this cluster.
    /// </summary>
    public List<LogEntry> Entries { get; set; } = new();

    /// <summary>
    ///     Number of occurrences in this cluster.
    /// </summary>
    public int Count => Entries.Count;

    /// <summary>
    ///     First occurrence timestamp.
    /// </summary>
    public DateTimeOffset FirstOccurrence => Entries.MinBy(e => e.Timestamp)?.Timestamp ?? DateTimeOffset.MinValue;

    /// <summary>
    ///     Last occurrence timestamp.
    /// </summary>
    public DateTimeOffset LastOccurrence => Entries.MaxBy(e => e.Timestamp)?.Timestamp ?? DateTimeOffset.MaxValue;

    /// <summary>
    ///     Common source contexts where this error occurs.
    /// </summary>
    public List<string> SourceContexts => Entries
        .Where(e => !string.IsNullOrEmpty(e.SourceContext))
        .Select(e => e.SourceContext!)
        .Distinct()
        .ToList();

    /// <summary>
    ///     Whether this is a new error pattern (first seen in this time period).
    /// </summary>
    public bool IsNew { get; set; }

    /// <summary>
    ///     Trend compared to previous period (positive = increasing, negative = decreasing).
    /// </summary>
    public double TrendPercent { get; set; }

    /// <summary>
    ///     Previous period count for trend calculation.
    /// </summary>
    public int PreviousPeriodCount { get; set; }

    /// <summary>
    ///     LLM-generated summary of this cluster.
    /// </summary>
    public string? LlmSummary { get; set; }

    /// <summary>
    ///     LLM-generated suggested fix or investigation steps.
    /// </summary>
    public string? SuggestedAction { get; set; }

    /// <summary>
    ///     Severity assessment based on frequency and impact.
    /// </summary>
    public ClusterSeverity Severity { get; set; } = ClusterSeverity.Medium;

    /// <summary>
    ///     Tags for categorization.
    /// </summary>
    public List<string> Tags { get; set; } = new();
}

/// <summary>
///     Severity levels for exception clusters.
/// </summary>
public enum ClusterSeverity
{
    Low,
    Medium,
    High,
    Critical
}