using System.Text;
using HtmlAgilityPack;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Mostlylucid.LlmAccessibilityAuditor.Models;

namespace Mostlylucid.LlmAccessibilityAuditor.Services;

/// <summary>
///     Service for parsing HTML and detecting accessibility issues using rule-based analysis
/// </summary>
public class HtmlAccessibilityParser : IHtmlAccessibilityParser
{
    // CSS classes that may indicate low contrast (common patterns)
    private static readonly HashSet<string> LowContrastClasses = new(StringComparer.OrdinalIgnoreCase)
    {
        "text-gray-400", "text-gray-500", "text-gray-600",
        "text-slate-400", "text-slate-500",
        "text-zinc-400", "text-zinc-500",
        "opacity-50", "opacity-40", "opacity-30",
        "text-muted", "text-light", "text-secondary",
        "disabled", "muted"
    };

    // Interactive element selectors
    private static readonly string[] InteractiveElements = { "button", "a", "input", "select", "textarea" };
    private readonly ILogger<HtmlAccessibilityParser> _logger;
    private readonly AccessibilityAuditorOptions _options;

    public HtmlAccessibilityParser(
        ILogger<HtmlAccessibilityParser> logger,
        IOptions<AccessibilityAuditorOptions> options)
    {
        _logger = logger;
        _options = options.Value;
    }

    public Task<List<AccessibilityIssue>> ParseAndAnalyzeAsync(string html,
        CancellationToken cancellationToken = default)
    {
        var issues = new List<AccessibilityIssue>();

        try
        {
            var doc = new HtmlDocument();
            doc.LoadHtml(html);

            // Run all checks
            CheckMissingLanguage(doc, issues);
            CheckMissingTitle(doc, issues);
            CheckHeadingHierarchy(doc, issues);
            CheckImages(doc, issues);
            CheckButtons(doc, issues);
            CheckLinks(doc, issues);
            CheckFormInputs(doc, issues);
            CheckAriaLabels(doc, issues);
            CheckLandmarks(doc, issues);
            CheckContrastIndicators(doc, issues);
            CheckTables(doc, issues);
            CheckSkipLinks(doc, issues);

            // Filter by minimum severity
            issues = issues
                .Where(i => i.Severity <= _options.MinimumSeverity)
                .ToList();

            _logger.LogDebug("HTML parser found {Count} accessibility issues", issues.Count);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error parsing HTML for accessibility issues");
        }

        return Task.FromResult(issues);
    }

    public string SimplifyForLlm(string html, int maxLength)
    {
        try
        {
            var doc = new HtmlDocument();
            doc.LoadHtml(html);

            // Remove scripts, styles, and comments
            RemoveNodes(doc, "//script");
            RemoveNodes(doc, "//style");
            RemoveNodes(doc, "//comment()");
            RemoveNodes(doc, "//noscript");
            RemoveNodes(doc, "//svg");

            // Get simplified HTML focusing on relevant elements
            var sb = new StringBuilder();

            // Include head info
            var title = doc.DocumentNode.SelectSingleNode("//title")?.InnerText;
            var lang = doc.DocumentNode.SelectSingleNode("//html")?.GetAttributeValue("lang", null);

            sb.AppendLine($"<!-- Page: {title ?? "No title"}, lang={lang ?? "not set"} -->");

            // Extract body content
            var body = doc.DocumentNode.SelectSingleNode("//body") ?? doc.DocumentNode;

            // Get key elements
            AppendElementsOfType(sb, body, "//h1|//h2|//h3|//h4|//h5|//h6", "Headings");
            AppendElementsOfType(sb, body, "//nav", "Navigation");
            AppendElementsOfType(sb, body, "//main", "Main content");
            AppendElementsOfType(sb, body, "//form", "Forms");
            AppendElementsOfType(sb, body, "//button", "Buttons");
            AppendElementsOfType(sb, body, "//a", "Links", 20);
            AppendElementsOfType(sb, body, "//img", "Images");
            AppendElementsOfType(sb, body, "//input|//select|//textarea", "Form controls");
            AppendElementsOfType(sb, body, "//*[@role]", "ARIA roles");
            AppendElementsOfType(sb, body, "//*[@aria-label or @aria-labelledby or @aria-describedby]", "ARIA labels");
            AppendElementsOfType(sb, body, "//table", "Tables");

            var result = sb.ToString();

            // Truncate if needed
            if (result.Length > maxLength)
                result = result.Substring(0, maxLength - 50) + "\n<!-- ... truncated ... -->";

            return result;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Error simplifying HTML for LLM");
            // Return truncated original
            return html.Length > maxLength
                ? html.Substring(0, maxLength - 50) + "\n<!-- ... truncated ... -->"
                : html;
        }
    }

