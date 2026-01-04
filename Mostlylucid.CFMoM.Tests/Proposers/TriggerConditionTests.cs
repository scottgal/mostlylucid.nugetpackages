using FluentAssertions;
using Mostlylucid.CFMoM.Proposers;
using Xunit;

namespace Mostlylucid.CFMoM.Tests.Proposers;

public class TriggerConditionTests
{
    [Fact]
    public void SignalExistsTrigger_SignalExists_ReturnsTrue()
    {
        // Arrange
        var trigger = new SignalExistsTrigger("my-signal");
        var signals = new Dictionary<string, object> { ["my-signal"] = "value" };

        // Act & Assert
        trigger.IsSatisfied(signals).Should().BeTrue();
    }

    [Fact]
    public void SignalExistsTrigger_SignalDoesNotExist_ReturnsFalse()
    {
        // Arrange
        var trigger = new SignalExistsTrigger("my-signal");
        var signals = new Dictionary<string, object> { ["other-signal"] = "value" };

        // Act & Assert
        trigger.IsSatisfied(signals).Should().BeFalse();
    }

    [Fact]
    public void SignalExistsTrigger_Description_IsCorrect()
    {
        // Arrange
        var trigger = new SignalExistsTrigger("test-signal");

        // Assert
        trigger.Description.Should().Contain("test-signal");
    }

    [Fact]
    public void SignalValueTrigger_MatchingValue_ReturnsTrue()
    {
        // Arrange
        var trigger = new SignalValueTrigger<string>("status", "active");
        var signals = new Dictionary<string, object> { ["status"] = "active" };

        // Act & Assert
        trigger.IsSatisfied(signals).Should().BeTrue();
    }

    [Fact]
    public void SignalValueTrigger_NonMatchingValue_ReturnsFalse()
    {
        // Arrange
        var trigger = new SignalValueTrigger<string>("status", "active");
        var signals = new Dictionary<string, object> { ["status"] = "inactive" };

        // Act & Assert
        trigger.IsSatisfied(signals).Should().BeFalse();
    }

    [Fact]
    public void SignalValueTrigger_WrongType_ReturnsFalse()
    {
        // Arrange
        var trigger = new SignalValueTrigger<int>("count", 5);
        var signals = new Dictionary<string, object> { ["count"] = "five" }; // String instead of int

        // Act & Assert
        trigger.IsSatisfied(signals).Should().BeFalse();
    }

    [Fact]
    public void SignalValueTrigger_SignalMissing_ReturnsFalse()
    {
        // Arrange
        var trigger = new SignalValueTrigger<string>("status", "active");
        var signals = new Dictionary<string, object>();

        // Act & Assert
        trigger.IsSatisfied(signals).Should().BeFalse();
    }

    [Fact]
    public void SignalPredicateTrigger_PredicateTrue_ReturnsTrue()
    {
        // Arrange
        var trigger = new SignalPredicateTrigger<int>("score", s => s > 50, "> 50");
        var signals = new Dictionary<string, object> { ["score"] = 75 };

        // Act & Assert
        trigger.IsSatisfied(signals).Should().BeTrue();
    }

    [Fact]
    public void SignalPredicateTrigger_PredicateFalse_ReturnsFalse()
    {
        // Arrange
        var trigger = new SignalPredicateTrigger<int>("score", s => s > 50, "> 50");
        var signals = new Dictionary<string, object> { ["score"] = 25 };

        // Act & Assert
        trigger.IsSatisfied(signals).Should().BeFalse();
    }

    [Fact]
    public void SignalPredicateTrigger_Description_IncludesPredicateDescription()
    {
        // Arrange
        var trigger = new SignalPredicateTrigger<double>("temperature", t => t < 100, "< 100°C");

        // Assert
        trigger.Description.Should().Contain("< 100°C");
    }

    [Fact]
    public void AnyOfTrigger_OneConditionMet_ReturnsTrue()
    {
        // Arrange
        var trigger = new AnyOfTrigger(new TriggerCondition[]
        {
            new SignalExistsTrigger("signal-a"),
            new SignalExistsTrigger("signal-b")
        });
        var signals = new Dictionary<string, object> { ["signal-b"] = true };

        // Act & Assert
        trigger.IsSatisfied(signals).Should().BeTrue();
    }

    [Fact]
    public void AnyOfTrigger_NoConditionsMet_ReturnsFalse()
    {
        // Arrange
        var trigger = new AnyOfTrigger(new TriggerCondition[]
        {
            new SignalExistsTrigger("signal-a"),
            new SignalExistsTrigger("signal-b")
        });
        var signals = new Dictionary<string, object> { ["signal-c"] = true };

        // Act & Assert
        trigger.IsSatisfied(signals).Should().BeFalse();
    }

    [Fact]
    public void AllOfTrigger_AllConditionsMet_ReturnsTrue()
    {
        // Arrange
        var trigger = new AllOfTrigger(new TriggerCondition[]
        {
            new SignalExistsTrigger("signal-a"),
            new SignalExistsTrigger("signal-b")
        });
        var signals = new Dictionary<string, object>
        {
            ["signal-a"] = true,
            ["signal-b"] = true
        };

        // Act & Assert
        trigger.IsSatisfied(signals).Should().BeTrue();
    }

