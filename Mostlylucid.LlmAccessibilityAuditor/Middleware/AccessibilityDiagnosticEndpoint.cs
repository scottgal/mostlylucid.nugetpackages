using System.Text;
using System.Text.Json;
using System.Web;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Mostlylucid.LlmAccessibilityAuditor.Models;
using Mostlylucid.LlmAccessibilityAuditor.Services;

namespace Mostlylucid.LlmAccessibilityAuditor.Middleware;

/// <summary>
///     Extension methods for mapping the accessibility diagnostic endpoint
/// </summary>
public static class AccessibilityDiagnosticEndpoint
{
    /// <summary>
    ///     Map the accessibility diagnostic endpoint
    /// </summary>
    public static IEndpointRouteBuilder MapAccessibilityDiagnostics(this IEndpointRouteBuilder endpoints)
    {
        var options = endpoints.ServiceProvider.GetService<IOptions<AccessibilityAuditorOptions>>()?.Value
                      ?? new AccessibilityAuditorOptions();

        if (!options.EnableDiagnosticEndpoint) return endpoints;

        var basePath = options.DiagnosticEndpointPath.TrimEnd('/');

        // Main dashboard
        endpoints.MapGet(basePath, async (HttpContext context, IAuditHistoryService historyService) =>
        {
            var reports = historyService.GetRecentReports(20);
            var stats = historyService.GetStatistics();

            var html = GenerateDashboardHtml(reports, stats);
            context.Response.ContentType = "text/html; charset=utf-8";
            await context.Response.WriteAsync(html);
        });

        // Get specific report
        endpoints.MapGet($"{basePath}/report/{{reportId}}",
            async (HttpContext context, IAuditHistoryService historyService, string reportId) =>
            {
                var report = historyService.GetReport(reportId);
                if (report == null)
                {
                    context.Response.StatusCode = 404;
                    await context.Response.WriteAsync("Report not found");
                    return;
                }

                var html = GenerateReportHtml(report);
                context.Response.ContentType = "text/html; charset=utf-8";
                await context.Response.WriteAsync(html);
            });

        // JSON API for reports
        endpoints.MapGet($"{basePath}/api/reports", async (HttpContext context, IAuditHistoryService historyService) =>
        {
            var reports = historyService.GetRecentReports(50);
            context.Response.ContentType = "application/json";
            await context.Response.WriteAsync(JsonSerializer.Serialize(reports, new JsonSerializerOptions
            {
                WriteIndented = true,
                PropertyNamingPolicy = JsonNamingPolicy.CamelCase
            }));
        });

        // JSON API for single report
        endpoints.MapGet($"{basePath}/api/report/{{reportId}}",
            async (HttpContext context, IAuditHistoryService historyService, string reportId) =>
            {
                var report = historyService.GetReport(reportId);
                if (report == null)
                {
                    context.Response.StatusCode = 404;
                    return;
                }

                context.Response.ContentType = "application/json";
                await context.Response.WriteAsync(JsonSerializer.Serialize(report, new JsonSerializerOptions
                {
                    WriteIndented = true,
                    PropertyNamingPolicy = JsonNamingPolicy.CamelCase
                }));
            });

        // Statistics endpoint
        endpoints.MapGet($"{basePath}/api/stats", async (HttpContext context, IAuditHistoryService historyService) =>
        {
            var stats = historyService.GetStatistics();
            context.Response.ContentType = "application/json";
            await context.Response.WriteAsync(JsonSerializer.Serialize(stats, new JsonSerializerOptions
            {
                WriteIndented = true,
                PropertyNamingPolicy = JsonNamingPolicy.CamelCase
            }));
        });

        // Health check
        endpoints.MapGet($"{basePath}/health", async (HttpContext context, IAccessibilityAuditor auditor) =>
        {
            var isReady = await auditor.IsReadyAsync(context.RequestAborted);
            context.Response.StatusCode = isReady ? 200 : 503;
            context.Response.ContentType = "application/json";
            await context.Response.WriteAsync(JsonSerializer.Serialize(new
                { ready = isReady, status = isReady ? "healthy" : "unhealthy" }));
        });

        // Clear history
        endpoints.MapPost($"{basePath}/clear", (IAuditHistoryService historyService) =>
        {
            historyService.Clear();
            return Results.Ok(new { message = "History cleared" });
        });

        return endpoints;
    }