    public string? ExtractTitle(string html)
    {
        try
        {
            var doc = new HtmlDocument();
            doc.LoadHtml(html);
            return doc.DocumentNode.SelectSingleNode("//title")?.InnerText?.Trim();
        }
        catch
        {
            return null;
        }
    }

    private void CheckMissingLanguage(HtmlDocument doc, List<AccessibilityIssue> issues)
    {
        var html = doc.DocumentNode.SelectSingleNode("//html");
        var lang = html?.GetAttributeValue("lang", null);

        if (string.IsNullOrWhiteSpace(lang))
            issues.Add(new AccessibilityIssue
            {
                Type = AccessibilityIssueType.MissingLanguage,
                Severity = IssueSeverity.Serious,
                Description = "Page is missing the lang attribute on the <html> element",
                Element = "<html>",
                Selector = "html",
                SuggestedFix = "Add lang attribute: <html lang=\"en\">",
                WcagReference = "3.1.1",
                WcagLevel = "A",
                Source = DetectionSource.HtmlParser
            });
    }

    private void CheckMissingTitle(HtmlDocument doc, List<AccessibilityIssue> issues)
    {
        var title = doc.DocumentNode.SelectSingleNode("//title");
        var titleText = title?.InnerText?.Trim();

        if (string.IsNullOrWhiteSpace(titleText))
            issues.Add(new AccessibilityIssue
            {
                Type = AccessibilityIssueType.MissingTitle,
                Severity = IssueSeverity.Serious,
                Description = "Page is missing a title or has an empty title",
                Element = "<title>",
                Selector = "title",
                SuggestedFix = "Add a descriptive page title: <title>Page Name - Site Name</title>",
                WcagReference = "2.4.2",
                WcagLevel = "A",
                Source = DetectionSource.HtmlParser
            });
    }

    private void CheckHeadingHierarchy(HtmlDocument doc, List<AccessibilityIssue> issues)
    {
        var headings = doc.DocumentNode.SelectNodes("//h1|//h2|//h3|//h4|//h5|//h6");
        if (headings == null) return;

        var h1Count = 0;
        var lastLevel = 0;

        foreach (var heading in headings)
        {
            var level = int.Parse(heading.Name[1].ToString());

            // Check for multiple h1
            if (level == 1)
            {
                h1Count++;
                if (h1Count > 1)
                    issues.Add(new AccessibilityIssue
                    {
                        Type = AccessibilityIssueType.MultipleH1Elements,
                        Severity = IssueSeverity.Moderate,
                        Description = $"Multiple <h1> elements found (this is #{h1Count})",
                        Element = TruncateElement(heading.OuterHtml),
                        Selector = GetSelector(heading),
                        SuggestedFix = "Use only one <h1> per page for the main heading",
                        WcagReference = "1.3.1",
                        WcagLevel = "A",
                        Source = DetectionSource.HtmlParser
                    });
            }

            // Check for skipped levels (e.g., h1 -> h3)
            if (lastLevel > 0 && level > lastLevel + 1)
                issues.Add(new AccessibilityIssue
                {
                    Type = AccessibilityIssueType.SkippedHeadingLevel,
                    Severity = IssueSeverity.Moderate,
                    Description = $"Heading level skipped: <h{lastLevel}> followed by <h{level}>",
                    Element = TruncateElement(heading.OuterHtml),
                    Selector = GetSelector(heading),
                    SuggestedFix = $"Use <h{lastLevel + 1}> instead, or restructure the heading hierarchy",
                    WcagReference = "1.3.1",
                    WcagLevel = "A",
                    Source = DetectionSource.HtmlParser
                });

            lastLevel = level;
        }

        // Check if page starts without h1
        if (headings.Count > 0 && int.Parse(headings[0].Name[1].ToString()) != 1)
            issues.Add(new AccessibilityIssue
            {
                Type = AccessibilityIssueType.BadHeadingHierarchy,
                Severity = IssueSeverity.Minor,
                Description = $"Page does not start with <h1>, first heading is <{headings[0].Name}>",
                Element = TruncateElement(headings[0].OuterHtml),
                Selector = GetSelector(headings[0]),
                SuggestedFix = "Start the page with an <h1> element for the main heading",
                WcagReference = "1.3.1",
                WcagLevel = "A",
                Source = DetectionSource.HtmlParser
            });
    }

