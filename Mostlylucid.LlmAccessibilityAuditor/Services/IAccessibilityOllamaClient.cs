using Mostlylucid.LlmAccessibilityAuditor.Models;

namespace Mostlylucid.LlmAccessibilityAuditor.Services;

/// <summary>
///     Client for Ollama LLM API for accessibility analysis
/// </summary>
public interface IAccessibilityOllamaClient
{
    /// <summary>
    ///     Get the model name being used
    /// </summary>
    string ModelName { get; }

    /// <summary>
    ///     Analyze HTML content for accessibility issues using LLM
    /// </summary>
    /// <param name="htmlContent">The HTML content to analyze</param>
    /// <param name="existingIssues">Issues already detected by rule-based analysis (for context)</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>List of accessibility issues found by LLM</returns>
    Task<List<AccessibilityIssue>> AnalyzeHtmlAsync(
        string htmlContent,
        IReadOnlyList<AccessibilityIssue>? existingIssues = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    ///     Generate a human-readable summary of the audit findings
    /// </summary>
    /// <param name="issues">All issues found</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Human-readable summary</returns>
    Task<string> GenerateSummaryAsync(
        IReadOnlyList<AccessibilityIssue> issues,
        CancellationToken cancellationToken = default);

    /// <summary>
    ///     Check if Ollama service is available
    /// </summary>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>True if available</returns>
    Task<bool> IsAvailableAsync(CancellationToken cancellationToken = default);
}