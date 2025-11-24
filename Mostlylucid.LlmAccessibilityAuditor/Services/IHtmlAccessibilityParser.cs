using Mostlylucid.LlmAccessibilityAuditor.Models;

namespace Mostlylucid.LlmAccessibilityAuditor.Services;

/// <summary>
///     Service for parsing HTML and detecting accessibility issues using rule-based analysis
/// </summary>
public interface IHtmlAccessibilityParser
{
    /// <summary>
    ///     Parse HTML and detect accessibility issues using rule-based checks
    /// </summary>
    /// <param name="html">The HTML content to analyze</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>List of detected accessibility issues</returns>
    Task<List<AccessibilityIssue>> ParseAndAnalyzeAsync(string html, CancellationToken cancellationToken = default);

    /// <summary>
    ///     Extract a simplified/truncated version of HTML suitable for LLM analysis
    /// </summary>
    /// <param name="html">The original HTML</param>
    /// <param name="maxLength">Maximum length in characters</param>
    /// <returns>Simplified HTML focusing on accessibility-relevant elements</returns>
    string SimplifyForLlm(string html, int maxLength);

    /// <summary>
    ///     Extract the page title from HTML
    /// </summary>
    /// <param name="html">The HTML content</param>
    /// <returns>Page title or null</returns>
    string? ExtractTitle(string html);
}