    private static string GenerateDashboardHtml(IReadOnlyList<AccessibilityAuditReport> reports, AuditStatistics stats)
    {
        var sb = new StringBuilder();

        sb.AppendLine(@"<!DOCTYPE html>
<html lang=""en"">
<head>
    <meta charset=""utf-8"">
    <meta name=""viewport"" content=""width=device-width, initial-scale=1"">
    <title>Accessibility Audit Dashboard</title>
    <style>
        * { box-sizing: border-box; margin: 0; padding: 0; }
        body { font-family: system-ui, -apple-system, sans-serif; background: #f3f4f6; color: #1f2937; line-height: 1.5; }
        .container { max-width: 1200px; margin: 0 auto; padding: 24px; }
        h1 { font-size: 24px; margin-bottom: 24px; color: #111827; }
        h2 { font-size: 18px; margin-bottom: 16px; color: #374151; }
        .stats-grid { display: grid; grid-template-columns: repeat(auto-fit, minmax(200px, 1fr)); gap: 16px; margin-bottom: 32px; }
        .stat-card { background: white; border-radius: 8px; padding: 20px; box-shadow: 0 1px 3px rgba(0,0,0,0.1); }
        .stat-value { font-size: 32px; font-weight: 700; color: #111827; }
        .stat-label { font-size: 14px; color: #6b7280; margin-top: 4px; }
        .report-list { background: white; border-radius: 8px; box-shadow: 0 1px 3px rgba(0,0,0,0.1); overflow: hidden; }
        .report-item { display: flex; align-items: center; padding: 16px; border-bottom: 1px solid #e5e7eb; transition: background 0.15s; }
        .report-item:hover { background: #f9fafb; }
        .report-item:last-child { border-bottom: none; }
        .report-score { width: 50px; height: 50px; border-radius: 50%; display: flex; align-items: center; justify-content: center; font-weight: 700; font-size: 14px; margin-right: 16px; }
        .score-good { background: #d1fae5; color: #065f46; }
        .score-ok { background: #fef3c7; color: #92400e; }
        .score-bad { background: #fee2e2; color: #991b1b; }
        .report-info { flex: 1; }
        .report-url { font-weight: 500; color: #111827; word-break: break-all; }
        .report-meta { font-size: 12px; color: #6b7280; margin-top: 4px; }
        .report-issues { display: flex; gap: 6px; }
        .badge { padding: 2px 8px; border-radius: 4px; font-size: 11px; font-weight: 500; }
        .badge-critical { background: #fee2e2; color: #991b1b; }
        .badge-serious { background: #ffedd5; color: #9a3412; }
        .badge-moderate { background: #fef3c7; color: #92400e; }
        .badge-minor { background: #dbeafe; color: #1e40af; }
        a { color: #2563eb; text-decoration: none; }
        a:hover { text-decoration: underline; }
        .empty { padding: 48px; text-align: center; color: #6b7280; }
        .chart-container { background: white; border-radius: 8px; padding: 20px; margin-bottom: 32px; box-shadow: 0 1px 3px rgba(0,0,0,0.1); }
        .bar-chart { display: flex; gap: 4px; align-items: flex-end; height: 100px; margin-top: 16px; }
        .bar { flex: 1; background: #3b82f6; border-radius: 4px 4px 0 0; min-height: 4px; position: relative; }
        .bar-label { position: absolute; bottom: -20px; left: 50%; transform: translateX(-50%); font-size: 10px; color: #6b7280; white-space: nowrap; }
    </style>
</head>
<body>
    <div class=""container"">
        <h1>Accessibility Audit Dashboard</h1>

        <div class=""stats-grid"">
            <div class=""stat-card"">
                <div class=""stat-value"">" + stats.TotalAudits + @"</div>
                <div class=""stat-label"">Total Audits</div>
            </div>
            <div class=""stat-card"">
                <div class=""stat-value"">" + stats.TotalIssuesFound + @"</div>
                <div class=""stat-label"">Issues Found</div>
            </div>
            <div class=""stat-card"">
                <div class=""stat-value"">" + stats.AverageScore.ToString("F0") + @"</div>
                <div class=""stat-label"">Avg Score</div>
            </div>
            <div class=""stat-card"">
                <div class=""stat-value"">" + stats.PagesWithCriticalIssues + @"</div>
                <div class=""stat-label"">Pages with Critical Issues</div>
            </div>
        </div>");

        if (stats.IssuesByType.Count > 0)
        {
            sb.AppendLine(@"
        <div class=""chart-container"">
            <h2>Issues by Type</h2>
            <div class=""bar-chart"">");

            var maxCount = stats.IssuesByType.Values.Max();
            foreach (var (type, count) in stats.IssuesByType.OrderByDescending(x => x.Value).Take(8))
            {
                var height = maxCount > 0 ? count * 100 / maxCount : 0;
                sb.AppendLine(
                    $@"<div class=""bar"" style=""height: {Math.Max(4, height)}%;"" title=""{type}: {count}""><span class=""bar-label"">{type.Replace("Missing", "").Replace("No", "")}</span></div>");
            }

            sb.AppendLine(@"</div></div>");
        }

        sb.AppendLine(@"
        <h2>Recent Audits</h2>
        <div class=""report-list"">");

        if (reports.Count == 0)
            sb.AppendLine(
                @"<div class=""empty"">No audits yet. Browse some pages with the middleware enabled to see results here.</div>");
        else
            foreach (var report in reports)
            {
                var scoreClass = report.OverallScore >= 80 ? "score-good" :
                    report.OverallScore >= 50 ? "score-ok" : "score-bad";

                sb.AppendLine($@"
            <a href=""/_accessibility/report/{report.ReportId}"" class=""report-item"">
                <div class=""report-score {scoreClass}"">{report.OverallScore ?? 0}</div>
                <div class=""report-info"">
                    <div class=""report-url"">{HttpUtility.HtmlEncode(report.PageUrl ?? "Unknown")}</div>
                    <div class=""report-meta"">{report.AuditedAt:g} | {report.AuditDurationMs}ms | {report.Summary.TotalIssues} issues</div>
                </div>
                <div class=""report-issues"">
                    " + (report.Summary.CriticalCount > 0
                    ? $@"<span class=""badge badge-critical"">{report.Summary.CriticalCount} critical</span>"
                    : "") + @"
                    " + (report.Summary.SeriousCount > 0
                    ? $@"<span class=""badge badge-serious"">{report.Summary.SeriousCount} serious</span>"
                    : "") + @"
                    " + (report.Summary.ModerateCount > 0
                    ? $@"<span class=""badge badge-moderate"">{report.Summary.ModerateCount} moderate</span>"
                    : "") + @"
                </div>
            </a>");
            }

        sb.AppendLine(@"
        </div>
    </div>
</body>
</html>");

        return sb.ToString();
    }

    private static string GenerateReportHtml(AccessibilityAuditReport report)
    {
        var sb = new StringBuilder();

        var severityColors = new Dictionary<IssueSeverity, (string bg, string text)>
        {
            [IssueSeverity.Critical] = ("#fee2e2", "#991b1b"),
            [IssueSeverity.Serious] = ("#ffedd5", "#9a3412"),
            [IssueSeverity.Moderate] = ("#fef3c7", "#92400e"),
            [IssueSeverity.Minor] = ("#dbeafe", "#1e40af"),
            [IssueSeverity.Info] = ("#f3f4f6", "#374151")
        };

        sb.AppendLine($@"<!DOCTYPE html>
<html lang=""en"">
<head>
    <meta charset=""utf-8"">
    <meta name=""viewport"" content=""width=device-width, initial-scale=1"">
    <title>Audit Report - {HttpUtility.HtmlEncode(report.PageTitle ?? report.PageUrl ?? "Unknown")}</title>
    <style>
        * {{ box-sizing: border-box; margin: 0; padding: 0; }}
        body {{ font-family: system-ui, -apple-system, sans-serif; background: #f3f4f6; color: #1f2937; line-height: 1.5; }}
        .container {{ max-width: 1000px; margin: 0 auto; padding: 24px; }}
        h1 {{ font-size: 24px; margin-bottom: 8px; color: #111827; }}
        h2 {{ font-size: 18px; margin: 24px 0 16px; color: #374151; }}
        .back {{ color: #2563eb; text-decoration: none; font-size: 14px; display: inline-block; margin-bottom: 16px; }}
        .back:hover {{ text-decoration: underline; }}
        .header {{ background: white; border-radius: 8px; padding: 24px; margin-bottom: 24px; box-shadow: 0 1px 3px rgba(0,0,0,0.1); }}
        .header-meta {{ font-size: 14px; color: #6b7280; margin-bottom: 16px; }}
        .score-display {{ display: flex; align-items: center; gap: 24px; }}
        .score-circle {{ width: 80px; height: 80px; border-radius: 50%; display: flex; align-items: center; justify-content: center; font-size: 24px; font-weight: 700; }}
        .score-good {{ background: #d1fae5; color: #065f46; }}
        .score-ok {{ background: #fef3c7; color: #92400e; }}
        .score-bad {{ background: #fee2e2; color: #991b1b; }}
        .summary {{ flex: 1; padding: 16px; background: #f9fafb; border-radius: 8px; }}
        .issue-card {{ background: white; border-radius: 8px; margin-bottom: 12px; overflow: hidden; box-shadow: 0 1px 3px rgba(0,0,0,0.1); }}
        .issue-header {{ padding: 12px 16px; display: flex; align-items: center; gap: 12px; cursor: pointer; }}
        .issue-header:hover {{ background: #f9fafb; }}
        .issue-severity {{ padding: 2px 8px; border-radius: 4px; font-size: 11px; font-weight: 600; text-transform: uppercase; }}
        .issue-type {{ font-weight: 600; color: #111827; }}
        .issue-desc {{ flex: 1; color: #4b5563; font-size: 14px; }}
        .issue-details {{ padding: 16px; border-top: 1px solid #e5e7eb; background: #f9fafb; display: none; }}
        .issue-details.open {{ display: block; }}
        .detail-row {{ margin-bottom: 12px; }}
        .detail-label {{ font-size: 11px; font-weight: 600; color: #6b7280; text-transform: uppercase; margin-bottom: 4px; }}
        .detail-value {{ font-size: 13px; color: #1f2937; }}
        .code {{ background: #1f2937; color: #e5e7eb; padding: 12px; border-radius: 4px; font-family: monospace; font-size: 12px; overflow-x: auto; white-space: pre-wrap; word-break: break-all; }}
        .fix {{ color: #059669; background: #d1fae5; padding: 8px 12px; border-radius: 4px; }}
        .wcag {{ font-size: 12px; color: #6b7280; }}
    </style>
    <script>
        function toggleDetails(id) {{
            var el = document.getElementById('details-' + id);
            el.classList.toggle('open');
        }}
    </script>
</head>
<body>
    <div class=""container"">
        <a href=""/_accessibility"" class=""back"">Back to Dashboard</a>

        <div class=""header"">
            <h1>{HttpUtility.HtmlEncode(report.PageTitle ?? "Untitled Page")}</h1>
            <div class=""header-meta"">
                <strong>URL:</strong> {HttpUtility.HtmlEncode(report.PageUrl ?? "Unknown")} |
                <strong>Audited:</strong> {report.AuditedAt:g} |
                <strong>Duration:</strong> {report.AuditDurationMs}ms |
                <strong>LLM:</strong> {(report.LlmAnalysisPerformed ? $"Yes ({report.LlmModel})" : "No")}
            </div>
            <div class=""score-display"">
                <div class=""score-circle {(report.OverallScore >= 80 ? "score-good" : report.OverallScore >= 50 ? "score-ok" : "score-bad")}"">{report.OverallScore ?? 0}</div>
                <div class=""summary"">{HttpUtility.HtmlEncode(report.HumanSummary ?? "")}</div>
            </div>
        </div>

        <h2>Issues ({report.Issues.Count})</h2>");

        var issueIndex = 0;
        foreach (var issue in report.Issues.OrderBy(i => i.Severity))
        {
            var (bg, text) = severityColors[issue.Severity];

            sb.AppendLine($@"
        <div class=""issue-card"">
            <div class=""issue-header"" onclick=""toggleDetails({issueIndex})"">
                <span class=""issue-severity"" style=""background: {bg}; color: {text};"">{issue.Severity}</span>
                <span class=""issue-type"">{issue.Type}</span>
                <span class=""issue-desc"">{HttpUtility.HtmlEncode(issue.Description)}</span>
            </div>
            <div class=""issue-details"" id=""details-{issueIndex}"">
                " + (!string.IsNullOrEmpty(issue.Element)
                ? $@"
                <div class=""detail-row"">
                    <div class=""detail-label"">Element</div>
                    <div class=""code"">{HttpUtility.HtmlEncode(issue.Element)}</div>
                </div>"
                : "") + @"

                " + (!string.IsNullOrEmpty(issue.Selector)
                ? $@"
                <div class=""detail-row"">
                    <div class=""detail-label"">Selector</div>
                    <div class=""detail-value""><code>{HttpUtility.HtmlEncode(issue.Selector)}</code></div>
                </div>"
                : "") + @"

                " + (!string.IsNullOrEmpty(issue.SuggestedFix)
                ? $@"
                <div class=""detail-row"">
                    <div class=""detail-label"">Suggested Fix</div>
                    <div class=""fix"">{HttpUtility.HtmlEncode(issue.SuggestedFix)}</div>
                </div>"
                : "") + @"

                " + (!string.IsNullOrEmpty(issue.WcagReference)
                ? $@"
                <div class=""detail-row"">
                    <div class=""wcag"">WCAG {issue.WcagReference} (Level {issue.WcagLevel ?? "?"}) | Source: {issue.Source}" +
                  (issue.Confidence.HasValue ? $" | Confidence: {issue.Confidence:P0}" : "") + @"</div>
                </div>"
                : "") + @"
            </div>
        </div>");
            issueIndex++;
        }

        sb.AppendLine(@"
    </div>
</body>
</html>");

        return sb.ToString();
    }
}