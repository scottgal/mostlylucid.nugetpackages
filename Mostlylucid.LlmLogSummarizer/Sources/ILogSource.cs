using Mostlylucid.LlmLogSummarizer.Models;

namespace Mostlylucid.LlmLogSummarizer.Sources;

/// <summary>
/// Interface for log sources that can provide log entries.
/// </summary>
public interface ILogSource
{
    /// <summary>
    /// Name of this log source.
    /// </summary>
    string Name { get; }

    /// <summary>
    /// Whether this source is available/configured.
    /// </summary>
    bool IsAvailable { get; }

    /// <summary>
    /// Gets log entries from this source for the specified time range.
    /// </summary>
    /// <param name="from">Start of time range.</param>
    /// <param name="to">End of time range.</param>
    /// <param name="maxEntries">Maximum entries to return.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Collection of log entries.</returns>
    IAsyncEnumerable<LogEntry> GetEntriesAsync(
        DateTimeOffset from,
        DateTimeOffset to,
        int maxEntries,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Tests if the source is accessible.
    /// </summary>
    Task<bool> TestConnectionAsync(CancellationToken cancellationToken = default);
}
