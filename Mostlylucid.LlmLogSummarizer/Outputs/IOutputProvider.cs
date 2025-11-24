using Mostlylucid.LlmLogSummarizer.Models;

namespace Mostlylucid.LlmLogSummarizer.Outputs;

/// <summary>
///     Interface for output providers that can emit summary reports.
/// </summary>
public interface IOutputProvider
{
    /// <summary>
    ///     Name of this output provider.
    /// </summary>
    string Name { get; }

    /// <summary>
    ///     Whether this provider is enabled and configured.
    /// </summary>
    bool IsEnabled { get; }

    /// <summary>
    ///     Outputs the summary report.
    /// </summary>
    /// <param name="report">The report to output.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    Task OutputAsync(SummaryReport report, CancellationToken cancellationToken = default);
}