using System.Text;
using System.Web;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Mostlylucid.LlmAccessibilityAuditor.Models;
using Mostlylucid.LlmAccessibilityAuditor.Services;

namespace Mostlylucid.LlmAccessibilityAuditor.Middleware;

/// <summary>
///     Middleware that intercepts HTML responses and performs accessibility audits
/// </summary>
public class AccessibilityAuditMiddleware
{
    private readonly bool _isDevelopment;
    private readonly ILogger<AccessibilityAuditMiddleware> _logger;
    private readonly RequestDelegate _next;
    private readonly AccessibilityAuditorOptions _options;

    public AccessibilityAuditMiddleware(
        RequestDelegate next,
        ILogger<AccessibilityAuditMiddleware> logger,
        IOptions<AccessibilityAuditorOptions> options,
        IHostEnvironment hostEnvironment)
    {
        _next = next;
        _logger = logger;
        _options = options.Value;
        _isDevelopment = hostEnvironment.IsDevelopment();
    }

    public async Task InvokeAsync(
        HttpContext context,
        IAccessibilityAuditor auditor,
        IAuditHistoryService historyService)
    {
        // Check if auditing should run for this request
        if (!ShouldAudit(context))
        {
            await _next(context);
            return;
        }

        // Capture the response
        var originalBodyStream = context.Response.Body;
        using var responseBody = new MemoryStream();
        context.Response.Body = responseBody;

        try
        {
            await _next(context);

            // Check if response is HTML
            var contentType = context.Response.ContentType?.ToLowerInvariant() ?? "";
            if (!_options.ContentTypesToAudit.Any(ct => contentType.Contains(ct)))
            {
                // Not HTML, just copy through
                responseBody.Seek(0, SeekOrigin.Begin);
                await responseBody.CopyToAsync(originalBodyStream);
                return;
            }

            // Read the HTML content
            responseBody.Seek(0, SeekOrigin.Begin);
            var html = await new StreamReader(responseBody).ReadToEndAsync();

            // Perform audit
            var requestPath = context.Request.Path.ToString();
            var requestUrl = $"{context.Request.Scheme}://{context.Request.Host}{requestPath}";

            var report = await auditor.AuditAsync(html, requestUrl, context.RequestAborted);

            // Store in history
            historyService.AddReport(report);

            // Store result in HttpContext for TagHelper access
            context.Items["AccessibilityAuditReport"] = report;

            // Optionally inject inline report
            if (_options.EnableInlineReport && report.Issues.Count > 0) html = InjectInlineReport(html, report);

            // Write the (possibly modified) response
            context.Response.Body = originalBodyStream;
            context.Response.ContentLength = null; // Reset since we may have modified content

            await context.Response.WriteAsync(html);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error in accessibility audit middleware");

            // On error, write original response
            responseBody.Seek(0, SeekOrigin.Begin);
            context.Response.Body = originalBodyStream;
            await responseBody.CopyToAsync(originalBodyStream);
        }
    }

    private bool ShouldAudit(HttpContext context)
    {
        if (!_options.Enabled) return false;

        // Check environment
        if (_options.OnlyInDevelopment && !_isDevelopment)
        {
            // Check for override header
            if (!string.IsNullOrEmpty(_options.EnableHeader))
                if (context.Request.Headers.TryGetValue(_options.EnableHeader, out var headerValue))
                    if (headerValue.ToString().Equals(_options.EnableHeaderValue, StringComparison.OrdinalIgnoreCase))
                        return true;

            return false;
        }

        // Check path exclusions
        var path = context.Request.Path.ToString();

        // Skip diagnostic endpoint itself
        if (path.Equals(_options.DiagnosticEndpointPath, StringComparison.OrdinalIgnoreCase))
            return false;

        // Check exclude paths
        foreach (var excludePath in _options.ExcludePaths)
            if (MatchesPath(path, excludePath))
                return false;

        // Check include paths (if specified)
        if (_options.IncludePaths != null && _options.IncludePaths.Count > 0)
            return _options.IncludePaths.Any(p => MatchesPath(path, p));

        // Only audit GET requests by default
        return context.Request.Method == HttpMethods.Get;
    }

    private static bool MatchesPath(string path, string pattern)
    {
        if (pattern.EndsWith("*"))
        {
            var prefix = pattern[..^1];
            return path.StartsWith(prefix, StringComparison.OrdinalIgnoreCase);
        }

        return path.Equals(pattern, StringComparison.OrdinalIgnoreCase);
    }

    private static string InjectInlineReport(string html, AccessibilityAuditReport report)
    {
        var widget = GenerateInlineWidget(report);

        // Try to inject before </body>
        var bodyCloseIndex = html.LastIndexOf("</body>", StringComparison.OrdinalIgnoreCase);
        if (bodyCloseIndex >= 0) return html.Insert(bodyCloseIndex, widget);

        // Fallback: append at end
        return html + widget;
    }

