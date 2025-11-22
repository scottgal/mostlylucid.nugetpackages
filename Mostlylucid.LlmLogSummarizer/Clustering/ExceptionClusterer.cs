using Microsoft.Extensions.Logging;
using Mostlylucid.LlmLogSummarizer.Models;
using ModelLogLevel = Mostlylucid.LlmLogSummarizer.Models.LogLevel;

namespace Mostlylucid.LlmLogSummarizer.Clustering;

/// <summary>
/// Clusters similar exceptions using fingerprinting and similarity algorithms.
/// </summary>
public class ExceptionClusterer : IExceptionClusterer
{
    private readonly ILogger<ExceptionClusterer> _logger;

    public ExceptionClusterer(ILogger<ExceptionClusterer> logger)
    {
        _logger = logger;
    }

    public List<ExceptionCluster> ClusterExceptions(IEnumerable<LogEntry> entries, ClusteringOptions options)
    {
        var errorEntries = entries
            .Where(e => e.Level >= ModelLogLevel.Warning)
            .ToList();

        _logger.LogDebug("Clustering {Count} error/warning entries", errorEntries.Count);

        var clusters = new Dictionary<string, ExceptionCluster>();

        foreach (var entry in errorEntries)
        {
            var fingerprint = entry.GetClusteringFingerprint();

            // Try to find an existing cluster with similar fingerprint
            var matchingCluster = FindSimilarCluster(clusters.Values, fingerprint, entry, options);

            if (matchingCluster != null)
            {
                matchingCluster.Entries.Add(entry);
            }
            else
            {
                // Create new cluster
                var cluster = CreateCluster(entry, fingerprint);
                clusters[cluster.Id] = cluster;
            }

            // Limit number of clusters
            if (clusters.Count >= options.MaxClusters)
            {
                _logger.LogWarning("Reached maximum cluster limit of {Max}", options.MaxClusters);
                break;
            }
        }

        // Filter by minimum size and assign severity
        var result = clusters.Values
            .Where(c => c.Count >= options.MinClusterSize)
            .OrderByDescending(c => c.Count)
            .ToList();

        foreach (var cluster in result)
        {
            AssignSeverity(cluster);
            GenerateTitle(cluster);
        }

        _logger.LogInformation("Created {Count} clusters from {Total} entries",
            result.Count, errorEntries.Count);

        return result;
    }

    public void CalculateTrends(List<ExceptionCluster> currentClusters, List<ExceptionCluster> historicalClusters)
    {
        foreach (var current in currentClusters)
        {
            var historical = FindMatchingHistoricalCluster(current, historicalClusters);

            if (historical == null)
            {
                current.IsNew = true;
                current.TrendPercent = 100; // New pattern
            }
            else
            {
                current.PreviousPeriodCount = historical.Count;

                if (historical.Count > 0)
                {
                    current.TrendPercent = ((double)(current.Count - historical.Count) / historical.Count) * 100;
                }
            }
        }
    }

    private ExceptionCluster? FindSimilarCluster(
        IEnumerable<ExceptionCluster> clusters,
        string fingerprint,
        LogEntry entry,
        ClusteringOptions options)
    {
        foreach (var cluster in clusters)
        {
            // Exact fingerprint match
            if (cluster.Fingerprint == fingerprint)
                return cluster;

            // Similarity-based matching
            if (options.UseLevenshteinDistance)
            {
                var similarity = CalculateSimilarity(cluster.Fingerprint, fingerprint);
                if (similarity >= options.SimilarityThreshold)
                    return cluster;
            }

            // Exception type matching
            if (!string.IsNullOrEmpty(entry.ExceptionType) &&
                entry.ExceptionType == cluster.ExceptionType)
            {
                var msgSimilarity = CalculateSimilarity(
                    cluster.RepresentativeMessage,
                    entry.Message);
                if (msgSimilarity >= options.SimilarityThreshold)
                    return cluster;
            }
        }

        return null;
    }

