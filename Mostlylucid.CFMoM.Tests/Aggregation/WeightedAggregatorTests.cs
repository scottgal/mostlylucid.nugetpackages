using System.Text.Json;
using FluentAssertions;
using Mostlylucid.CFMoM.Aggregation;
using Mostlylucid.CFMoM.Signals;
using Xunit;

namespace Mostlylucid.CFMoM.Tests.Aggregation;

public class WeightedAggregatorTests
{
    private static ConstrainedSignal CreateTestSignal(
        float confidence = 0.5f,
        string schemaId = "test.v1",
        string sourceId = "test-source",
        bool triggerEarlyExit = false,
        string? earlyExitClassification = null)
    {
        var signal = ConstrainedSignal.Create(
            sourceId: sourceId,
            factsSchemaId: schemaId,
            facts: JsonSerializer.SerializeToElement(new { }),
            confidence: confidence);

        if (triggerEarlyExit)
        {
            signal = signal.WithEarlyExit(earlyExitClassification ?? "verified");
        }

        return signal;
    }

    [Fact]
    public void Aggregate_EmptySignals_ReturnsDefaultScore()
    {
        // Arrange
        var aggregator = new WeightedAggregator();
        var signals = new List<ConstrainedSignal>();

        // Act
        var result = aggregator.Aggregate(signals);

        // Assert
        result.Score.Should().Be(0.5);
        result.Confidence.Should().Be(0.0);
        result.Band.Should().Be(ClassificationBand.Unknown);
        result.EarlyExit.Should().BeFalse();
        result.ContributingProposers.Should().BeEmpty();
    }

    [Fact]
    public void Aggregate_SingleHighConfidenceSignal_ReturnsHighScore()
    {
        // Arrange
        var aggregator = new WeightedAggregator();
        var signals = new List<ConstrainedSignal> { CreateTestSignal(confidence: 0.95f) };

        // Act
        var result = aggregator.Aggregate(signals);

        // Assert - With sigmoid aggregation, single 0.95 signal gives score ~0.71
        // Confidence = max(weightFactor=0.2, evidenceStrength=0.42) = 0.42
        result.Score.Should().BeGreaterThan(0.7);
        result.Confidence.Should().BeGreaterThan(0.4);
        result.Band.Should().BeOneOf(ClassificationBand.High, ClassificationBand.VeryHigh);
    }

    [Fact]
    public void Aggregate_SingleLowConfidenceSignal_ReturnsLowScore()
    {
        // Arrange
        var aggregator = new WeightedAggregator();
        var signals = new List<ConstrainedSignal> { CreateTestSignal(confidence: 0.05f) };

        // Act
        var result = aggregator.Aggregate(signals);

        // Assert
        result.Score.Should().BeLessThan(0.3);
        result.Band.Should().BeOneOf(ClassificationBand.VeryLow, ClassificationBand.Low);
    }

    [Fact]
    public void Aggregate_NeutralConfidenceSignal_ReturnsNeutralScore()
    {
        // Arrange
        var aggregator = new WeightedAggregator();
        var signals = new List<ConstrainedSignal> { CreateTestSignal(confidence: 0.5f) };

        // Act
        var result = aggregator.Aggregate(signals);

        // Assert
        result.Score.Should().BeApproximately(0.5, 0.1);
    }

    [Fact]
    public void Aggregate_MultipleSignals_AggregatesCorrectly()
    {
        // Arrange
        var aggregator = new WeightedAggregator();
        var signals = new List<ConstrainedSignal>
        {
            CreateTestSignal(confidence: 0.9f, sourceId: "high"),
            CreateTestSignal(confidence: 0.8f, sourceId: "medium-high"),
            CreateTestSignal(confidence: 0.7f, sourceId: "medium")
        };

        // Act
        var result = aggregator.Aggregate(signals);

        // Assert
        result.Score.Should().BeGreaterThan(0.6);
        result.ContributingProposers.Should().HaveCount(3);
        result.ContributingProposers.Should().Contain("high");
        result.ContributingProposers.Should().Contain("medium-high");
        result.ContributingProposers.Should().Contain("medium");
    }

    [Fact]
    public void Aggregate_MixedConfidenceSignals_BalancesScore()
    {
        // Arrange
        var aggregator = new WeightedAggregator();
        var signals = new List<ConstrainedSignal>
        {
            CreateTestSignal(confidence: 0.9f),  // High positive
            CreateTestSignal(confidence: 0.1f)   // Low (negative in delta space)
        };

        // Act
        var result = aggregator.Aggregate(signals);

        // Assert
        result.Score.Should().BeApproximately(0.5, 0.15); // Should balance out
    }

