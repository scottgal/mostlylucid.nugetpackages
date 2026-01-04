using System.Text.Json;
using FluentAssertions;
using Mostlylucid.CFMoM.Aggregation;
using Mostlylucid.CFMoM.Constrainers;
using Mostlylucid.CFMoM.Signals;
using Xunit;

namespace Mostlylucid.CFMoM.Tests.Constrainers;

public class ThresholdConstrainerTests
{
    private static AggregatedResult CreateResult(
        double score,
        double confidence = 0.5,
        ClassificationBand band = ClassificationBand.Medium,
        bool earlyExit = false,
        string? earlyExitClassification = null)
    {
        return new AggregatedResult
        {
            Score = score,
            Confidence = confidence,
            Band = band,
            EarlyExit = earlyExit,
            EarlyExitClassification = earlyExitClassification,
            Signals = new List<ConstrainedSignal>(),
            ContributingProposers = new HashSet<string>()
        };
    }

    [Fact]
    public void Evaluate_ScoreAboveBlockThreshold_ReturnsBlock()
    {
        // Arrange
        var constrainer = new ThresholdConstrainer<string>();
        var result = CreateResult(score: 0.90, confidence: 0.8);

        // Act
        var decision = constrainer.Evaluate(result, "context");

        // Assert
        decision.Decision.Should().Be(CommonDecision.Block);
        decision.ShouldContinue.Should().BeFalse();
        decision.TriggeredBy.Should().Be("immediate-block");
    }

    [Fact]
    public void Evaluate_ScoreBelowAllowThreshold_WithSufficientConfidence_ReturnsAllow()
    {
        // Arrange
        var constrainer = new ThresholdConstrainer<string>();
        var result = CreateResult(score: 0.10, confidence: 0.6);

        // Act
        var decision = constrainer.Evaluate(result, "context");

        // Assert
        decision.Decision.Should().Be(CommonDecision.Allow);
        decision.ShouldContinue.Should().BeFalse();
        decision.TriggeredBy.Should().Be("immediate-allow");
    }

    [Fact]
    public void Evaluate_ScoreBelowAllowThreshold_WithLowConfidence_ReturnsContinue()
    {
        // Arrange
        var constrainer = new ThresholdConstrainer<string>();
        var result = CreateResult(score: 0.10, confidence: 0.3); // Below MinConfidenceForAllow

        // Act
        var decision = constrainer.Evaluate(result, "context");

        // Assert
        decision.Decision.Should().Be(CommonDecision.Allow);
        decision.ShouldContinue.Should().BeTrue(); // Continues because low confidence
    }

    [Fact]
    public void Evaluate_ScoreInChallengeRange_ReturnsChallenge()
    {
        // Arrange
        var constrainer = new ThresholdConstrainer<string>();
        var result = CreateResult(score: 0.75, confidence: 0.8);

        // Act
        var decision = constrainer.Evaluate(result, "context");

        // Assert
        decision.Decision.Should().Be(CommonDecision.Challenge);
        decision.ShouldContinue.Should().BeFalse();
        decision.TriggeredBy.Should().Be("challenge");
    }

    [Fact]
    public void Evaluate_ScoreInEscalateRange_WithLowConfidence_ReturnsContinue()
    {
        // Arrange
        var constrainer = new ThresholdConstrainer<string>();
        var result = CreateResult(score: 0.45, confidence: 0.2); // Below MinConfidenceForDecision

        // Act
        var decision = constrainer.Evaluate(result, "context");

        // Assert
        decision.Decision.Should().Be(CommonDecision.Escalate);
        decision.ShouldContinue.Should().BeTrue(); // Continues to gather more evidence
    }

    [Fact]
    public void Evaluate_ScoreBelowThresholds_ReturnsContinue()
    {
        // Arrange
        var constrainer = new ThresholdConstrainer<string>();
        var result = CreateResult(score: 0.30, confidence: 0.5);

        // Act
        var decision = constrainer.Evaluate(result, "context");

        // Assert
        decision.Decision.Should().Be(CommonDecision.Allow);
        decision.ShouldContinue.Should().BeTrue();
    }