    private void CheckImages(HtmlDocument doc, List<AccessibilityIssue> issues)
    {
        var images = doc.DocumentNode.SelectNodes("//img");
        if (images == null) return;

        foreach (var img in images)
        {
            var alt = img.GetAttributeValue("alt", null);
            var role = img.GetAttributeValue("role", null);
            var ariaHidden = img.GetAttributeValue("aria-hidden", null);

            // Skip decorative images
            if (role == "presentation" || role == "none" || ariaHidden == "true")
                continue;

            if (alt == null)
                issues.Add(new AccessibilityIssue
                {
                    Type = AccessibilityIssueType.MissingAltText,
                    Severity = IssueSeverity.Critical,
                    Description = "Image is missing alt attribute",
                    Element = TruncateElement(img.OuterHtml),
                    Selector = GetSelector(img),
                    SuggestedFix = "Add alt attribute: alt=\"Description of image\" or alt=\"\" for decorative images",
                    WcagReference = "1.1.1",
                    WcagLevel = "A",
                    Source = DetectionSource.HtmlParser
                });
            else if (string.IsNullOrWhiteSpace(alt))
                // Empty alt might be intentional for decorative images, but flag if no role
                if (role == null)
                    issues.Add(new AccessibilityIssue
                    {
                        Type = AccessibilityIssueType.EmptyAltText,
                        Severity = IssueSeverity.Info,
                        Description = "Image has empty alt text - verify this is decorative",
                        Element = TruncateElement(img.OuterHtml),
                        Selector = GetSelector(img),
                        SuggestedFix =
                            "If decorative, add role=\"presentation\". If informative, provide descriptive alt text",
                        WcagReference = "1.1.1",
                        WcagLevel = "A",
                        Source = DetectionSource.HtmlParser
                    });
        }
    }

    private void CheckButtons(HtmlDocument doc, List<AccessibilityIssue> issues)
    {
        var buttons = doc.DocumentNode.SelectNodes("//button|//*[@role='button']");
        if (buttons == null) return;

        foreach (var button in buttons)
            if (!HasAccessibleName(button))
                issues.Add(new AccessibilityIssue
                {
                    Type = AccessibilityIssueType.ButtonNoAccessibleName,
                    Severity = IssueSeverity.Critical,
                    Description = "Button has no accessible name",
                    Element = TruncateElement(button.OuterHtml),
                    Selector = GetSelector(button),
                    SuggestedFix = "Add text content, aria-label, or aria-labelledby to the button",
                    WcagReference = "4.1.2",
                    WcagLevel = "A",
                    Source = DetectionSource.HtmlParser
                });
    }

    private void CheckLinks(HtmlDocument doc, List<AccessibilityIssue> issues)
    {
        var links = doc.DocumentNode.SelectNodes("//a[@href]");
        if (links == null) return;

        foreach (var link in links)
            if (!HasAccessibleName(link))
                issues.Add(new AccessibilityIssue
                {
                    Type = AccessibilityIssueType.LinkNoAccessibleName,
                    Severity = IssueSeverity.Critical,
                    Description = "Link has no accessible name",
                    Element = TruncateElement(link.OuterHtml),
                    Selector = GetSelector(link),
                    SuggestedFix = "Add text content, aria-label, or use a descriptive link text",
                    WcagReference = "2.4.4",
                    WcagLevel = "A",
                    Source = DetectionSource.HtmlParser
                });
    }

