using System.Text;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Razor.TagHelpers;
using Mostlylucid.LlmAccessibilityAuditor.Models;

namespace Mostlylucid.LlmAccessibilityAuditor.TagHelpers;

/// <summary>
/// TagHelper that displays accessibility warnings from the audit report
/// Usage: &lt;accessibility-warnings /&gt;
/// </summary>
[HtmlTargetElement("accessibility-warnings")]
public class AccessibilityWarningsTagHelper : TagHelper
{
    private readonly IHttpContextAccessor _httpContextAccessor;

    public AccessibilityWarningsTagHelper(IHttpContextAccessor httpContextAccessor)
    {
        _httpContextAccessor = httpContextAccessor;
    }

    /// <summary>
    /// Minimum severity to display (default: Moderate)
    /// </summary>
    [HtmlAttributeName("min-severity")]
    public IssueSeverity MinSeverity { get; set; } = IssueSeverity.Moderate;

    /// <summary>
    /// Maximum number of issues to show (default: 5)
    /// </summary>
    [HtmlAttributeName("max-issues")]
    public int MaxIssues { get; set; } = 5;

    /// <summary>
    /// Show as inline element (default: false - shows as floating widget)
    /// </summary>
    [HtmlAttributeName("inline")]
    public bool Inline { get; set; } = false;

    /// <summary>
    /// Custom CSS class
    /// </summary>
    [HtmlAttributeName("class")]
    public string? CssClass { get; set; }

    public override void Process(TagHelperContext context, TagHelperOutput output)
    {
        var httpContext = _httpContextAccessor.HttpContext;
        if (httpContext == null)
        {
            output.SuppressOutput();
            return;
        }

        // Get audit report from HttpContext.Items (set by middleware)
        if (!httpContext.Items.TryGetValue("AccessibilityAuditReport", out var reportObj) ||
            reportObj is not AccessibilityAuditReport report)
        {
            output.SuppressOutput();
            return;
        }

        var issues = report.Issues
            .Where(i => i.Severity <= MinSeverity)
            .Take(MaxIssues)
            .ToList();

        if (issues.Count == 0)
        {
            output.SuppressOutput();
            return;
        }

        output.TagName = "div";
        output.TagMode = TagMode.StartTagAndEndTag;

        var sb = new StringBuilder();

        if (Inline)
        {
            output.Attributes.SetAttribute("class", $"a11y-warnings-inline {CssClass}".Trim());
            sb.Append(GenerateInlineContent(issues, report));
        }
        else
        {
            output.Attributes.SetAttribute("class", $"a11y-warnings-widget {CssClass}".Trim());
            sb.Append(GenerateWidgetContent(issues, report));
        }

        output.Content.SetHtmlContent(sb.ToString());
    }