    [Fact]
    public void Evaluate_EarlyExit_WhitelistedClassification_ReturnsAllow()
    {
        // Arrange
        var constrainer = new ThresholdConstrainer<string>();
        var result = CreateResult(score: 0.5, earlyExit: true, earlyExitClassification: "whitelisted");

        // Act
        var decision = constrainer.Evaluate(result, "context");

        // Assert
        decision.Decision.Should().Be(CommonDecision.Allow);
        decision.ShouldContinue.Should().BeFalse();
        decision.TriggeredBy.Should().Be("early-exit");
    }

    [Fact]
    public void Evaluate_EarlyExit_BlacklistedClassification_ReturnsBlock()
    {
        // Arrange
        var constrainer = new ThresholdConstrainer<string>();
        var result = CreateResult(score: 0.5, earlyExit: true, earlyExitClassification: "blacklisted");

        // Act
        var decision = constrainer.Evaluate(result, "context");

        // Assert
        decision.Decision.Should().Be(CommonDecision.Block);
        decision.ShouldContinue.Should().BeFalse();
    }

    [Fact]
    public void Evaluate_EarlyExit_ChallengeClassification_ReturnsChallenge()
    {
        // Arrange
        var constrainer = new ThresholdConstrainer<string>();
        var result = CreateResult(score: 0.5, earlyExit: true, earlyExitClassification: "challenge");

        // Act
        var decision = constrainer.Evaluate(result, "context");

        // Assert
        decision.Decision.Should().Be(CommonDecision.Challenge);
        decision.ShouldContinue.Should().BeFalse();
    }

    [Fact]
    public void Evaluate_EarlyExit_UnknownClassification_DefaultsToAllow()
    {
        // Arrange
        var constrainer = new ThresholdConstrainer<string>();
        var result = CreateResult(score: 0.5, earlyExit: true, earlyExitClassification: "unknown-classification");

        // Act
        var decision = constrainer.Evaluate(result, "context");

        // Assert
        decision.Decision.Should().Be(CommonDecision.Allow);
    }

    [Fact]
    public void Evaluate_CustomThresholds_UsesCustomValues()
    {
        // Arrange
        var options = new ThresholdConstrainerOptions
        {
            ImmediateBlockThreshold = 0.95, // Higher
            ImmediateAllowThreshold = 0.05, // Lower
            ChallengeThreshold = 0.80,
            EscalateThreshold = 0.50
        };
        var constrainer = new ThresholdConstrainer<string>(options);

        // Score of 0.90 should NOT block with custom threshold of 0.95
        var result = CreateResult(score: 0.90, confidence: 0.8);

        // Act
        var decision = constrainer.Evaluate(result, "context");

        // Assert
        decision.Decision.Should().Be(CommonDecision.Challenge); // 0.90 >= 0.80 challenge threshold
    }

    [Fact]
    public void ConstrainerResult_Continue_SetsCorrectProperties()
    {
        // Act
        var result = ConstrainerResult<CommonDecision>.Continue(CommonDecision.Escalate, "Need more evidence");

        // Assert
        result.Decision.Should().Be(CommonDecision.Escalate);
        result.ShouldContinue.Should().BeTrue();
        result.Reason.Should().Be("Need more evidence");
        result.TriggeredBy.Should().BeNull();
    }

    [Fact]
    public void ConstrainerResult_Stop_SetsCorrectProperties()
    {
        // Act
        var result = ConstrainerResult<CommonDecision>.Stop(
            CommonDecision.Block,
            "Score exceeded threshold",
            "rule-1");

        // Assert
        result.Decision.Should().Be(CommonDecision.Block);
        result.ShouldContinue.Should().BeFalse();
        result.Reason.Should().Be("Score exceeded threshold");
        result.TriggeredBy.Should().Be("rule-1");
    }
}
