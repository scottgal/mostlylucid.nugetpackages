using System.Text.Json.Serialization;

namespace Mostlylucid.LlmAccessibilityAuditor.Models;

/// <summary>
/// Complete accessibility audit report for an HTML document
/// </summary>
public class AccessibilityAuditReport
{
    /// <summary>
    /// Unique identifier for this report
    /// </summary>
    public string ReportId { get; set; } = Guid.NewGuid().ToString("N");

    /// <summary>
    /// URL or identifier of the audited page
    /// </summary>
    public string? PageUrl { get; set; }

    /// <summary>
    /// Title of the audited page
    /// </summary>
    public string? PageTitle { get; set; }

    /// <summary>
    /// When the audit was performed
    /// </summary>
    public DateTimeOffset AuditedAt { get; set; } = DateTimeOffset.UtcNow;

    /// <summary>
    /// Duration of the audit in milliseconds
    /// </summary>
    public long AuditDurationMs { get; set; }

    /// <summary>
    /// All issues found during the audit
    /// </summary>
    public List<AccessibilityIssue> Issues { get; set; } = new();

    /// <summary>
    /// Summary statistics
    /// </summary>
    public AuditSummary Summary { get; set; } = new();

    /// <summary>
    /// Human-readable summary of findings
    /// </summary>
    public string? HumanSummary { get; set; }

    /// <summary>
    /// Overall accessibility score (0-100)
    /// </summary>
    public int? OverallScore { get; set; }

    /// <summary>
    /// Whether LLM analysis was performed
    /// </summary>
    public bool LlmAnalysisPerformed { get; set; }

    /// <summary>
    /// Model used for LLM analysis (if any)
    /// </summary>
    public string? LlmModel { get; set; }

    /// <summary>
    /// Any errors that occurred during auditing
    /// </summary>
    public List<string> Errors { get; set; } = new();

    /// <summary>
    /// Size of the HTML document in bytes
    /// </summary>
    public long HtmlSizeBytes { get; set; }

    /// <summary>
    /// Whether the audit was truncated due to size limits
    /// </summary>
    public bool WasTruncated { get; set; }
}

/// <summary>
/// Summary statistics for an audit report
/// </summary>
public class AuditSummary
{
    /// <summary>
    /// Total number of issues found
    /// </summary>
    public int TotalIssues { get; set; }

    /// <summary>
    /// Number of critical issues
    /// </summary>
    public int CriticalCount { get; set; }

    /// <summary>
    /// Number of serious issues
    /// </summary>
    public int SeriousCount { get; set; }

    /// <summary>
    /// Number of moderate issues
    /// </summary>
    public int ModerateCount { get; set; }

    /// <summary>
    /// Number of minor issues
    /// </summary>
    public int MinorCount { get; set; }

    /// <summary>
    /// Number of informational issues
    /// </summary>
    public int InfoCount { get; set; }

    /// <summary>
    /// Issues by type
    /// </summary>
    public Dictionary<string, int> IssuesByType { get; set; } = new();

    /// <summary>
    /// Issues detected by HTML parser
    /// </summary>
    public int HtmlParserIssues { get; set; }

    /// <summary>
    /// Issues detected by LLM analysis
    /// </summary>
    public int LlmIssues { get; set; }

    /// <summary>
    /// Calculate summary from issues list
    /// </summary>
    public static AuditSummary FromIssues(IReadOnlyList<AccessibilityIssue> issues)
    {
        var summary = new AuditSummary
        {
            TotalIssues = issues.Count,
            CriticalCount = issues.Count(i => i.Severity == IssueSeverity.Critical),
            SeriousCount = issues.Count(i => i.Severity == IssueSeverity.Serious),
            ModerateCount = issues.Count(i => i.Severity == IssueSeverity.Moderate),
            MinorCount = issues.Count(i => i.Severity == IssueSeverity.Minor),
            InfoCount = issues.Count(i => i.Severity == IssueSeverity.Info),
            HtmlParserIssues = issues.Count(i => i.Source == DetectionSource.HtmlParser),
            LlmIssues = issues.Count(i => i.Source == DetectionSource.LlmAnalysis),
            IssuesByType = issues
                .GroupBy(i => i.Type.ToString())
                .ToDictionary(g => g.Key, g => g.Count())
        };

        return summary;
    }
}

/// <summary>
/// Lightweight audit result for middleware/diagnostic use
/// </summary>
public class AuditResult
{
    /// <summary>
    /// Whether any issues were found
    /// </summary>
    public bool HasIssues => Issues.Count > 0;

    /// <summary>
    /// Quick issue count
    /// </summary>
    public int IssueCount => Issues.Count;

    /// <summary>
    /// Issues found
    /// </summary>
    public List<AccessibilityIssue> Issues { get; set; } = new();

    /// <summary>
    /// Full report (if generated)
    /// </summary>
    [JsonIgnore]
    public AccessibilityAuditReport? FullReport { get; set; }
}