    private static string GenerateInlineContent(List<AccessibilityIssue> issues, AccessibilityAuditReport report)
    {
        var sb = new StringBuilder();

        sb.AppendLine(@"<style>
            .a11y-warnings-inline { border: 2px solid #fbbf24; background: #fffbeb; border-radius: 8px; padding: 16px; margin: 16px 0; font-family: system-ui, sans-serif; }
            .a11y-warnings-inline h4 { margin: 0 0 12px; color: #92400e; font-size: 14px; }
            .a11y-warnings-inline ul { margin: 0; padding-left: 20px; }
            .a11y-warnings-inline li { margin-bottom: 8px; font-size: 13px; color: #78350f; }
            .a11y-warnings-inline .severity { font-weight: 600; text-transform: uppercase; font-size: 10px; padding: 2px 6px; border-radius: 3px; margin-right: 6px; }
            .a11y-warnings-inline .severity-critical { background: #fee2e2; color: #991b1b; }
            .a11y-warnings-inline .severity-serious { background: #ffedd5; color: #9a3412; }
            .a11y-warnings-inline .severity-moderate { background: #fef3c7; color: #92400e; }
        </style>");

        sb.AppendLine($@"<h4>Accessibility Warnings ({report.Summary.TotalIssues} total, score: {report.OverallScore}/100)</h4>");
        sb.AppendLine("<ul>");

        foreach (var issue in issues)
        {
            var severityClass = issue.Severity switch
            {
                IssueSeverity.Critical => "severity-critical",
                IssueSeverity.Serious => "severity-serious",
                _ => "severity-moderate"
            };

            sb.AppendLine($@"<li><span class=""severity {severityClass}"">{issue.Severity}</span> <strong>{issue.Type}</strong>: {System.Web.HttpUtility.HtmlEncode(issue.Description)}</li>");
        }

        sb.AppendLine("</ul>");

        if (report.Issues.Count > issues.Count)
        {
            sb.AppendLine($@"<p style=""font-size: 12px; color: #92400e; margin-top: 8px;"">...and {report.Issues.Count - issues.Count} more issues. <a href=""/_accessibility/report/{report.ReportId}"" style=""color: #b45309;"">View full report</a></p>");
        }

        return sb.ToString();
    }

    private static string GenerateWidgetContent(List<AccessibilityIssue> issues, AccessibilityAuditReport report)
    {
        var sb = new StringBuilder();

        var badgeColor = report.Summary.CriticalCount > 0 ? "#dc2626"
            : report.Summary.SeriousCount > 0 ? "#ea580c"
            : "#ca8a04";

        sb.AppendLine($@"<style>
            .a11y-warnings-widget {{ position: fixed; bottom: 80px; right: 20px; z-index: 999998; font-family: system-ui, sans-serif; font-size: 13px; }}
            .a11y-widget-badge {{ background: {badgeColor}; color: white; padding: 8px 12px; border-radius: 20px; cursor: pointer; display: flex; align-items: center; gap: 6px; box-shadow: 0 2px 8px rgba(0,0,0,0.2); }}
            .a11y-widget-badge:hover {{ filter: brightness(1.1); }}
            .a11y-widget-popup {{ display: none; position: absolute; bottom: 40px; right: 0; width: 300px; background: white; border-radius: 8px; box-shadow: 0 4px 16px rgba(0,0,0,0.2); overflow: hidden; }}
            .a11y-widget-popup.open {{ display: block; }}
            .a11y-widget-header {{ background: #1f2937; color: white; padding: 10px 14px; font-weight: 600; }}
            .a11y-widget-body {{ padding: 12px; max-height: 250px; overflow-y: auto; }}
            .a11y-widget-item {{ border-left: 3px solid #fbbf24; padding: 6px 10px; margin-bottom: 8px; background: #f9fafb; font-size: 12px; }}
            .a11y-widget-item.critical {{ border-color: #dc2626; }}
            .a11y-widget-item.serious {{ border-color: #ea580c; }}
            .a11y-widget-link {{ display: block; text-align: center; padding: 10px; background: #f3f4f6; color: #2563eb; text-decoration: none; font-size: 12px; }}
        </style>");

        var popupId = $"a11y-popup-{Guid.NewGuid():N}";

        sb.AppendLine($@"
        <div class=""a11y-widget-badge"" onclick=""document.getElementById('{popupId}').classList.toggle('open')"">
            <svg width=""16"" height=""16"" viewBox=""0 0 24 24"" fill=""none"" stroke=""currentColor"" stroke-width=""2"">
                <circle cx=""12"" cy=""12"" r=""10""/><line x1=""12"" y1=""8"" x2=""12"" y2=""12""/><line x1=""12"" y1=""16"" x2=""12.01"" y2=""16""/>
            </svg>
            {report.Summary.TotalIssues} A11y Issues
        </div>
        <div class=""a11y-widget-popup"" id=""{popupId}"">
            <div class=""a11y-widget-header"">Accessibility Score: {report.OverallScore}/100</div>
            <div class=""a11y-widget-body"">");

        foreach (var issue in issues)
        {
            var itemClass = issue.Severity == IssueSeverity.Critical ? "critical" : issue.Severity == IssueSeverity.Serious ? "serious" : "";
            sb.AppendLine($@"<div class=""a11y-widget-item {itemClass}""><strong>{issue.Type}</strong><br>{System.Web.HttpUtility.HtmlEncode(issue.Description)}</div>");
        }

        sb.AppendLine($@"
            </div>
            <a href=""/_accessibility/report/{report.ReportId}"" class=""a11y-widget-link"">View Full Report</a>
        </div>");

        return sb.ToString();
    }
}
