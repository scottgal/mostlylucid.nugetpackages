using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Mostlylucid.LlmAccessibilityAuditor.Models;

namespace Mostlylucid.LlmAccessibilityAuditor.Services;

/// <summary>
/// Client for Ollama LLM API for accessibility analysis
/// </summary>
public class AccessibilityOllamaClient : IAccessibilityOllamaClient
{
    private readonly AccessibilityAuditorOptions _options;
    private readonly HttpClient _httpClient;
    private readonly ILogger<AccessibilityOllamaClient> _logger;

    public AccessibilityOllamaClient(
        ILogger<AccessibilityOllamaClient> logger,
        HttpClient httpClient,
        IOptions<AccessibilityAuditorOptions> options)
    {
        _logger = logger;
        _httpClient = httpClient;
        _options = options.Value;

        _httpClient.BaseAddress = new Uri(_options.Ollama.Endpoint);
        _httpClient.Timeout = TimeSpan.FromSeconds(_options.Ollama.TimeoutSeconds);
    }

    public string ModelName => _options.Ollama.Model;

    public async Task<List<AccessibilityIssue>> AnalyzeHtmlAsync(
        string htmlContent,
        IReadOnlyList<AccessibilityIssue>? existingIssues = null,
        CancellationToken cancellationToken = default)
    {
        _logger.LogDebug("Analyzing HTML for accessibility issues using Ollama model {Model}", _options.Ollama.Model);

        var prompt = BuildAnalysisPrompt(htmlContent, existingIssues);

        var requestBody = new
        {
            model = _options.Ollama.Model,
            prompt,
            stream = false,
            format = "json",
            options = new
            {
                temperature = _options.Ollama.Temperature,
                num_predict = _options.Ollama.MaxTokens
            }
        };

        try
        {
            var response = await _httpClient.PostAsJsonAsync(
                "/api/generate",
                requestBody,
                cancellationToken);

            response.EnsureSuccessStatusCode();

            var result = await response.Content.ReadFromJsonAsync<OllamaResponse>(cancellationToken);

            if (result?.Response == null)
            {
                _logger.LogWarning("Ollama returned null response");
                return new List<AccessibilityIssue>();
            }

            return ParseLlmResponse(result.Response);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error analyzing HTML with Ollama");
            return new List<AccessibilityIssue>();
        }
    }

    public async Task<string> GenerateSummaryAsync(
        IReadOnlyList<AccessibilityIssue> issues,
        CancellationToken cancellationToken = default)
    {
        if (issues.Count == 0)
        {
            return "No accessibility issues were found. The page appears to follow accessibility best practices.";
        }

        var prompt = BuildSummaryPrompt(issues);

        var requestBody = new
        {
            model = _options.Ollama.Model,
            prompt,
            stream = false,
            options = new
            {
                temperature = 0.3f,
                num_predict = 1024
            }
        };

        try
        {
            var response = await _httpClient.PostAsJsonAsync(
                "/api/generate",
                requestBody,
                cancellationToken);

            response.EnsureSuccessStatusCode();

            var result = await response.Content.ReadFromJsonAsync<OllamaResponse>(cancellationToken);

            return result?.Response?.Trim() ?? "Unable to generate summary.";
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error generating summary with Ollama");
            return $"Found {issues.Count} accessibility issues: {issues.Count(i => i.Severity == IssueSeverity.Critical)} critical, {issues.Count(i => i.Severity == IssueSeverity.Serious)} serious.";
        }
    }

