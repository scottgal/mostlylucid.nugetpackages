using Mostlylucid.LlmAccessibilityAuditor.Models;

namespace Mostlylucid.LlmAccessibilityAuditor.Services;

/// <summary>
/// Main accessibility auditor service that combines rule-based and LLM analysis
/// </summary>
public interface IAccessibilityAuditor
{
    /// <summary>
    /// Perform a full accessibility audit on HTML content
    /// </summary>
    /// <param name="html">The HTML content to audit</param>
    /// <param name="pageUrl">Optional URL of the page (for reporting)</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Complete audit report</returns>
    Task<AccessibilityAuditReport> AuditAsync(
        string html,
        string? pageUrl = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Perform a quick audit using only rule-based checks (no LLM)
    /// </summary>
    /// <param name="html">The HTML content to audit</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>List of issues found</returns>
    Task<AuditResult> QuickAuditAsync(
        string html,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Check if the auditor is ready (including LLM availability if enabled)
    /// </summary>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>True if ready to audit</returns>
    Task<bool> IsReadyAsync(CancellationToken cancellationToken = default);
}