    private static string GenerateInlineWidget(AccessibilityAuditReport report)
    {
        var severityColors = new Dictionary<IssueSeverity, string>
        {
            [IssueSeverity.Critical] = "#dc2626",
            [IssueSeverity.Serious] = "#ea580c",
            [IssueSeverity.Moderate] = "#ca8a04",
            [IssueSeverity.Minor] = "#2563eb",
            [IssueSeverity.Info] = "#6b7280"
        };

        var badgeColor = report.Summary.CriticalCount > 0 ? severityColors[IssueSeverity.Critical]
            : report.Summary.SeriousCount > 0 ? severityColors[IssueSeverity.Serious]
            : report.Summary.ModerateCount > 0 ? severityColors[IssueSeverity.Moderate]
            : severityColors[IssueSeverity.Info];

        var sb = new StringBuilder();

        sb.AppendLine(@"
<!-- Accessibility Audit Widget -->
<div id=""a11y-audit-widget"" style=""
    position: fixed;
    bottom: 20px;
    right: 20px;
    z-index: 999999;
    font-family: system-ui, -apple-system, sans-serif;
    font-size: 14px;
"">
    <button id=""a11y-audit-toggle"" onclick=""document.getElementById('a11y-audit-panel').style.display = document.getElementById('a11y-audit-panel').style.display === 'none' ? 'block' : 'none'"" style=""
        background: " + badgeColor + @";
        color: white;
        border: none;
        border-radius: 50%;
        width: 50px;
        height: 50px;
        cursor: pointer;
        box-shadow: 0 4px 12px rgba(0,0,0,0.3);
        font-weight: bold;
        font-size: 16px;
    "" title=""Accessibility Audit: " + report.Issues.Count + @" issues found"">
        " + report.Issues.Count + @"
    </button>

    <div id=""a11y-audit-panel"" style=""
        display: none;
        position: absolute;
        bottom: 60px;
        right: 0;
        width: 400px;
        max-height: 500px;
        background: white;
        border-radius: 8px;
        box-shadow: 0 4px 20px rgba(0,0,0,0.2);
        overflow: hidden;
    "">
        <div style=""
            background: #1f2937;
            color: white;
            padding: 12px 16px;
            font-weight: 600;
            display: flex;
            justify-content: space-between;
            align-items: center;
        "">
            <span>Accessibility Audit</span>
            <span style=""
                background: " + badgeColor + @";
                padding: 2px 8px;
                border-radius: 12px;
                font-size: 12px;
            "">Score: " + (report.OverallScore ?? 0) + @"/100</span>
        </div>

        <div style=""padding: 16px; overflow-y: auto; max-height: 400px;"">
            <div style=""margin-bottom: 12px; padding: 8px; background: #f3f4f6; border-radius: 4px; font-size: 12px;"">
                " + HttpUtility.HtmlEncode(report.HumanSummary ?? "") + @"
            </div>

            <div style=""display: flex; gap: 8px; margin-bottom: 12px; flex-wrap: wrap;"">
                " + (report.Summary.CriticalCount > 0
            ? $@"<span style=""background: {severityColors[IssueSeverity.Critical]}; color: white; padding: 2px 8px; border-radius: 4px; font-size: 11px;"">Critical: {report.Summary.CriticalCount}</span>"
            : "") + @"
                " + (report.Summary.SeriousCount > 0
            ? $@"<span style=""background: {severityColors[IssueSeverity.Serious]}; color: white; padding: 2px 8px; border-radius: 4px; font-size: 11px;"">Serious: {report.Summary.SeriousCount}</span>"
            : "") + @"
                " + (report.Summary.ModerateCount > 0
            ? $@"<span style=""background: {severityColors[IssueSeverity.Moderate]}; color: white; padding: 2px 8px; border-radius: 4px; font-size: 11px;"">Moderate: {report.Summary.ModerateCount}</span>"
            : "") + @"
                " + (report.Summary.MinorCount > 0
            ? $@"<span style=""background: {severityColors[IssueSeverity.Minor]}; color: white; padding: 2px 8px; border-radius: 4px; font-size: 11px;"">Minor: {report.Summary.MinorCount}</span>"
            : "") + @"
            </div>
");

        // Add issue list
        sb.AppendLine(@"<div style=""font-size: 12px;"">");

        foreach (var issue in report.Issues.Take(15))
        {
            var color = severityColors[issue.Severity];
            sb.AppendLine($@"
                <div style=""border-left: 3px solid {color}; padding: 8px; margin-bottom: 8px; background: #f9fafb;"">
                    <div style=""font-weight: 600; color: {color};"">{issue.Type}</div>
                    <div style=""color: #374151; margin: 4px 0;"">{HttpUtility.HtmlEncode(issue.Description)}</div>
                    " + (!string.IsNullOrEmpty(issue.SuggestedFix)
                ? $@"<div style=""color: #059669; font-size: 11px;"">Fix: {HttpUtility.HtmlEncode(issue.SuggestedFix)}</div>"
                : "") + @"
                    " + (!string.IsNullOrEmpty(issue.WcagReference)
                ? $@"<div style=""color: #6b7280; font-size: 10px; margin-top: 4px;"">WCAG {issue.WcagReference} ({issue.WcagLevel ?? ""})</div>"
                : "") + @"
                </div>
            ");
        }

        if (report.Issues.Count > 15)
            sb.AppendLine(
                $@"<div style=""text-align: center; color: #6b7280; padding: 8px;"">...and {report.Issues.Count - 15} more issues</div>");

        sb.AppendLine(@"</div></div></div></div>
<!-- End Accessibility Audit Widget -->");

        return sb.ToString();
    }
}