    private ExceptionCluster? FindMatchingHistoricalCluster(
        ExceptionCluster current,
        List<ExceptionCluster> historical)
    {
        // First try exact fingerprint match
        var exact = historical.FirstOrDefault(h => h.Fingerprint == current.Fingerprint);
        if (exact != null)
            return exact;

        // Then try exception type + similar message
        if (!string.IsNullOrEmpty(current.ExceptionType))
        {
            return historical
                .Where(h => h.ExceptionType == current.ExceptionType)
                .OrderByDescending(h => CalculateSimilarity(h.RepresentativeMessage, current.RepresentativeMessage))
                .FirstOrDefault(h => CalculateSimilarity(h.RepresentativeMessage, current.RepresentativeMessage) > 0.7);
        }

        return null;
    }

    private static ExceptionCluster CreateCluster(LogEntry entry, string fingerprint)
    {
        return new ExceptionCluster
        {
            Fingerprint = fingerprint,
            ExceptionType = entry.ExceptionType,
            RepresentativeMessage = entry.ExceptionMessage ?? entry.Message,
            RepresentativeStackTrace = entry.StackTrace,
            Entries = new List<LogEntry> { entry }
        };
    }

    private static void AssignSeverity(ExceptionCluster cluster)
    {
        // Base severity on count and log levels
        var hasCritical = cluster.Entries.Any(e => e.Level == ModelLogLevel.Critical);
        var errorCount = cluster.Count;

        cluster.Severity = (hasCritical, errorCount) switch
        {
            (true, _) => ClusterSeverity.Critical,
            (_, > 100) => ClusterSeverity.Critical,
            (_, > 50) => ClusterSeverity.High,
            (_, > 10) => ClusterSeverity.Medium,
            _ => ClusterSeverity.Low
        };
    }

    private static void GenerateTitle(ExceptionCluster cluster)
    {
        if (!string.IsNullOrEmpty(cluster.ExceptionType))
        {
            var typeName = cluster.ExceptionType.Split('.').LastOrDefault() ?? cluster.ExceptionType;
            cluster.Title = $"{typeName}: {TruncateMessage(cluster.RepresentativeMessage, 60)}";
        }
        else
        {
            cluster.Title = TruncateMessage(cluster.RepresentativeMessage, 80);
        }
    }

    private static string TruncateMessage(string message, int maxLength)
    {
        if (string.IsNullOrEmpty(message))
            return "Unknown error";

        // Get first line only
        var firstLine = message.Split('\n').FirstOrDefault() ?? message;

        if (firstLine.Length <= maxLength)
            return firstLine;

        return firstLine[..(maxLength - 3)] + "...";
    }

    /// <summary>
    /// Calculates similarity between two strings using Levenshtein distance.
    /// Returns value between 0.0 (completely different) and 1.0 (identical).
    /// </summary>
    private static double CalculateSimilarity(string s1, string s2)
    {
        if (string.IsNullOrEmpty(s1) && string.IsNullOrEmpty(s2))
            return 1.0;
        if (string.IsNullOrEmpty(s1) || string.IsNullOrEmpty(s2))
            return 0.0;

        // Normalize strings
        s1 = s1.ToLowerInvariant();
        s2 = s2.ToLowerInvariant();

        if (s1 == s2)
            return 1.0;

        var maxLen = Math.Max(s1.Length, s2.Length);
        var distance = LevenshteinDistance(s1, s2);

        return 1.0 - ((double)distance / maxLen);
    }

    /// <summary>
    /// Calculates the Levenshtein distance between two strings.
    /// </summary>
    private static int LevenshteinDistance(string s1, string s2)
    {
        var n = s1.Length;
        var m = s2.Length;

        if (n == 0) return m;
        if (m == 0) return n;

        // Use single array for space efficiency
        var current = new int[m + 1];
        var previous = new int[m + 1];

        for (var j = 0; j <= m; j++)
            previous[j] = j;

        for (var i = 1; i <= n; i++)
        {
            current[0] = i;

            for (var j = 1; j <= m; j++)
            {
                var cost = s1[i - 1] == s2[j - 1] ? 0 : 1;
                current[j] = Math.Min(
                    Math.Min(current[j - 1] + 1, previous[j] + 1),
                    previous[j - 1] + cost);
            }

            (previous, current) = (current, previous);
        }

        return previous[m];
    }
}
