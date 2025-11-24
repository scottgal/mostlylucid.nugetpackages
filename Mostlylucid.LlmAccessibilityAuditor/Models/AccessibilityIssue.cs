using System.Text.Json.Serialization;

namespace Mostlylucid.LlmAccessibilityAuditor.Models;

/// <summary>
///     Represents a single accessibility issue found in HTML content
/// </summary>
public class AccessibilityIssue
{
    /// <summary>
    ///     Unique identifier for this issue
    /// </summary>
    public string Id { get; set; } = Guid.NewGuid().ToString("N")[..8];

    /// <summary>
    ///     Type/category of the accessibility issue
    /// </summary>
    public AccessibilityIssueType Type { get; set; }

    /// <summary>
    ///     Severity of the issue
    /// </summary>
    public IssueSeverity Severity { get; set; }

    /// <summary>
    ///     Human-readable description of the issue
    /// </summary>
    public string Description { get; set; } = string.Empty;

    /// <summary>
    ///     The HTML element or snippet causing the issue (sanitized)
    /// </summary>
    public string Element { get; set; } = string.Empty;

    /// <summary>
    ///     XPath or CSS selector to locate the element
    /// </summary>
    public string Selector { get; set; } = string.Empty;

    /// <summary>
    ///     Suggested fix for the issue
    /// </summary>
    public string SuggestedFix { get; set; } = string.Empty;

    /// <summary>
    ///     WCAG guideline reference (e.g., "1.1.1", "2.4.6")
    /// </summary>
    public string? WcagReference { get; set; }

    /// <summary>
    ///     WCAG conformance level (A, AA, AAA)
    /// </summary>
    public string? WcagLevel { get; set; }

    /// <summary>
    ///     Source of detection (HtmlParser, LlmAnalysis)
    /// </summary>
    public DetectionSource Source { get; set; }

    /// <summary>
    ///     Confidence score from LLM (0.0 - 1.0), null for rule-based detections
    /// </summary>
    public float? Confidence { get; set; }

    /// <summary>
    ///     Line number in the HTML (if available)
    /// </summary>
    public int? LineNumber { get; set; }

    /// <summary>
    ///     Additional context or notes about the issue
    /// </summary>
    public string? Context { get; set; }
}

/// <summary>
///     Categories of accessibility issues
/// </summary>
[JsonConverter(typeof(JsonStringEnumConverter))]
public enum AccessibilityIssueType
{
    /// <summary>
    ///     Missing or empty aria-label, aria-labelledby, etc.
    /// </summary>
    MissingAriaLabel,

    /// <summary>
    ///     Missing alt text on images
    /// </summary>
    MissingAltText,

    /// <summary>
    ///     Empty alt text on informative images
    /// </summary>
    EmptyAltText,

    /// <summary>
    ///     Heading hierarchy issues (e.g., h1 -> h4 -> h2)
    /// </summary>
    BadHeadingHierarchy,

    /// <summary>
    ///     Skipped heading levels
    /// </summary>
    SkippedHeadingLevel,

    /// <summary>
    ///     Multiple h1 elements on the page
    /// </summary>
    MultipleH1Elements,

    /// <summary>
    ///     Suspicious color contrast based on class/role analysis
    /// </summary>
    SuspiciousContrast,

    /// <summary>
    ///     Text that may be hard to read (light colors, small sizes)
    /// </summary>
    LowContrastIndicator,

    /// <summary>
    ///     Clickable element without visible text or accessible name
    /// </summary>
    ClickTargetNoText,

    /// <summary>
    ///     Button without accessible text
    /// </summary>
    ButtonNoAccessibleName,

    /// <summary>
    ///     Link without accessible text
    /// </summary>
    LinkNoAccessibleName,

    /// <summary>
    ///     Form input without associated label
    /// </summary>
    FormInputNoLabel,

    /// <summary>
    ///     Missing language attribute on html element
    /// </summary>
    MissingLanguage,

    /// <summary>
    ///     Missing page title
    /// </summary>
    MissingTitle,

    /// <summary>
    ///     Missing or empty document landmark
    /// </summary>
    MissingLandmark,

    /// <summary>
    ///     Interactive element not keyboard accessible
    /// </summary>
    NotKeyboardAccessible,

    /// <summary>
    ///     Missing skip navigation link
    /// </summary>
    MissingSkipLink,

    /// <summary>
    ///     Table without proper headers
    /// </summary>
    TableNoHeaders,

    /// <summary>
    ///     General accessibility concern identified by LLM
    /// </summary>
    GeneralConcern,

    /// <summary>
    ///     Other/uncategorized issue
    /// </summary>
    Other
}

/// <summary>
///     Severity levels for accessibility issues
/// </summary>
[JsonConverter(typeof(JsonStringEnumConverter))]
public enum IssueSeverity
{
    /// <summary>
    ///     Critical - must fix, major accessibility barrier
    /// </summary>
    Critical,

    /// <summary>
    ///     Serious - should fix, significant accessibility issue
    /// </summary>
    Serious,

    /// <summary>
    ///     Moderate - consider fixing, accessibility improvement
    /// </summary>
    Moderate,

    /// <summary>
    ///     Minor - nice to fix, minor accessibility enhancement
    /// </summary>
    Minor,

    /// <summary>
    ///     Info - informational, best practice suggestion
    /// </summary>
    Info
}

/// <summary>
///     Source of issue detection
/// </summary>
[JsonConverter(typeof(JsonStringEnumConverter))]
public enum DetectionSource
{
    /// <summary>
    ///     Detected by HTML parsing rules
    /// </summary>
    HtmlParser,

    /// <summary>
    ///     Detected by LLM analysis
    /// </summary>
    LlmAnalysis,

    /// <summary>
    ///     Detected by both methods
    /// </summary>
    Combined
}