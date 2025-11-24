using System.Diagnostics;
using Mostlylucid.LlmAccessibilityAuditor.Models;

namespace Mostlylucid.LlmAccessibilityAuditor.Telemetry;

/// <summary>
///     Telemetry instrumentation for accessibility auditor operations
/// </summary>
public static class AccessibilityAuditorTelemetry
{
    /// <summary>
    ///     Activity source name for accessibility auditor
    /// </summary>
    public const string ActivitySourceName = "Mostlylucid.LlmAccessibilityAuditor";

    /// <summary>
    ///     Activity source for accessibility auditor telemetry
    /// </summary>
    public static readonly ActivitySource ActivitySource = new(ActivitySourceName, GetVersion());

    private static string GetVersion()
    {
        return typeof(AccessibilityAuditorTelemetry).Assembly.GetName().Version?.ToString() ?? "1.0.0";
    }

    /// <summary>
    ///     Starts an activity for a full accessibility audit
    /// </summary>
    public static Activity? StartAuditActivity(string? pageUrl = null, int? htmlLength = null)
    {
        var activity = ActivitySource.StartActivity("AccessibilityAuditor.Audit");

        if (activity != null)
        {
            if (pageUrl != null)
                activity.SetTag("accessibility.page_url", pageUrl);
            if (htmlLength.HasValue)
                activity.SetTag("accessibility.html_length", htmlLength.Value);
        }

        return activity;
    }

    /// <summary>
    ///     Starts an activity for a quick accessibility audit
    /// </summary>
    public static Activity? StartQuickAuditActivity(int? htmlLength = null)
    {
        var activity = ActivitySource.StartActivity("AccessibilityAuditor.QuickAudit");

        if (activity != null)
            if (htmlLength.HasValue)
                activity.SetTag("accessibility.html_length", htmlLength.Value);

        return activity;
    }

    /// <summary>
    ///     Records accessibility audit result on the activity
    /// </summary>
    public static void RecordResult(Activity? activity, AccessibilityAuditReport report)
    {
        if (activity == null)
            return;

        activity.SetTag("accessibility.issue_count", report.Summary.TotalIssues);
        activity.SetTag("accessibility.critical_count", report.Summary.CriticalCount);
        activity.SetTag("accessibility.serious_count", report.Summary.SeriousCount);
        activity.SetTag("accessibility.moderate_count", report.Summary.ModerateCount);
        activity.SetTag("accessibility.minor_count", report.Summary.MinorCount);
        activity.SetTag("accessibility.info_count", report.Summary.InfoCount);
        activity.SetTag("accessibility.overall_score", report.OverallScore);
        activity.SetTag("accessibility.audit_duration_ms", report.AuditDurationMs);
        activity.SetTag("accessibility.html_size_bytes", report.HtmlSizeBytes);
        activity.SetTag("accessibility.llm_analysis_performed", report.LlmAnalysisPerformed);
        activity.SetTag("accessibility.was_truncated", report.WasTruncated);

        if (!string.IsNullOrEmpty(report.LlmModel))
            activity.SetTag("accessibility.llm_model", report.LlmModel);

        if (!string.IsNullOrEmpty(report.PageUrl))
            activity.SetTag("accessibility.page_url", report.PageUrl);

        // Record max severity level
        var maxSeverity = GetMaxSeverity(report.Summary);
        if (maxSeverity != null)
            activity.SetTag("accessibility.max_severity", maxSeverity);

        if (report.Errors.Count > 0)
            activity.SetTag("accessibility.error_count", report.Errors.Count);

        activity.SetStatus(ActivityStatusCode.Ok);
    }

    /// <summary>
    ///     Records quick audit result on the activity
    /// </summary>
    public static void RecordQuickAuditResult(Activity? activity, AuditResult result)
    {
        if (activity == null)
            return;

        activity.SetTag("accessibility.issue_count", result.IssueCount);
        activity.SetTag("accessibility.has_issues", result.HasIssues);

        if (result.Issues.Count > 0)
        {
            var criticalCount = result.Issues.Count(i => i.Severity == IssueSeverity.Critical);
            var seriousCount = result.Issues.Count(i => i.Severity == IssueSeverity.Serious);

            activity.SetTag("accessibility.critical_count", criticalCount);
            activity.SetTag("accessibility.serious_count", seriousCount);

            // Record max severity
            var maxSeverity = result.Issues.Min(i => i.Severity);
            activity.SetTag("accessibility.max_severity", maxSeverity.ToString());
        }

        activity.SetStatus(ActivityStatusCode.Ok);
    }

    /// <summary>
    ///     Records an exception on the activity
    /// </summary>
    public static void RecordException(Activity? activity, Exception ex)
    {
        if (activity == null)
            return;

        activity.SetStatus(ActivityStatusCode.Error, ex.Message);
        activity.SetTag("exception.type", ex.GetType().FullName);
        activity.SetTag("exception.message", ex.Message);
    }

    private static string? GetMaxSeverity(AuditSummary summary)
    {
        if (summary.CriticalCount > 0)
            return IssueSeverity.Critical.ToString();
        if (summary.SeriousCount > 0)
            return IssueSeverity.Serious.ToString();
        if (summary.ModerateCount > 0)
            return IssueSeverity.Moderate.ToString();
        if (summary.MinorCount > 0)
            return IssueSeverity.Minor.ToString();
        if (summary.InfoCount > 0)
            return IssueSeverity.Info.ToString();
        return null;
    }
}