    public async Task<bool> IsAvailableAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            var response = await _httpClient.GetAsync("/api/tags", cancellationToken);
            return response.IsSuccessStatusCode;
        }
        catch (Exception ex)
        {
            _logger.LogDebug(ex, "Ollama service not available at {Endpoint}", _options.Ollama.Endpoint);
            return false;
        }
    }

    private string BuildAnalysisPrompt(string htmlContent, IReadOnlyList<AccessibilityIssue>? existingIssues)
    {
        var sb = new StringBuilder();

        sb.AppendLine(_options.CustomLlmPrompt ?? GetDefaultSystemPrompt());
        sb.AppendLine();

        if (existingIssues != null && existingIssues.Count > 0)
        {
            sb.AppendLine("The following issues have already been detected by rule-based analysis:");
            foreach (var issue in existingIssues.Take(10))
            {
                sb.AppendLine($"- [{issue.Type}] {issue.Description}");
            }
            sb.AppendLine();
            sb.AppendLine("Please identify ADDITIONAL issues not covered above.");
            sb.AppendLine();
        }

        sb.AppendLine("Analyze this HTML for accessibility issues:");
        sb.AppendLine("```html");
        sb.AppendLine(htmlContent);
        sb.AppendLine("```");
        sb.AppendLine();
        sb.AppendLine("Return your analysis as a JSON object with this structure:");
        sb.AppendLine(@"{
  ""issues"": [
    {
      ""type"": ""MissingAriaLabel|MissingAltText|BadHeadingHierarchy|SuspiciousContrast|ClickTargetNoText|FormInputNoLabel|Other"",
      ""severity"": ""Critical|Serious|Moderate|Minor|Info"",
      ""description"": ""Clear description of the issue"",
      ""element"": ""The problematic HTML element (truncated if long)"",
      ""suggestedFix"": ""How to fix this issue"",
      ""wcagReference"": ""e.g., 1.1.1 or 2.4.6"",
      ""confidence"": 0.0-1.0
    }
  ]
}");

        return sb.ToString();
    }

    private static string GetDefaultSystemPrompt()
    {
        return @"You are an expert web accessibility auditor analyzing HTML for WCAG compliance issues.

Focus on identifying these common accessibility problems:
1. Missing or empty ARIA labels on interactive elements (buttons, links, form controls)
2. Images missing alt text or with unhelpful alt text
3. Heading hierarchy problems (skipped levels, multiple h1s, illogical order)
4. Interactive elements that may not be keyboard accessible
5. Form inputs without proper labels
6. Color/contrast concerns based on CSS classes (e.g., 'text-gray-400', 'opacity-50')
7. Click targets without visible text content
8. Missing landmark regions (main, nav, etc.)
9. Tables without proper header associations

Be concise and specific. Only report genuine accessibility issues, not style preferences.
Assign appropriate severity: Critical for blocking issues, Serious for significant barriers, Moderate for real but navigable issues, Minor for improvements, Info for best practices.";
    }

    private string BuildSummaryPrompt(IReadOnlyList<AccessibilityIssue> issues)
    {
        var sb = new StringBuilder();

        sb.AppendLine("Generate a brief, human-readable summary of these accessibility audit findings:");
        sb.AppendLine();

        var grouped = issues.GroupBy(i => i.Severity).OrderBy(g => g.Key);
        foreach (var group in grouped)
        {
            sb.AppendLine($"**{group.Key}** ({group.Count()}):");
            foreach (var issue in group.Take(3))
            {
                sb.AppendLine($"  - {issue.Description}");
            }
            if (group.Count() > 3)
            {
                sb.AppendLine($"  - ...and {group.Count() - 3} more");
            }
        }

        sb.AppendLine();
        sb.AppendLine("Write a 2-3 sentence summary highlighting the most important findings and overall accessibility status.");

        return sb.ToString();
    }

    private List<AccessibilityIssue> ParseLlmResponse(string response)
    {
        var issues = new List<AccessibilityIssue>();

        try
        {
            // Try to parse as JSON
            var jsonStart = response.IndexOf('{');
            var jsonEnd = response.LastIndexOf('}');

            if (jsonStart >= 0 && jsonEnd > jsonStart)
            {
                var jsonContent = response.Substring(jsonStart, jsonEnd - jsonStart + 1);
                var parsed = JsonSerializer.Deserialize<LlmIssuesResponse>(jsonContent, new JsonSerializerOptions
                {
                    PropertyNameCaseInsensitive = true
                });

                if (parsed?.Issues != null)
                {
                    foreach (var llmIssue in parsed.Issues)
                    {
                        issues.Add(new AccessibilityIssue
                        {
                            Type = ParseIssueType(llmIssue.Type),
                            Severity = ParseSeverity(llmIssue.Severity),
                            Description = llmIssue.Description ?? "LLM-detected accessibility issue",
                            Element = llmIssue.Element ?? string.Empty,
                            SuggestedFix = llmIssue.SuggestedFix ?? string.Empty,
                            WcagReference = llmIssue.WcagReference,
                            Confidence = llmIssue.Confidence,
                            Source = DetectionSource.LlmAnalysis
                        });
                    }
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to parse LLM response as JSON");
        }

        return issues;
    }

    private static AccessibilityIssueType ParseIssueType(string? type)
    {
        if (string.IsNullOrEmpty(type)) return AccessibilityIssueType.Other;

        return type.ToLowerInvariant() switch
        {
            "missingarialabel" or "missing_aria_label" or "aria" => AccessibilityIssueType.MissingAriaLabel,
            "missingalttext" or "missing_alt_text" or "alt" => AccessibilityIssueType.MissingAltText,
            "emptyalttext" or "empty_alt_text" => AccessibilityIssueType.EmptyAltText,
            "badheadinghierarchy" or "heading_hierarchy" or "heading" or "headings" => AccessibilityIssueType.BadHeadingHierarchy,
            "skippedheadinglevel" or "skipped_heading" => AccessibilityIssueType.SkippedHeadingLevel,
            "multipleh1elements" or "multiple_h1" => AccessibilityIssueType.MultipleH1Elements,
            "suspiciouscontrast" or "contrast" or "color_contrast" => AccessibilityIssueType.SuspiciousContrast,
            "lowcontrastindicator" or "low_contrast" => AccessibilityIssueType.LowContrastIndicator,
            "clicktargetnotext" or "click_target" or "clickable" => AccessibilityIssueType.ClickTargetNoText,
            "buttonnoaccessiblename" or "button" => AccessibilityIssueType.ButtonNoAccessibleName,
            "linknoaccessiblename" or "link" => AccessibilityIssueType.LinkNoAccessibleName,
            "forminputnolabel" or "form_label" or "label" => AccessibilityIssueType.FormInputNoLabel,
            "missinglanguage" or "language" or "lang" => AccessibilityIssueType.MissingLanguage,
            "missingtitle" or "title" => AccessibilityIssueType.MissingTitle,
            "missinglandmark" or "landmark" => AccessibilityIssueType.MissingLandmark,
            "notkeyboardaccessible" or "keyboard" => AccessibilityIssueType.NotKeyboardAccessible,
            "missingskiplink" or "skip_link" => AccessibilityIssueType.MissingSkipLink,
            "tablenoheaders" or "table" => AccessibilityIssueType.TableNoHeaders,
            "generalconcern" or "general" => AccessibilityIssueType.GeneralConcern,
            _ => AccessibilityIssueType.Other
        };
    }

    private static IssueSeverity ParseSeverity(string? severity)
    {
        if (string.IsNullOrEmpty(severity)) return IssueSeverity.Moderate;

        return severity.ToLowerInvariant() switch
        {
            "critical" or "error" or "blocker" => IssueSeverity.Critical,
            "serious" or "major" or "warning" => IssueSeverity.Serious,
            "moderate" or "medium" => IssueSeverity.Moderate,
            "minor" or "low" => IssueSeverity.Minor,
            "info" or "informational" or "suggestion" => IssueSeverity.Info,
            _ => IssueSeverity.Moderate
        };
    }

    private class OllamaResponse
    {
        public string? Response { get; set; }
    }

    private class LlmIssuesResponse
    {
        public List<LlmIssue>? Issues { get; set; }
    }

    private class LlmIssue
    {
        public string? Type { get; set; }
        public string? Severity { get; set; }
        public string? Description { get; set; }
        public string? Element { get; set; }
        public string? SuggestedFix { get; set; }
        public string? WcagReference { get; set; }
        public float? Confidence { get; set; }
    }
}
