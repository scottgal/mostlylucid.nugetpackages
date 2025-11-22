using Mostlylucid.LlmLogSummarizer.Models;

namespace Mostlylucid.LlmLogSummarizer.Clustering;

/// <summary>
/// Interface for clustering similar exceptions.
/// </summary>
public interface IExceptionClusterer
{
    /// <summary>
    /// Clusters log entries into exception clusters.
    /// </summary>
    /// <param name="entries">Log entries to cluster.</param>
    /// <param name="options">Clustering options.</param>
    /// <returns>List of exception clusters.</returns>
    List<ExceptionCluster> ClusterExceptions(IEnumerable<LogEntry> entries, ClusteringOptions options);

    /// <summary>
    /// Compares current clusters with historical data to identify trends.
    /// </summary>
    /// <param name="currentClusters">Current period clusters.</param>
    /// <param name="historicalClusters">Previous period clusters.</param>
    void CalculateTrends(List<ExceptionCluster> currentClusters, List<ExceptionCluster> historicalClusters);
}
