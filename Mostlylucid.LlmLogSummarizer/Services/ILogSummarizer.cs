using Mostlylucid.LlmLogSummarizer.Models;

namespace Mostlylucid.LlmLogSummarizer.Services;

/// <summary>
/// Interface for LLM-powered log summarization.
/// </summary>
public interface ILogSummarizer
{
    /// <summary>
    /// Generates a summary for a single exception cluster.
    /// </summary>
    Task<string> SummarizeClusterAsync(
        ExceptionCluster cluster,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Generates suggested actions for a cluster.
    /// </summary>
    Task<string> GenerateSuggestedActionAsync(
        ExceptionCluster cluster,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Generates an executive summary for the entire report.
    /// </summary>
    Task<string> GenerateExecutiveSummaryAsync(
        SummaryReport report,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Generates key insights from the analysis.
    /// </summary>
    Task<List<string>> GenerateKeyInsightsAsync(
        SummaryReport report,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Assesses the overall health status.
    /// </summary>
    Task<HealthStatus> AssessHealthStatusAsync(
        SummaryReport report,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Checks if the LLM service is available.
    /// </summary>
    Task<bool> IsAvailableAsync(CancellationToken cancellationToken = default);
}