    [Fact]
    public void AllOfTrigger_SomeConditionsNotMet_ReturnsFalse()
    {
        // Arrange
        var trigger = new AllOfTrigger(new TriggerCondition[]
        {
            new SignalExistsTrigger("signal-a"),
            new SignalExistsTrigger("signal-b")
        });
        var signals = new Dictionary<string, object> { ["signal-a"] = true };

        // Act & Assert
        trigger.IsSatisfied(signals).Should().BeFalse();
    }

    [Fact]
    public void ProposerCountTrigger_EnoughProposers_ReturnsTrue()
    {
        // Arrange
        var trigger = new ProposerCountTrigger(3);
        var signals = new Dictionary<string, object>
        {
            [ProposerCountTrigger.CompletedProposersSignal] = 5
        };

        // Act & Assert
        trigger.IsSatisfied(signals).Should().BeTrue();
    }

    [Fact]
    public void ProposerCountTrigger_NotEnoughProposers_ReturnsFalse()
    {
        // Arrange
        var trigger = new ProposerCountTrigger(3);
        var signals = new Dictionary<string, object>
        {
            [ProposerCountTrigger.CompletedProposersSignal] = 2
        };

        // Act & Assert
        trigger.IsSatisfied(signals).Should().BeFalse();
    }

    [Fact]
    public void ProposerCountTrigger_SignalMissing_ReturnsFalse()
    {
        // Arrange
        var trigger = new ProposerCountTrigger(3);
        var signals = new Dictionary<string, object>();

        // Act & Assert
        trigger.IsSatisfied(signals).Should().BeFalse();
    }

    [Fact]
    public void ScoreThresholdTrigger_ScoreExceedsThreshold_ReturnsTrue()
    {
        // Arrange
        var trigger = new ScoreThresholdTrigger(0.7);
        var signals = new Dictionary<string, object>
        {
            [ScoreThresholdTrigger.CurrentScoreSignal] = 0.85
        };

        // Act & Assert
        trigger.IsSatisfied(signals).Should().BeTrue();
    }

    [Fact]
    public void ScoreThresholdTrigger_ScoreBelowThreshold_ReturnsFalse()
    {
        // Arrange
        var trigger = new ScoreThresholdTrigger(0.7);
        var signals = new Dictionary<string, object>
        {
            [ScoreThresholdTrigger.CurrentScoreSignal] = 0.5
        };

        // Act & Assert
        trigger.IsSatisfied(signals).Should().BeFalse();
    }

    [Fact]
    public void Triggers_WhenSignalExists_CreatesCorrectTrigger()
    {
        // Act
        var trigger = Triggers.WhenSignalExists("my-key");

        // Assert
        trigger.Should().BeOfType<SignalExistsTrigger>();
        trigger.Description.Should().Contain("my-key");
    }

    [Fact]
    public void Triggers_WhenSignalEquals_CreatesCorrectTrigger()
    {
        // Act
        var trigger = Triggers.WhenSignalEquals("status", "ready");

        // Assert
        trigger.Should().BeOfType<SignalValueTrigger<string>>();
    }

    [Fact]
    public void Triggers_WhenSignal_CreatesPredicateTrigger()
    {
        // Act
        var trigger = Triggers.WhenSignal<int>("count", c => c >= 10, ">= 10");

        // Assert
        trigger.Should().BeOfType<SignalPredicateTrigger<int>>();
    }

    [Fact]
    public void Triggers_AnyOf_CreatesAnyOfTrigger()
    {
        // Act
        var trigger = Triggers.AnyOf(
            Triggers.WhenSignalExists("a"),
            Triggers.WhenSignalExists("b"));

        // Assert
        trigger.Should().BeOfType<AnyOfTrigger>();
    }

    [Fact]
    public void Triggers_AllOf_CreatesAllOfTrigger()
    {
        // Act
        var trigger = Triggers.AllOf(
            Triggers.WhenSignalExists("a"),
            Triggers.WhenSignalExists("b"));

        // Assert
        trigger.Should().BeOfType<AllOfTrigger>();
    }

    [Fact]
    public void Triggers_WhenProposerCount_CreatesProposerCountTrigger()
    {
        // Act
        var trigger = Triggers.WhenProposerCount(5);

        // Assert
        trigger.Should().BeOfType<ProposerCountTrigger>();
    }

    [Fact]
    public void Triggers_WhenScoreExceeds_CreatesScoreThresholdTrigger()
    {
        // Act
        var trigger = Triggers.WhenScoreExceeds(0.8);

        // Assert
        trigger.Should().BeOfType<ScoreThresholdTrigger>();
    }

    [Fact]
    public void Triggers_None_ReturnsEmptyList()
    {
        // Assert
        Triggers.None.Should().BeEmpty();
    }

    [Fact]
    public void NestedTriggers_AnyOfWithAllOf_WorksCorrectly()
    {
        // Arrange
        var trigger = Triggers.AnyOf(
            Triggers.AllOf(
                Triggers.WhenSignalExists("a"),
                Triggers.WhenSignalExists("b")),
            Triggers.WhenSignalEquals("override", true));

        var signals1 = new Dictionary<string, object> { ["a"] = 1, ["b"] = 2 };
        var signals2 = new Dictionary<string, object> { ["override"] = true };
        var signals3 = new Dictionary<string, object> { ["a"] = 1 };

        // Assert
        trigger.IsSatisfied(signals1).Should().BeTrue();  // All of (a, b) met
        trigger.IsSatisfied(signals2).Should().BeTrue();  // Override met
        trigger.IsSatisfied(signals3).Should().BeFalse(); // Neither condition fully met
    }
}
