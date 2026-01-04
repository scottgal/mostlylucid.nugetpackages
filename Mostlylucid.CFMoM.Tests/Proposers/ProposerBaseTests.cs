using System.Text.Json;
using FluentAssertions;
using Mostlylucid.CFMoM.Proposers;
using Mostlylucid.CFMoM.Signals;
using Xunit;

namespace Mostlylucid.CFMoM.Tests.Proposers;

public class ProposerBaseTests
{
    private class TestProposer : ProposerBase<string>
    {
        public override string Name => "test-proposer";
        public override string FactsSchemaId => "test.v1";

        public override Task<IReadOnlyList<ConstrainedSignal>> ProposeAsync(
            ProposerState<string> state,
            CancellationToken cancellationToken = default)
        {
            return Task.FromResult<IReadOnlyList<ConstrainedSignal>>(None());
        }

        // Expose protected methods for testing
        public IReadOnlyList<ConstrainedSignal> TestSingle(ConstrainedSignal signal) => Single(signal);
        public IReadOnlyList<ConstrainedSignal> TestMultiple(params ConstrainedSignal[] signals) => Multiple(signals);
        public IReadOnlyList<ConstrainedSignal> TestNone() => None();

        public ConstrainedSignal TestCreateSignal<T>(T facts, float confidence) =>
            CreateSignal(facts, confidence);
    }

    private class CustomPriorityProposer : ProposerBase<string>
    {
        public override string Name => "custom-priority";
        public override string FactsSchemaId => "custom.v1";
        public override int Priority => 10; // Higher priority (lower number)
        public override bool IsEnabled => true;
        public override TimeSpan TriggerTimeout => TimeSpan.FromSeconds(1);
        public override TimeSpan ExecutionTimeout => TimeSpan.FromSeconds(5);
        public override bool IsOptional => false; // Required proposer

        public override IReadOnlyList<TriggerCondition> TriggerConditions =>
            new[] { Triggers.WhenSignalExists("prerequisite") };

        public override Task<IReadOnlyList<ConstrainedSignal>> ProposeAsync(
            ProposerState<string> state,
            CancellationToken cancellationToken = default)
        {
            return Task.FromResult<IReadOnlyList<ConstrainedSignal>>(None());
        }
    }

    [Fact]
    public void DefaultPriority_Is100()
    {
        // Arrange
        var proposer = new TestProposer();

        // Assert
        proposer.Priority.Should().Be(100);
    }

    [Fact]
    public void DefaultIsEnabled_IsTrue()
    {
        // Arrange
        var proposer = new TestProposer();

        // Assert
        proposer.IsEnabled.Should().BeTrue();
    }

    [Fact]
    public void DefaultTriggerConditions_IsEmpty()
    {
        // Arrange
        var proposer = new TestProposer();

        // Assert
        proposer.TriggerConditions.Should().BeEmpty();
    }

    [Fact]
    public void DefaultTriggerTimeout_Is500ms()
    {
        // Arrange
        var proposer = new TestProposer();

        // Assert
        proposer.TriggerTimeout.Should().Be(TimeSpan.FromMilliseconds(500));
    }

    [Fact]
    public void DefaultExecutionTimeout_Is2Seconds()
    {
        // Arrange
        var proposer = new TestProposer();

        // Assert
        proposer.ExecutionTimeout.Should().Be(TimeSpan.FromSeconds(2));
    }

    [Fact]
    public void DefaultIsOptional_IsTrue()
    {
        // Arrange
        var proposer = new TestProposer();

        // Assert
        proposer.IsOptional.Should().BeTrue();
    }

    [Fact]
    public void CustomProposer_CanOverrideDefaults()
    {
        // Arrange
        var proposer = new CustomPriorityProposer();

        // Assert
        proposer.Priority.Should().Be(10);
        proposer.IsOptional.Should().BeFalse();
        proposer.TriggerTimeout.Should().Be(TimeSpan.FromSeconds(1));
        proposer.ExecutionTimeout.Should().Be(TimeSpan.FromSeconds(5));
        proposer.TriggerConditions.Should().NotBeEmpty();
    }

    [Fact]
    public void Single_ReturnsListWithOneSignal()
    {
        // Arrange
        var proposer = new TestProposer();
        var signal = ConstrainedSignal.Create(
            sourceId: "test",
            factsSchemaId: "test.v1",
            facts: JsonSerializer.SerializeToElement(new { }));

        // Act
        var result = proposer.TestSingle(signal);

        // Assert
        result.Should().HaveCount(1);
        result[0].Should().BeSameAs(signal);
    }

    [Fact]
    public void Multiple_ReturnsListWithAllSignals()
    {
        // Arrange
        var proposer = new TestProposer();
        var signal1 = ConstrainedSignal.Create("s1", "s.v1", JsonSerializer.SerializeToElement(new { }));
        var signal2 = ConstrainedSignal.Create("s2", "s.v1", JsonSerializer.SerializeToElement(new { }));

        // Act
        var result = proposer.TestMultiple(signal1, signal2);

        // Assert
        result.Should().HaveCount(2);
    }

