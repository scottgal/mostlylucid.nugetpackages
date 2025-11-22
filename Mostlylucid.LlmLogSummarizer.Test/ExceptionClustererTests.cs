using Microsoft.Extensions.Logging;
using Mostlylucid.LlmLogSummarizer.Clustering;
using Mostlylucid.LlmLogSummarizer.Models;

namespace Mostlylucid.LlmLogSummarizer.Test;

public class ExceptionClustererTests
{
    private readonly ExceptionClusterer _clusterer;

    public ExceptionClustererTests()
    {
        var logger = new Mock<ILogger<ExceptionClusterer>>();
        _clusterer = new ExceptionClusterer(logger.Object);
    }

    [Fact]
    public void ClusterExceptions_WithNoEntries_ReturnsEmptyClusters()
    {
        // Arrange
        var entries = new List<LogEntry>();
        var options = new ClusteringOptions();

        // Act
        var clusters = _clusterer.ClusterExceptions(entries, options);

        // Assert
        Assert.Empty(clusters);
    }

    [Fact]
    public void ClusterExceptions_WithSingleError_CreatesSingleCluster()
    {
        // Arrange
        var entries = new List<LogEntry>
        {
            new()
            {
                Level = LogLevel.Error,
                Message = "Test error message",
                ExceptionType = "System.NullReferenceException",
                Timestamp = DateTimeOffset.UtcNow
            }
        };
        var options = new ClusteringOptions { MinClusterSize = 1 };

        // Act
        var clusters = _clusterer.ClusterExceptions(entries, options);

        // Assert
        Assert.Single(clusters);
        Assert.Equal("System.NullReferenceException", clusters[0].ExceptionType);
        Assert.Single(clusters[0].Entries);
    }

    [Fact]
    public void ClusterExceptions_GroupsSimilarErrors()
    {
        // Arrange
        var entries = new List<LogEntry>
        {
            new()
            {
                Level = LogLevel.Error,
                Message = "User 123 not found",
                ExceptionType = "System.ArgumentException",
                SourceContext = "UserService",
                Timestamp = DateTimeOffset.UtcNow
            },
            new()
            {
                Level = LogLevel.Error,
                Message = "User 456 not found",
                ExceptionType = "System.ArgumentException",
                SourceContext = "UserService",
                Timestamp = DateTimeOffset.UtcNow
            },
            new()
            {
                Level = LogLevel.Error,
                Message = "User 789 not found",
                ExceptionType = "System.ArgumentException",
                SourceContext = "UserService",
                Timestamp = DateTimeOffset.UtcNow
            }
        };
        var options = new ClusteringOptions
        {
            SimilarityThreshold = 0.7,
            UseLevenshteinDistance = true,
            MinClusterSize = 1
        };

        // Act
        var clusters = _clusterer.ClusterExceptions(entries, options);

        // Assert
        Assert.Single(clusters);
        Assert.Equal(3, clusters[0].Count);
    }

    [Fact]
    public void ClusterExceptions_SeparatesDifferentErrorTypes()
    {
        // Arrange
        var entries = new List<LogEntry>
        {
            new()
            {
                Level = LogLevel.Error,
                Message = "Null reference error",
                ExceptionType = "System.NullReferenceException",
                Timestamp = DateTimeOffset.UtcNow
            },
            new()
            {
                Level = LogLevel.Error,
                Message = "Timeout occurred",
                ExceptionType = "System.TimeoutException",
                Timestamp = DateTimeOffset.UtcNow
            }
        };
        var options = new ClusteringOptions { MinClusterSize = 1 };

        // Act
        var clusters = _clusterer.ClusterExceptions(entries, options);

        // Assert
        Assert.Equal(2, clusters.Count);
    }

