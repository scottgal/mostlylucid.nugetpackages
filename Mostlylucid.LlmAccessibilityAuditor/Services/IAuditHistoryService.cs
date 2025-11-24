using Mostlylucid.LlmAccessibilityAuditor.Models;

namespace Mostlylucid.LlmAccessibilityAuditor.Services;

/// <summary>
///     Service for storing and retrieving audit history
/// </summary>
public interface IAuditHistoryService
{
    /// <summary>
    ///     Add a report to the history
    /// </summary>
    void AddReport(AccessibilityAuditReport report);

    /// <summary>
    ///     Get recent reports
    /// </summary>
    IReadOnlyList<AccessibilityAuditReport> GetRecentReports(int count = 10);

    /// <summary>
    ///     Get a specific report by ID
    /// </summary>
    AccessibilityAuditReport? GetReport(string reportId);

    /// <summary>
    ///     Get reports for a specific URL
    /// </summary>
    IReadOnlyList<AccessibilityAuditReport> GetReportsForUrl(string url);

    /// <summary>
    ///     Clear all history
    /// </summary>
    void Clear();

    /// <summary>
    ///     Get aggregate statistics
    /// </summary>
    AuditStatistics GetStatistics();
}

/// <summary>
///     Aggregate statistics from audit history
/// </summary>
public class AuditStatistics
{
    public int TotalAudits { get; set; }
    public int TotalIssuesFound { get; set; }
    public double AverageIssuesPerPage { get; set; }
    public double AverageScore { get; set; }
    public Dictionary<string, int> IssuesByType { get; set; } = new();
    public Dictionary<string, int> IssuesBySeverity { get; set; } = new();
    public int PagesWithCriticalIssues { get; set; }
}