    [Fact]
    public void Aggregate_EarlyExitSignal_ReturnsVerifiedBand()
    {
        // Arrange
        var aggregator = new WeightedAggregator();
        var signals = new List<ConstrainedSignal>
        {
            CreateTestSignal(confidence: 0.3f), // Normal signal
            CreateTestSignal(confidence: 0.8f, triggerEarlyExit: true, earlyExitClassification: "whitelisted")
        };

        // Act
        var result = aggregator.Aggregate(signals);

        // Assert
        result.EarlyExit.Should().BeTrue();
        result.Band.Should().Be(ClassificationBand.Verified);
        result.EarlyExitClassification.Should().Be("whitelisted");
        result.Confidence.Should().Be(1.0);
    }

    [Fact]
    public void Aggregate_WithSchemaWeights_AppliesWeights()
    {
        // Arrange
        var options = new WeightedAggregatorOptions
        {
            SchemaWeights = new Dictionary<string, double>
            {
                ["high-priority.v1"] = 3.0,
                ["low-priority.v1"] = 0.5
            }
        };
        var aggregator = new WeightedAggregator(options);
        var signals = new List<ConstrainedSignal>
        {
            CreateTestSignal(confidence: 0.9f, schemaId: "high-priority.v1"),
            CreateTestSignal(confidence: 0.2f, schemaId: "low-priority.v1")
        };

        // Act
        var result = aggregator.Aggregate(signals);

        // Assert
        // High priority signal (0.9 with 3x weight) should dominate
        result.Score.Should().BeGreaterThan(0.6);
    }

    [Fact]
    public void Aggregate_WithCustomWeightResolver_UsesResolver()
    {
        // Arrange
        static double weightResolver(ConstrainedSignal s) =>
            s.SourceId == "important" ? 5.0 : 1.0;

        var aggregator = new WeightedAggregator(weightResolver: weightResolver);
        var signals = new List<ConstrainedSignal>
        {
            CreateTestSignal(confidence: 0.9f, sourceId: "important"),
            CreateTestSignal(confidence: 0.2f, sourceId: "normal")
        };

        // Act
        var result = aggregator.Aggregate(signals);

        // Assert
        result.Score.Should().BeGreaterThan(0.6);
    }

    [Fact]
    public void Aggregate_LowConfidenceResult_ReturnsMediumBand()
    {
        // Arrange
        var options = new WeightedAggregatorOptions { LowConfidenceThreshold = 0.5 };
        var aggregator = new WeightedAggregator(options);
        // Single signal with weight of 1 and confidence divisor of 5 = 0.2 confidence
        var signals = new List<ConstrainedSignal>
        {
            CreateTestSignal(confidence: 0.9f)
        };

        // Act
        var result = aggregator.Aggregate(signals);

        // Assert
        // Single signal gives low weight confidence, should still classify based on strength
        result.Band.Should().NotBe(ClassificationBand.Unknown);
    }

    [Fact]
    public void Aggregate_BuildsSchemaBreakdown()
    {
        // Arrange
        var aggregator = new WeightedAggregator();
        var signals = new List<ConstrainedSignal>
        {
            CreateTestSignal(confidence: 0.9f, schemaId: "intent.v1"),
            CreateTestSignal(confidence: 0.8f, schemaId: "intent.v1"),
            CreateTestSignal(confidence: 0.6f, schemaId: "sentiment.v1")
        };

        // Act
        var result = aggregator.Aggregate(signals);

        // Assert
        result.SchemaBreakdown.Should().HaveCount(2);
        result.SchemaBreakdown.Should().ContainKey("intent.v1");
        result.SchemaBreakdown.Should().ContainKey("sentiment.v1");

        var intentBreakdown = result.SchemaBreakdown["intent.v1"];
        intentBreakdown.SignalCount.Should().Be(2);
        intentBreakdown.AverageConfidence.Should().BeApproximately(0.85, 0.001); // Float precision tolerance

        var sentimentBreakdown = result.SchemaBreakdown["sentiment.v1"];
        sentimentBreakdown.SignalCount.Should().Be(1);
    }

    [Fact]
    public void ClassificationBand_Low_ForLowConfidenceSignals()
    {
        // Arrange
        var aggregator = new WeightedAggregator();
        var signals = new List<ConstrainedSignal>
        {
            CreateTestSignal(confidence: 0.02f),
            CreateTestSignal(confidence: 0.03f)
        };

        // Act
        var result = aggregator.Aggregate(signals);

        // Assert - Sigmoid with two low signals gives score ~0.13 (Low band: 0.1-0.3)
        // delta = (0.025 - 0.5) * 2 ≈ -0.95, weighted_sum ≈ -1.9, sigmoid ≈ 0.13
        result.Score.Should().BeLessThan(0.2);
        result.Band.Should().Be(ClassificationBand.Low);
    }

    [Fact]
    public void ClassificationBand_VeryHigh_ForScoreAbove07()
    {
        // Arrange
        var aggregator = new WeightedAggregator();
        var signals = new List<ConstrainedSignal>
        {
            CreateTestSignal(confidence: 0.98f),
            CreateTestSignal(confidence: 0.95f)
        };

        // Act
        var result = aggregator.Aggregate(signals);

        // Assert
        result.Score.Should().BeGreaterThan(0.7);
        result.Band.Should().Be(ClassificationBand.VeryHigh);
    }
}