    [Fact]
    public void ClusterExceptions_IgnoresInfoLevelLogs()
    {
        // Arrange
        var entries = new List<LogEntry>
        {
            new()
            {
                Level = LogLevel.Information,
                Message = "Request processed successfully",
                Timestamp = DateTimeOffset.UtcNow
            },
            new()
            {
                Level = LogLevel.Error,
                Message = "Error occurred",
                ExceptionType = "System.Exception",
                Timestamp = DateTimeOffset.UtcNow
            }
        };
        var options = new ClusteringOptions { MinClusterSize = 1 };

        // Act
        var clusters = _clusterer.ClusterExceptions(entries, options);

        // Assert
        Assert.Single(clusters);
        Assert.Equal("System.Exception", clusters[0].ExceptionType);
    }

    [Fact]
    public void ClusterExceptions_IncludesWarnings()
    {
        // Arrange
        var entries = new List<LogEntry>
        {
            new()
            {
                Level = LogLevel.Warning,
                Message = "Deprecation warning",
                Timestamp = DateTimeOffset.UtcNow
            }
        };
        var options = new ClusteringOptions { MinClusterSize = 1 };

        // Act
        var clusters = _clusterer.ClusterExceptions(entries, options);

        // Assert
        Assert.Single(clusters);
    }

    [Fact]
    public void ClusterExceptions_AssignsCorrectSeverity()
    {
        // Arrange
        var entries = Enumerable.Range(0, 150)
            .Select(_ => new LogEntry
            {
                Level = LogLevel.Error,
                Message = "High frequency error",
                ExceptionType = "System.Exception",
                Timestamp = DateTimeOffset.UtcNow
            })
            .ToList();
        var options = new ClusteringOptions { MinClusterSize = 1 };

        // Act
        var clusters = _clusterer.ClusterExceptions(entries, options);

        // Assert
        Assert.Single(clusters);
        Assert.Equal(ClusterSeverity.Critical, clusters[0].Severity);
    }

    [Fact]
    public void ClusterExceptions_RespectsMaxClusters()
    {
        // Arrange - create many different error types
        var entries = Enumerable.Range(0, 200)
            .Select(i => new LogEntry
            {
                Level = LogLevel.Error,
                Message = $"Unique error {i}",
                ExceptionType = $"Exception{i}",
                Timestamp = DateTimeOffset.UtcNow
            })
            .ToList();
        var options = new ClusteringOptions
        {
            MaxClusters = 50,
            MinClusterSize = 1
        };

        // Act
        var clusters = _clusterer.ClusterExceptions(entries, options);

        // Assert
        Assert.True(clusters.Count <= 50);
    }

    [Fact]
    public void CalculateTrends_IdentifiesNewErrors()
    {
        // Arrange
        var currentClusters = new List<ExceptionCluster>
        {
            new()
            {
                Fingerprint = "new-error",
                ExceptionType = "NewException",
                RepresentativeMessage = "A brand new error"
            }
        };
        var historicalClusters = new List<ExceptionCluster>();

        // Act
        _clusterer.CalculateTrends(currentClusters, historicalClusters);

        // Assert
        Assert.True(currentClusters[0].IsNew);
        Assert.Equal(100, currentClusters[0].TrendPercent);
    }

    [Fact]
    public void CalculateTrends_CalculatesIncreasingTrend()
    {
        // Arrange
        var currentClusters = new List<ExceptionCluster>
        {
            new()
            {
                Fingerprint = "existing-error",
                ExceptionType = "ExistingException",
                RepresentativeMessage = "Known error",
                Entries = Enumerable.Range(0, 20).Select(_ => new LogEntry()).ToList()
            }
        };
        var historicalClusters = new List<ExceptionCluster>
        {
            new()
            {
                Fingerprint = "existing-error",
                ExceptionType = "ExistingException",
                RepresentativeMessage = "Known error",
                Entries = Enumerable.Range(0, 10).Select(_ => new LogEntry()).ToList()
            }
        };

        // Act
        _clusterer.CalculateTrends(currentClusters, historicalClusters);

        // Assert
        Assert.False(currentClusters[0].IsNew);
        Assert.Equal(100, currentClusters[0].TrendPercent); // 100% increase (10 -> 20)
        Assert.Equal(10, currentClusters[0].PreviousPeriodCount);
    }
}
