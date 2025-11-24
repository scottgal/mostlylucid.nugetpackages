using System.Diagnostics;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Mostlylucid.LlmAccessibilityAuditor.Models;
using Mostlylucid.LlmAccessibilityAuditor.Telemetry;

namespace Mostlylucid.LlmAccessibilityAuditor.Services;

/// <summary>
///     Main accessibility auditor service that combines rule-based and LLM analysis
/// </summary>
public class AccessibilityAuditor : IAccessibilityAuditor
{
    private readonly IHtmlAccessibilityParser _htmlParser;
    private readonly ILogger<AccessibilityAuditor> _logger;
    private readonly IAccessibilityOllamaClient _ollamaClient;
    private readonly AccessibilityAuditorOptions _options;

    public AccessibilityAuditor(
        IHtmlAccessibilityParser htmlParser,
        IAccessibilityOllamaClient ollamaClient,
        IOptions<AccessibilityAuditorOptions> options,
        ILogger<AccessibilityAuditor> logger)
    {
        _htmlParser = htmlParser;
        _ollamaClient = ollamaClient;
        _options = options.Value;
        _logger = logger;
    }

    public async Task<AccessibilityAuditReport> AuditAsync(
        string html,
        string? pageUrl = null,
        CancellationToken cancellationToken = default)
    {
        using var activity = AccessibilityAuditorTelemetry.StartAuditActivity(pageUrl, html.Length);

        var stopwatch = Stopwatch.StartNew();
        var report = new AccessibilityAuditReport
        {
            PageUrl = pageUrl,
            PageTitle = _htmlParser.ExtractTitle(html),
            HtmlSizeBytes = html.Length
        };

        try
        {
            _logger.LogDebug("Starting accessibility audit for {Url}", pageUrl ?? "unknown page");

            // Check if HTML is too large
            if (html.Length > _options.MaxHtmlSizeBytes)
            {
                _logger.LogWarning("HTML size {Size} exceeds maximum {Max}, truncating",
                    html.Length, _options.MaxHtmlSizeBytes);
                html = html.Substring(0, _options.MaxHtmlSizeBytes);
                report.WasTruncated = true;
            }

            // Step 1: Rule-based analysis
            var parserIssues = await _htmlParser.ParseAndAnalyzeAsync(html, cancellationToken);
            report.Issues.AddRange(parserIssues);

            _logger.LogDebug("HTML parser found {Count} issues", parserIssues.Count);

            // Step 2: LLM analysis (if enabled)
            if (_options.EnableLlmAnalysis)
            {
                var llmAvailable = await _ollamaClient.IsAvailableAsync(cancellationToken);

                if (llmAvailable)
                {
                    report.LlmAnalysisPerformed = true;
                    report.LlmModel = _ollamaClient.ModelName;

                    // Simplify HTML for LLM
                    var simplifiedHtml = _htmlParser.SimplifyForLlm(html, _options.MaxLlmHtmlSizeBytes);

                    // Get LLM analysis
                    var llmIssues = await _ollamaClient.AnalyzeHtmlAsync(
                        simplifiedHtml,
                        parserIssues,
                        cancellationToken);

                    // Deduplicate and add LLM issues
                    var newIssues = DeduplicateIssues(llmIssues, parserIssues);
                    report.Issues.AddRange(newIssues);

                    _logger.LogDebug("LLM analysis found {Count} additional issues", newIssues.Count);

                    // Generate human summary
                    if (report.Issues.Count > 0)
                        report.HumanSummary = await _ollamaClient.GenerateSummaryAsync(
                            report.Issues,
                            cancellationToken);
                }
                else
                {
                    _logger.LogWarning("LLM analysis enabled but Ollama is not available");
                    report.Errors.Add("LLM analysis was enabled but Ollama service was not available");
                }
            }

            // Calculate summary and score
            report.Summary = AuditSummary.FromIssues(report.Issues);
            report.OverallScore = CalculateScore(report.Issues);

            // Generate fallback summary if LLM didn't provide one
            if (string.IsNullOrEmpty(report.HumanSummary)) report.HumanSummary = GenerateFallbackSummary(report);

            AccessibilityAuditorTelemetry.RecordResult(activity, report);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error during accessibility audit");
            report.Errors.Add($"Audit error: {ex.Message}");
            AccessibilityAuditorTelemetry.RecordException(activity, ex);
        }

        stopwatch.Stop();
        report.AuditDurationMs = stopwatch.ElapsedMilliseconds;

        _logger.LogInformation("Accessibility audit completed in {Duration}ms, found {Count} issues",
            report.AuditDurationMs, report.Issues.Count);

        return report;
    }

    public async Task<AuditResult> QuickAuditAsync(
        string html,
        CancellationToken cancellationToken = default)
    {
        using var activity = AccessibilityAuditorTelemetry.StartQuickAuditActivity(html.Length);

        try
        {
            var issues = await _htmlParser.ParseAndAnalyzeAsync(html, cancellationToken);
            var result = new AuditResult { Issues = issues };
            AccessibilityAuditorTelemetry.RecordQuickAuditResult(activity, result);
            return result;
        }
        catch (Exception ex)
        {
            AccessibilityAuditorTelemetry.RecordException(activity, ex);
            throw;
        }
    }

    public async Task<bool> IsReadyAsync(CancellationToken cancellationToken = default)
    {
        if (!_options.EnableLlmAnalysis) return true; // Only rule-based, always ready

        return await _ollamaClient.IsAvailableAsync(cancellationToken);
    }

    private static List<AccessibilityIssue> DeduplicateIssues(
        List<AccessibilityIssue> llmIssues,
        List<AccessibilityIssue> parserIssues)
    {
        var result = new List<AccessibilityIssue>();

        foreach (var llmIssue in llmIssues)
        {
            // Check if a similar issue already exists
            var isDuplicate = parserIssues.Any(p =>
                p.Type == llmIssue.Type &&
                (p.Selector == llmIssue.Selector ||
                 p.Element.Contains(llmIssue.Element) ||
                 llmIssue.Element.Contains(p.Element)));

            if (!isDuplicate) result.Add(llmIssue);
        }

        return result;
    }

    private static int CalculateScore(IReadOnlyList<AccessibilityIssue> issues)
    {
        if (issues.Count == 0) return 100;

        // Start at 100, deduct points based on severity
        var score = 100.0;

        foreach (var issue in issues)
            score -= issue.Severity switch
            {
                IssueSeverity.Critical => 15,
                IssueSeverity.Serious => 8,
                IssueSeverity.Moderate => 4,
                IssueSeverity.Minor => 2,
                IssueSeverity.Info => 0.5,
                _ => 1
            };

        return Math.Max(0, Math.Min(100, (int)Math.Round(score)));
    }

    private static string GenerateFallbackSummary(AccessibilityAuditReport report)
    {
        if (report.Issues.Count == 0)
            return "No accessibility issues were detected. The page appears to follow accessibility best practices.";

        var critical = report.Summary.CriticalCount;
        var serious = report.Summary.SeriousCount;
        var total = report.Summary.TotalIssues;

        if (critical > 0)
            return
                $"Found {total} accessibility issues including {critical} critical issues that may prevent some users from accessing content. Immediate attention recommended.";

        if (serious > 0)
            return
                $"Found {total} accessibility issues including {serious} serious issues that may significantly impact user experience. Review and fix recommended.";

        return
            $"Found {total} accessibility issues. While not critical, addressing these would improve the experience for users of assistive technologies.";
    }
}