    private void CheckFormInputs(HtmlDocument doc, List<AccessibilityIssue> issues)
    {
        var inputs = doc.DocumentNode.SelectNodes(
            "//input[not(@type='hidden' or @type='submit' or @type='button' or @type='image')]|//select|//textarea");
        if (inputs == null) return;

        foreach (var input in inputs)
        {
            var id = input.GetAttributeValue("id", null);
            var ariaLabel = input.GetAttributeValue("aria-label", null);
            var ariaLabelledby = input.GetAttributeValue("aria-labelledby", null);
            var title = input.GetAttributeValue("title", null);
            var placeholder = input.GetAttributeValue("placeholder", null);

            // Check for associated label
            var hasLabel = false;
            if (!string.IsNullOrEmpty(id))
            {
                var label = doc.DocumentNode.SelectSingleNode($"//label[@for='{id}']");
                hasLabel = label != null;
            }

            // Also check for implicit label (input inside label)
            if (!hasLabel)
            {
                var parentLabel = input.Ancestors("label").FirstOrDefault();
                hasLabel = parentLabel != null;
            }

            if (!hasLabel && string.IsNullOrEmpty(ariaLabel) && string.IsNullOrEmpty(ariaLabelledby) &&
                string.IsNullOrEmpty(title))
            {
                var severity = IssueSeverity.Critical;
                var fix = "Add a <label for=\"inputId\"> element or aria-label attribute";

                // Placeholder alone is not sufficient but less severe
                if (!string.IsNullOrEmpty(placeholder))
                {
                    severity = IssueSeverity.Serious;
                    fix = "Placeholder alone is not sufficient. Add a proper <label> element";
                }

                issues.Add(new AccessibilityIssue
                {
                    Type = AccessibilityIssueType.FormInputNoLabel,
                    Severity = severity,
                    Description = "Form input has no associated label",
                    Element = TruncateElement(input.OuterHtml),
                    Selector = GetSelector(input),
                    SuggestedFix = fix,
                    WcagReference = "1.3.1",
                    WcagLevel = "A",
                    Source = DetectionSource.HtmlParser
                });
            }
        }
    }

    private void CheckAriaLabels(HtmlDocument doc, List<AccessibilityIssue> issues)
    {
        // Check for empty aria-labels
        var elementsWithAriaLabel = doc.DocumentNode.SelectNodes("//*[@aria-label]");
        if (elementsWithAriaLabel != null)
            foreach (var element in elementsWithAriaLabel)
            {
                var ariaLabel = element.GetAttributeValue("aria-label", "");
                if (string.IsNullOrWhiteSpace(ariaLabel))
                    issues.Add(new AccessibilityIssue
                    {
                        Type = AccessibilityIssueType.MissingAriaLabel,
                        Severity = IssueSeverity.Serious,
                        Description = "Element has empty aria-label attribute",
                        Element = TruncateElement(element.OuterHtml),
                        Selector = GetSelector(element),
                        SuggestedFix = "Provide a meaningful value for aria-label or remove if not needed",
                        WcagReference = "4.1.2",
                        WcagLevel = "A",
                        Source = DetectionSource.HtmlParser
                    });
            }

        // Check for aria-labelledby referencing non-existent IDs
        var elementsWithLabelledby = doc.DocumentNode.SelectNodes("//*[@aria-labelledby]");
        if (elementsWithLabelledby != null)
            foreach (var element in elementsWithLabelledby)
            {
                var labelledby = element.GetAttributeValue("aria-labelledby", "");
                var ids = labelledby.Split(' ', StringSplitOptions.RemoveEmptyEntries);

                foreach (var id in ids)
                {
                    var target = doc.DocumentNode.SelectSingleNode($"//*[@id='{id}']");
                    if (target == null)
                        issues.Add(new AccessibilityIssue
                        {
                            Type = AccessibilityIssueType.MissingAriaLabel,
                            Severity = IssueSeverity.Serious,
                            Description = $"aria-labelledby references non-existent id '{id}'",
                            Element = TruncateElement(element.OuterHtml),
                            Selector = GetSelector(element),
                            SuggestedFix = $"Ensure an element with id=\"{id}\" exists",
                            WcagReference = "4.1.2",
                            WcagLevel = "A",
                            Source = DetectionSource.HtmlParser
                        });
                }
            }
    }

