using Microsoft.Extensions.Logging;
using Mostlylucid.LlmLogSummarizer.Models;

namespace Mostlylucid.LlmLogSummarizer.Sources;

/// <summary>
/// Aggregates log entries from multiple sources.
/// </summary>
public class LogSourceAggregator : ILogSourceAggregator
{
    private readonly IEnumerable<ILogSource> _sources;
    private readonly ILogger<LogSourceAggregator> _logger;

    public LogSourceAggregator(
        IEnumerable<ILogSource> sources,
        ILogger<LogSourceAggregator> logger)
    {
        _sources = sources;
        _logger = logger;
    }

    /// <summary>
    /// Gets all available log sources.
    /// </summary>
    public IEnumerable<ILogSource> Sources => _sources.Where(s => s.IsAvailable);

    /// <summary>
    /// Gets log entries from all configured sources.
    /// </summary>
    public async IAsyncEnumerable<LogEntry> GetAllEntriesAsync(
        DateTimeOffset from,
        DateTimeOffset to,
        int maxEntriesPerSource,
        [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        var availableSources = Sources.ToList();

        _logger.LogInformation(
            "Collecting logs from {Count} sources for period {From} to {To}",
            availableSources.Count, from, to);

        foreach (var source in availableSources)
        {
            if (cancellationToken.IsCancellationRequested)
                yield break;

            _logger.LogDebug("Reading from source: {SourceName}", source.Name);

            var count = 0;
            await foreach (var entry in source.GetEntriesAsync(from, to, maxEntriesPerSource, cancellationToken))
            {
                count++;
                yield return entry;
            }

            _logger.LogDebug("Read {Count} entries from {SourceName}", count, source.Name);
        }
    }

    /// <summary>
    /// Tests connectivity to all configured sources.
    /// </summary>
    public async Task<Dictionary<string, bool>> TestAllSourcesAsync(CancellationToken cancellationToken = default)
    {
        var results = new Dictionary<string, bool>();

        foreach (var source in _sources)
        {
            try
            {
                results[source.Name] = await source.TestConnectionAsync(cancellationToken);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to test source {SourceName}", source.Name);
                results[source.Name] = false;
            }
        }

        return results;
    }
}

/// <summary>
/// Interface for log source aggregation.
/// </summary>
public interface ILogSourceAggregator
{
    /// <summary>
    /// Gets all available log sources.
    /// </summary>
    IEnumerable<ILogSource> Sources { get; }

    /// <summary>
    /// Gets log entries from all configured sources.
    /// </summary>
    IAsyncEnumerable<LogEntry> GetAllEntriesAsync(
        DateTimeOffset from,
        DateTimeOffset to,
        int maxEntriesPerSource,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Tests connectivity to all configured sources.
    /// </summary>
    Task<Dictionary<string, bool>> TestAllSourcesAsync(CancellationToken cancellationToken = default);
}
