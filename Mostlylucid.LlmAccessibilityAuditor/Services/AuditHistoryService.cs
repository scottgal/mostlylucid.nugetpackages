using System.Collections.Concurrent;
using Microsoft.Extensions.Options;
using Mostlylucid.LlmAccessibilityAuditor.Models;

namespace Mostlylucid.LlmAccessibilityAuditor.Services;

/// <summary>
/// In-memory service for storing audit history
/// </summary>
public class AuditHistoryService : IAuditHistoryService
{
    private readonly ConcurrentQueue<AccessibilityAuditReport> _reports = new();
    private readonly ConcurrentDictionary<string, AccessibilityAuditReport> _reportById = new();
    private readonly AccessibilityAuditorOptions _options;
    private readonly object _lock = new();

    public AuditHistoryService(IOptions<AccessibilityAuditorOptions> options)
    {
        _options = options.Value;
    }

    public void AddReport(AccessibilityAuditReport report)
    {
        if (!_options.StoreAuditHistory) return;

        _reports.Enqueue(report);
        _reportById[report.ReportId] = report;

        // Trim if over limit
        while (_reports.Count > _options.MaxHistoryCount)
        {
            if (_reports.TryDequeue(out var old))
            {
                _reportById.TryRemove(old.ReportId, out _);
            }
        }
    }

    public IReadOnlyList<AccessibilityAuditReport> GetRecentReports(int count = 10)
    {
        return _reports
            .Reverse()
            .Take(count)
            .ToList();
    }

    public AccessibilityAuditReport? GetReport(string reportId)
    {
        _reportById.TryGetValue(reportId, out var report);
        return report;
    }

    public IReadOnlyList<AccessibilityAuditReport> GetReportsForUrl(string url)
    {
        return _reports
            .Where(r => r.PageUrl?.Equals(url, StringComparison.OrdinalIgnoreCase) == true)
            .OrderByDescending(r => r.AuditedAt)
            .ToList();
    }

    public void Clear()
    {
        lock (_lock)
        {
            while (_reports.TryDequeue(out _)) { }
            _reportById.Clear();
        }
    }

    public AuditStatistics GetStatistics()
    {
        var reports = _reports.ToList();
        var allIssues = reports.SelectMany(r => r.Issues).ToList();

        return new AuditStatistics
        {
            TotalAudits = reports.Count,
            TotalIssuesFound = allIssues.Count,
            AverageIssuesPerPage = reports.Count > 0 ? (double)allIssues.Count / reports.Count : 0,
            AverageScore = reports.Count > 0 ? reports.Where(r => r.OverallScore.HasValue).Average(r => r.OverallScore!.Value) : 0,
            IssuesByType = allIssues
                .GroupBy(i => i.Type.ToString())
                .ToDictionary(g => g.Key, g => g.Count()),
            IssuesBySeverity = allIssues
                .GroupBy(i => i.Severity.ToString())
                .ToDictionary(g => g.Key, g => g.Count()),
            PagesWithCriticalIssues = reports.Count(r => r.Issues.Any(i => i.Severity == IssueSeverity.Critical))
        };
    }
}