    private void CheckLandmarks(HtmlDocument doc, List<AccessibilityIssue> issues)
    {
        var main = doc.DocumentNode.SelectSingleNode("//main|//*[@role='main']");
        if (main == null)
            issues.Add(new AccessibilityIssue
            {
                Type = AccessibilityIssueType.MissingLandmark,
                Severity = IssueSeverity.Moderate,
                Description = "Page is missing a <main> landmark",
                Element = "<body>",
                Selector = "body",
                SuggestedFix = "Add a <main> element to wrap the primary content",
                WcagReference = "1.3.1",
                WcagLevel = "A",
                Source = DetectionSource.HtmlParser
            });
    }

    private void CheckContrastIndicators(HtmlDocument doc, List<AccessibilityIssue> issues)
    {
        // Check for CSS classes that commonly indicate low contrast
        foreach (var className in LowContrastClasses)
        {
            var elements = doc.DocumentNode.SelectNodes($"//*[contains(@class, '{className}')]");
            if (elements != null)
                foreach (var element in elements.Take(3)) // Limit to avoid noise
                {
                    // Only flag if it contains meaningful text
                    var text = element.InnerText?.Trim();
                    if (!string.IsNullOrEmpty(text) && text.Length > 2)
                        issues.Add(new AccessibilityIssue
                        {
                            Type = AccessibilityIssueType.LowContrastIndicator,
                            Severity = IssueSeverity.Info,
                            Description = $"Element uses class '{className}' which may indicate low contrast",
                            Element = TruncateElement(element.OuterHtml),
                            Selector = GetSelector(element),
                            SuggestedFix = "Verify color contrast meets WCAG 4.5:1 ratio for normal text",
                            WcagReference = "1.4.3",
                            WcagLevel = "AA",
                            Source = DetectionSource.HtmlParser,
                            Context = "This is a heuristic check. Use a contrast checker tool to verify."
                        });
                }
        }
    }

    private void CheckTables(HtmlDocument doc, List<AccessibilityIssue> issues)
    {
        var tables = doc.DocumentNode.SelectNodes("//table");
        if (tables == null) return;

        foreach (var table in tables)
        {
            // Check for presentation role (data tables need headers)
            var role = table.GetAttributeValue("role", null);
            if (role == "presentation" || role == "none") continue;

            var headers = table.SelectNodes(".//th");
            if (headers == null || headers.Count == 0)
                issues.Add(new AccessibilityIssue
                {
                    Type = AccessibilityIssueType.TableNoHeaders,
                    Severity = IssueSeverity.Serious,
                    Description = "Data table has no header cells (<th>)",
                    Element = TruncateElement(table.OuterHtml, 200),
                    Selector = GetSelector(table),
                    SuggestedFix =
                        "Add <th> elements for column/row headers, or role=\"presentation\" if not a data table",
                    WcagReference = "1.3.1",
                    WcagLevel = "A",
                    Source = DetectionSource.HtmlParser
                });
        }
    }

    private void CheckSkipLinks(HtmlDocument doc, List<AccessibilityIssue> issues)
    {
        // Check for skip navigation link (should be one of the first focusable elements)
        var firstLinks = doc.DocumentNode.SelectNodes("//a[@href]")?.Take(5).ToList();
        if (firstLinks == null) return;

        var hasSkipLink = firstLinks.Any(link =>
        {
            var href = link.GetAttributeValue("href", "");
            var text = link.InnerText?.ToLowerInvariant() ?? "";
            var ariaLabel = link.GetAttributeValue("aria-label", "")?.ToLowerInvariant() ?? "";

            return href.StartsWith("#") &&
                   (text.Contains("skip") || text.Contains("main") ||
                    ariaLabel.Contains("skip") || ariaLabel.Contains("main"));
        });

        if (!hasSkipLink)
            issues.Add(new AccessibilityIssue
            {
                Type = AccessibilityIssueType.MissingSkipLink,
                Severity = IssueSeverity.Minor,
                Description = "Page may be missing a skip navigation link",
                Element = "<body>",
                Selector = "body",
                SuggestedFix = "Add a 'Skip to main content' link as one of the first focusable elements",
                WcagReference = "2.4.1",
                WcagLevel = "A",
                Source = DetectionSource.HtmlParser,
                Context = "Skip links help keyboard users bypass repetitive navigation"
            });
    }

