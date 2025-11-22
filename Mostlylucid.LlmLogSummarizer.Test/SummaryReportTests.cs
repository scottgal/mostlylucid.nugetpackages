using Mostlylucid.LlmLogSummarizer.Models;

namespace Mostlylucid.LlmLogSummarizer.Test;

public class SummaryReportTests
{
    [Fact]
    public void SummaryReport_HasUniqueId()
    {
        // Arrange & Act
        var report1 = new SummaryReport();
        var report2 = new SummaryReport();

        // Assert
        Assert.NotEqual(report1.Id, report2.Id);
    }

    [Fact]
    public void SummaryReport_GeneratedAt_DefaultsToNow()
    {
        // Arrange
        var before = DateTimeOffset.UtcNow;

        // Act
        var report = new SummaryReport();

        // Assert
        Assert.True(report.GeneratedAt >= before);
        Assert.True(report.GeneratedAt <= DateTimeOffset.UtcNow);
    }

    [Fact]
    public void SummaryReport_ListsDefaultToEmpty()
    {
        // Arrange & Act
        var report = new SummaryReport();

        // Assert
        Assert.Empty(report.TopErrorPatterns);
        Assert.Empty(report.NewErrorTypes);
        Assert.Empty(report.TrendingUp);
        Assert.Empty(report.TrendingDown);
        Assert.Empty(report.AllClusters);
        Assert.Empty(report.KeyInsights);
        Assert.Empty(report.RecommendedActions);
        Assert.Empty(report.SourcesAnalyzed);
    }

    [Fact]
    public void SummaryReport_DefaultHealth_IsUnknown()
    {
        // Arrange & Act
        var report = new SummaryReport();

        // Assert
        Assert.Equal(HealthStatus.Unknown, report.OverallHealth);
    }

    [Fact]
    public void ExceptionCluster_Count_ReflectsEntries()
    {
        // Arrange
        var cluster = new ExceptionCluster
        {
            Entries = new List<LogEntry>
            {
                new() { Timestamp = DateTimeOffset.UtcNow },
                new() { Timestamp = DateTimeOffset.UtcNow },
                new() { Timestamp = DateTimeOffset.UtcNow }
            }
        };

        // Assert
        Assert.Equal(3, cluster.Count);
    }

    [Fact]
    public void ExceptionCluster_FirstAndLastOccurrence_AreCorrect()
    {
        // Arrange
        var now = DateTimeOffset.UtcNow;
        var cluster = new ExceptionCluster
        {
            Entries = new List<LogEntry>
            {
                new() { Timestamp = now.AddHours(-2) },
                new() { Timestamp = now.AddHours(-1) },
                new() { Timestamp = now }
            }
        };

        // Assert
        Assert.Equal(now.AddHours(-2), cluster.FirstOccurrence);
        Assert.Equal(now, cluster.LastOccurrence);
    }

    [Fact]
    public void ExceptionCluster_SourceContexts_AreDistinct()
    {
        // Arrange
        var cluster = new ExceptionCluster
        {
            Entries = new List<LogEntry>
            {
                new() { SourceContext = "ServiceA" },
                new() { SourceContext = "ServiceA" },
                new() { SourceContext = "ServiceB" },
                new() { SourceContext = null }
            }
        };

        // Assert
        Assert.Equal(2, cluster.SourceContexts.Count);
        Assert.Contains("ServiceA", cluster.SourceContexts);
        Assert.Contains("ServiceB", cluster.SourceContexts);
    }

    [Fact]
    public void SummarizationStats_DefaultsToZero()
    {
        // Arrange & Act
        var stats = new SummarizationStats();

        // Assert
        Assert.Equal(TimeSpan.Zero, stats.CollectionDuration);
        Assert.Equal(TimeSpan.Zero, stats.ClusteringDuration);
        Assert.Equal(TimeSpan.Zero, stats.LlmSummarizationDuration);
        Assert.Equal(TimeSpan.Zero, stats.TotalDuration);
        Assert.Equal(0, stats.LlmCallCount);
        Assert.Equal(0, stats.TotalTokensUsed);
    }
}