    [Fact]
    public void None_ReturnsEmptyList()
    {
        // Arrange
        var proposer = new TestProposer();

        // Act
        var result = proposer.TestNone();

        // Assert
        result.Should().BeEmpty();
    }

    [Fact]
    public void CreateSignal_SetsSourceToProposerName()
    {
        // Arrange
        var proposer = new TestProposer();
        // Use PascalCase to match TestFacts property name (System.Text.Json is case-sensitive by default)
        var facts = new { Intent = "question" };

        // Act
        var signal = proposer.TestCreateSignal(facts, 0.85f);

        // Assert
        signal.SourceId.Should().Be("test-proposer");
        signal.FactsSchemaId.Should().Be("test.v1");
        signal.Confidence.Should().Be(0.85f);

        var deserializedFacts = signal.GetFacts<TestFacts>();
        deserializedFacts!.Intent.Should().Be("question");
    }

    [Fact]
    public void ProposerState_GetSignal_ReturnsTypedValue()
    {
        // Arrange
        var state = new ProposerState<string>
        {
            Context = "test-context",
            Signals = new Dictionary<string, object> { ["count"] = 42 },
            CollectedSignals = new List<ConstrainedSignal>(),
            CompletedProposers = new HashSet<string>(),
            FailedProposers = new HashSet<string>(),
            CorrelationId = "corr-123"
        };

        // Act
        var count = state.GetSignal<int>("count");

        // Assert
        count.Should().Be(42);
    }

    [Fact]
    public void ProposerState_GetSignal_WrongType_ReturnsDefault()
    {
        // Arrange
        var state = new ProposerState<string>
        {
            Context = "test-context",
            Signals = new Dictionary<string, object> { ["count"] = "not-an-int" },
            CollectedSignals = new List<ConstrainedSignal>(),
            CompletedProposers = new HashSet<string>(),
            FailedProposers = new HashSet<string>(),
            CorrelationId = "corr-123"
        };

        // Act
        var count = state.GetSignal<int>("count");

        // Assert
        count.Should().Be(0); // default(int)
    }

    [Fact]
    public void ProposerState_HasSignal_ReturnsTrue_WhenExists()
    {
        // Arrange
        var state = new ProposerState<string>
        {
            Context = "test-context",
            Signals = new Dictionary<string, object> { ["exists"] = true },
            CollectedSignals = new List<ConstrainedSignal>(),
            CompletedProposers = new HashSet<string>(),
            FailedProposers = new HashSet<string>(),
            CorrelationId = "corr-123"
        };

        // Assert
        state.HasSignal("exists").Should().BeTrue();
        state.HasSignal("missing").Should().BeFalse();
    }

    [Fact]
    public void ProposerState_GetSignalsFrom_FiltersCorrectly()
    {
        // Arrange
        var signals = new List<ConstrainedSignal>
        {
            ConstrainedSignal.Create("proposer-a", "s.v1", JsonSerializer.SerializeToElement(new { })),
            ConstrainedSignal.Create("proposer-b", "s.v1", JsonSerializer.SerializeToElement(new { })),
            ConstrainedSignal.Create("proposer-a", "s.v1", JsonSerializer.SerializeToElement(new { }))
        };

        var state = new ProposerState<string>
        {
            Context = "test-context",
            Signals = new Dictionary<string, object>(),
            CollectedSignals = signals,
            CompletedProposers = new HashSet<string>(),
            FailedProposers = new HashSet<string>(),
            CorrelationId = "corr-123"
        };

        // Act
        var fromA = state.GetSignalsFrom("proposer-a").ToList();

        // Assert
        fromA.Should().HaveCount(2);
    }

    [Fact]
    public void ProposerState_GetSignalsBySchema_FiltersCorrectly()
    {
        // Arrange
        var signals = new List<ConstrainedSignal>
        {
            ConstrainedSignal.Create("p", "intent.v1", JsonSerializer.SerializeToElement(new { })),
            ConstrainedSignal.Create("p", "sentiment.v1", JsonSerializer.SerializeToElement(new { })),
            ConstrainedSignal.Create("p", "intent.v1", JsonSerializer.SerializeToElement(new { }))
        };

        var state = new ProposerState<string>
        {
            Context = "test-context",
            Signals = new Dictionary<string, object>(),
            CollectedSignals = signals,
            CompletedProposers = new HashSet<string>(),
            FailedProposers = new HashSet<string>(),
            CorrelationId = "corr-123"
        };

        // Act
        var intentSignals = state.GetSignalsBySchema("intent.v1").ToList();

        // Assert
        intentSignals.Should().HaveCount(2);
    }

    private record TestFacts
    {
        public string? Intent { get; init; }
    }
}