    private static bool HasAccessibleName(HtmlNode element)
    {
        // Check aria-label
        var ariaLabel = element.GetAttributeValue("aria-label", null);
        if (!string.IsNullOrWhiteSpace(ariaLabel)) return true;

        // Check aria-labelledby
        var ariaLabelledby = element.GetAttributeValue("aria-labelledby", null);
        if (!string.IsNullOrWhiteSpace(ariaLabelledby)) return true;

        // Check title
        var title = element.GetAttributeValue("title", null);
        if (!string.IsNullOrWhiteSpace(title)) return true;

        // Check visible text content
        var text = GetVisibleText(element);
        if (!string.IsNullOrWhiteSpace(text)) return true;

        // Check for img with alt inside
        var img = element.SelectSingleNode(".//img[@alt]");
        if (img != null)
        {
            var alt = img.GetAttributeValue("alt", "");
            if (!string.IsNullOrWhiteSpace(alt)) return true;
        }

        // Check for sr-only text
        var srOnly =
            element.SelectSingleNode(".//*[contains(@class, 'sr-only') or contains(@class, 'visually-hidden')]");
        if (srOnly != null && !string.IsNullOrWhiteSpace(srOnly.InnerText)) return true;

        return false;
    }

    private static string GetVisibleText(HtmlNode element)
    {
        // Get text excluding hidden elements
        var text = new StringBuilder();
        foreach (var child in element.DescendantsAndSelf())
            if (child.NodeType == HtmlNodeType.Text)
            {
                var parent = child.ParentNode;
                var ariaHidden = parent?.GetAttributeValue("aria-hidden", null);
                if (ariaHidden != "true") text.Append(child.InnerText);
            }

        return text.ToString().Trim();
    }

    private static string GetSelector(HtmlNode node)
    {
        var parts = new List<string>();
        var current = node;

        while (current != null && current.NodeType == HtmlNodeType.Element)
        {
            var id = current.GetAttributeValue("id", null);
            if (!string.IsNullOrEmpty(id))
            {
                parts.Insert(0, $"#{id}");
                break;
            }

            var className = current.GetAttributeValue("class", null)?.Split(' ').FirstOrDefault();
            var selector = current.Name;
            if (!string.IsNullOrEmpty(className)) selector += $".{className}";

            parts.Insert(0, selector);
            current = current.ParentNode;

            // Limit depth
            if (parts.Count > 4) break;
        }

        return string.Join(" > ", parts);
    }

    private static string TruncateElement(string html, int maxLength = 150)
    {
        if (html.Length <= maxLength) return html;
        return html.Substring(0, maxLength) + "...";
    }

    private static void RemoveNodes(HtmlDocument doc, string xpath)
    {
        var nodes = doc.DocumentNode.SelectNodes(xpath);
        if (nodes != null)
            foreach (var node in nodes.ToList())
                node.Remove();
    }

    private void AppendElementsOfType(StringBuilder sb, HtmlNode root, string xpath, string label, int limit = 10)
    {
        var elements = root.SelectNodes(xpath);
        if (elements == null || elements.Count == 0) return;

        sb.AppendLine($"<!-- {label}: -->");
        foreach (var el in elements.Take(limit)) sb.AppendLine(TruncateElement(el.OuterHtml, 200));
        if (elements.Count > limit) sb.AppendLine($"<!-- ... and {elements.Count - limit} more {label.ToLower()} -->");
        sb.AppendLine();
    }
}