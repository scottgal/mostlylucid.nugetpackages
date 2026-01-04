using System.Text.Json;
using FluentAssertions;
using Mostlylucid.CFMoM.Aggregation;
using Mostlylucid.CFMoM.Constrainers;
using Mostlylucid.CFMoM.Orchestration;
using Mostlylucid.CFMoM.Proposers;
using Mostlylucid.CFMoM.Signals;
using NSubstitute;
using Xunit;

namespace Mostlylucid.CFMoM.Tests.Orchestration;

public class CFMoMOrchestratorTests
{
    private class TestProposer : ProposerBase<string>
    {
        private readonly Func<ProposerState<string>, IReadOnlyList<ConstrainedSignal>> _proposeFn;

        public TestProposer(
            string name,
            Func<ProposerState<string>, IReadOnlyList<ConstrainedSignal>>? proposeFn = null,
            IReadOnlyList<TriggerCondition>? triggers = null,
            int priority = 100,
            bool isEnabled = true)
        {
            Name = name;
            FactsSchemaId = $"{name}.v1";
            _proposeFn = proposeFn ?? (_ => None());
            TriggerConditions = triggers ?? Triggers.None;
            Priority = priority;
            IsEnabled = isEnabled;
        }

        public override string Name { get; }
        public override string FactsSchemaId { get; }
        public override int Priority { get; }
        public override IReadOnlyList<TriggerCondition> TriggerConditions { get; }
        public override bool IsEnabled { get; }

        public override Task<IReadOnlyList<ConstrainedSignal>> ProposeAsync(
            ProposerState<string> state,
            CancellationToken cancellationToken = default)
        {
            var signals = _proposeFn(state);
            return Task.FromResult(signals);
        }
    }

    private static ConstrainedSignal CreateSignal(string sourceId, float confidence)
    {
        return ConstrainedSignal.Create(
            sourceId: sourceId,
            factsSchemaId: $"{sourceId}.v1",
            facts: JsonSerializer.SerializeToElement(new { test = true }),
            confidence: confidence);
    }

    [Fact]
    public async Task ExecuteAsync_SingleProposer_ReturnsResult()
    {
        // Arrange
        var proposer = new TestProposer("test", _ => new[] { CreateSignal("test", 0.7f) });
        var consensusSpace = new CFMoM.ConsensusSpace.ConsensusSpace();
        var aggregator = new WeightedAggregator();
        var constrainer = new ThresholdConstrainer<string>();
        var orchestrator = new CFMoMOrchestrator<string, CommonDecision>(
            new[] { proposer },
            consensusSpace,
            aggregator,
            constrainer);

        // Act
        var result = await orchestrator.ExecuteAsync("test-context");

        // Assert
        result.Should().NotBeNull();
        result.CorrelationId.Should().NotBeNullOrEmpty();
        result.Signals.Should().HaveCount(1);
        result.CompletedProposers.Should().Contain("test");
        result.FailedProposers.Should().BeEmpty();
    }

    [Fact]
    public async Task ExecuteAsync_MultipleProposers_RunsInWaves()
    {
        // Arrange
        var wave0Proposer1 = new TestProposer("wave0-a", _ => new[] { CreateSignal("wave0-a", 0.6f) });
        var wave0Proposer2 = new TestProposer("wave0-b", _ => new[] { CreateSignal("wave0-b", 0.5f) });

        var consensusSpace = new CFMoM.ConsensusSpace.ConsensusSpace();
        var aggregator = new WeightedAggregator();
        var constrainer = new ThresholdConstrainer<string>();
        var orchestrator = new CFMoMOrchestrator<string, CommonDecision>(
            new[] { wave0Proposer1, wave0Proposer2 },
            consensusSpace,
            aggregator,
            constrainer);

        // Act
        var result = await orchestrator.ExecuteAsync("test-context");

        // Assert
        result.Signals.Should().HaveCount(2);
        result.CompletedProposers.Should().HaveCount(2);
    }

    [Fact]
    public async Task ExecuteAsync_TriggeredProposer_RunsInLaterWave()
    {
        // Arrange
        var executionOrder = new List<string>();

        var wave0Proposer = new TestProposer("wave0", state =>
        {
            executionOrder.Add("wave0");
            return new[] { CreateSignal("wave0", 0.5f).WithMetadata("orchestration_signals",
                new Dictionary<string, object> { ["wave0_done"] = true }) };
        });

        var wave1Proposer = new TestProposer("wave1", state =>
        {
            executionOrder.Add("wave1");
            return new[] { CreateSignal("wave1", 0.6f) };
        }, triggers: new[] { Triggers.WhenSignalExists("wave0_done") });

        var consensusSpace = new CFMoM.ConsensusSpace.ConsensusSpace();
        var aggregator = new WeightedAggregator();
        var constrainer = new ThresholdConstrainer<string>();
        var orchestrator = new CFMoMOrchestrator<string, CommonDecision>(
            new[] { wave0Proposer, wave1Proposer },
            consensusSpace,
            aggregator,
            constrainer);

        // Act
        var result = await orchestrator.ExecuteAsync("test-context");

        // Assert
        executionOrder.Should().ContainInOrder("wave0", "wave1");
        result.Signals.Should().HaveCount(2);
        result.WaveCount.Should().BeGreaterOrEqualTo(1);
    }

    [Fact]
    public async Task ExecuteAsync_EarlyExitSignal_StopsOrchestration()
    {
        // Arrange
        var proposer1 = new TestProposer("early-exit", _ =>
            new[] { CreateSignal("early-exit", 0.9f).WithEarlyExit("verified-good") });

        var proposer2 = new TestProposer("never-runs", _ =>
            new[] { CreateSignal("never-runs", 0.5f) },
            triggers: new[] { Triggers.WhenProposerCount(10) }); // Should never run

        var consensusSpace = new CFMoM.ConsensusSpace.ConsensusSpace();
        var aggregator = new WeightedAggregator();
        var constrainer = new ThresholdConstrainer<string>();
        var orchestrator = new CFMoMOrchestrator<string, CommonDecision>(
            new[] { proposer1, proposer2 },
            consensusSpace,
            aggregator,
            constrainer);

        // Act
        var result = await orchestrator.ExecuteAsync("test-context");

        // Assert
        result.Aggregation.EarlyExit.Should().BeTrue();
        result.Decision.Should().Be(CommonDecision.Allow);
    }

    [Fact]
    public async Task ExecuteAsync_ConstrainerStops_ReturnsDecision()
    {
        // Arrange - High confidence signal triggers challenge decision
        // Note: A single 0.95 signal with sigmoid aggregation gives score ~0.71 (Challenge threshold)
        // For Block (>= 0.85), you'd need multiple high signals or an early exit
        var proposer = new TestProposer("blocker", _ =>
            new[] { CreateSignal("blocker", 0.95f) });

        var consensusSpace = new CFMoM.ConsensusSpace.ConsensusSpace();
        var aggregator = new WeightedAggregator();
        var constrainer = new ThresholdConstrainer<string>();
        var orchestrator = new CFMoMOrchestrator<string, CommonDecision>(
            new[] { proposer },
            consensusSpace,
            aggregator,
            constrainer);

        // Act
        var result = await orchestrator.ExecuteAsync("test-context");

        // Assert - Score ~0.71 triggers Challenge
        result.Decision.Should().Be(CommonDecision.Challenge);
    }

    [Fact]
    public async Task ExecuteAsync_DisabledProposer_IsSkipped()
    {
        // Arrange
        var enabledProposer = new TestProposer("enabled", _ => new[] { CreateSignal("enabled", 0.5f) });
        var disabledProposer = new TestProposer("disabled", _ => new[] { CreateSignal("disabled", 0.5f) },
            isEnabled: false);

        var consensusSpace = new CFMoM.ConsensusSpace.ConsensusSpace();
        var aggregator = new WeightedAggregator();
        var constrainer = new ThresholdConstrainer<string>();
        var orchestrator = new CFMoMOrchestrator<string, CommonDecision>(
            new[] { enabledProposer, disabledProposer },
            consensusSpace,
            aggregator,
            constrainer);

        // Act
        var result = await orchestrator.ExecuteAsync("test-context");

        // Assert
        result.CompletedProposers.Should().Contain("enabled");
        result.CompletedProposers.Should().NotContain("disabled");
    }

    [Fact]
    public async Task ExecuteAsync_ProposersByPriority_RunsInOrder()
    {
        // Arrange
        var executionOrder = new List<string>();

        var lowPriority = new TestProposer("low", _ =>
        {
            executionOrder.Add("low");
            return new[] { CreateSignal("low", 0.5f) };
        }, priority: 200);

        var highPriority = new TestProposer("high", _ =>
        {
            executionOrder.Add("high");
            return new[] { CreateSignal("high", 0.5f) };
        }, priority: 10);

        var mediumPriority = new TestProposer("medium", _ =>
        {
            executionOrder.Add("medium");
            return new[] { CreateSignal("medium", 0.5f) };
        }, priority: 100);

        var consensusSpace = new CFMoM.ConsensusSpace.ConsensusSpace();
        var aggregator = new WeightedAggregator();
        var constrainer = new ThresholdConstrainer<string>();
        var options = new CFMoMOptions { MaxParallelProposers = 1 }; // Sequential to test order
        var orchestrator = new CFMoMOrchestrator<string, CommonDecision>(
            new[] { lowPriority, mediumPriority, highPriority },
            consensusSpace,
            aggregator,
            constrainer,
            options);

        // Act
        await orchestrator.ExecuteAsync("test-context");

        // Assert - Higher priority (lower number) should run first
        executionOrder.Should().ContainInOrder("high", "medium", "low");
    }

    [Fact]
    public async Task ExecuteAsync_MaxWavesReached_Stops()
    {
        // Arrange - Create proposers that keep triggering each other
        var proposer = new TestProposer("always-runs", _ =>
            new[] { CreateSignal("always-runs", 0.5f) });

        var consensusSpace = new CFMoM.ConsensusSpace.ConsensusSpace();
        var aggregator = new WeightedAggregator();
        var constrainer = new ThresholdConstrainer<string>();
        var options = new CFMoMOptions { MaxWaves = 2 };
        var orchestrator = new CFMoMOrchestrator<string, CommonDecision>(
            new[] { proposer },
            consensusSpace,
            aggregator,
            constrainer,
            options);

        // Act
        var result = await orchestrator.ExecuteAsync("test-context");

        // Assert
        result.WaveCount.Should().BeLessThanOrEqualTo(2);
    }

    [Fact]
    public async Task ExecuteAsync_ProposerThrows_MarksFailed()
    {
        // Arrange
        var failingProposer = new TestProposer("failing", _ =>
            throw new InvalidOperationException("Simulated failure"));
        var successProposer = new TestProposer("success", _ => new[] { CreateSignal("success", 0.5f) });

        var consensusSpace = new CFMoM.ConsensusSpace.ConsensusSpace();
        var aggregator = new WeightedAggregator();
        var constrainer = new ThresholdConstrainer<string>();
        var orchestrator = new CFMoMOrchestrator<string, CommonDecision>(
            new[] { failingProposer, successProposer },
            consensusSpace,
            aggregator,
            constrainer);

        // Act
        var result = await orchestrator.ExecuteAsync("test-context");

        // Assert
        result.FailedProposers.Should().Contain("failing");
        result.CompletedProposers.Should().Contain("success");
    }

    [Fact]
    public async Task ExecuteAsync_CorrelationId_IsPropagated()
    {
        // Arrange
        string? capturedCorrelationId = null;
        var proposer = new TestProposer("capture", state =>
        {
            capturedCorrelationId = state.CorrelationId;
            return Array.Empty<ConstrainedSignal>();
        });

        var consensusSpace = new CFMoM.ConsensusSpace.ConsensusSpace();
        var aggregator = new WeightedAggregator();
        var constrainer = new ThresholdConstrainer<string>();
        var orchestrator = new CFMoMOrchestrator<string, CommonDecision>(
            new[] { proposer },
            consensusSpace,
            aggregator,
            constrainer);

        // Act
        var result = await orchestrator.ExecuteAsync("test-context");

        // Assert
        capturedCorrelationId.Should().Be(result.CorrelationId);
    }

    [Fact]
    public async Task ExecuteAsync_ConsensusSpaceCleared_BetweenRuns()
    {
        // Arrange
        var proposer = new TestProposer("test", _ => new[] { CreateSignal("test", 0.5f) });
        var consensusSpace = new CFMoM.ConsensusSpace.ConsensusSpace();
        var aggregator = new WeightedAggregator();
        var constrainer = new ThresholdConstrainer<string>();
        var orchestrator = new CFMoMOrchestrator<string, CommonDecision>(
            new[] { proposer },
            consensusSpace,
            aggregator,
            constrainer);

        // Act - Run twice
        var result1 = await orchestrator.ExecuteAsync("context1");
        var result2 = await orchestrator.ExecuteAsync("context2");

        // Assert - Each run should have only 1 signal (space cleared between runs)
        result1.Signals.Should().HaveCount(1);
        result2.Signals.Should().HaveCount(1);
    }

    [Fact]
    public async Task ExecuteAsync_TotalDuration_IsTracked()
    {
        // Arrange
        var proposer = new AsyncTestProposer("delayed", async state =>
        {
            await Task.Delay(50);
            return new[] { CreateSignal("delayed", 0.5f) };
        });

        var consensusSpace = new CFMoM.ConsensusSpace.ConsensusSpace();
        var aggregator = new WeightedAggregator();
        var constrainer = new ThresholdConstrainer<string>();
        var orchestrator = new CFMoMOrchestrator<string, CommonDecision>(
            new[] { proposer },
            consensusSpace,
            aggregator,
            constrainer);

        // Act
        var result = await orchestrator.ExecuteAsync("test-context");

        // Assert
        result.TotalDurationMs.Should().BeGreaterThan(40);
    }

    private class AsyncTestProposer : ProposerBase<string>
    {
        private readonly Func<ProposerState<string>, Task<IReadOnlyList<ConstrainedSignal>>> _proposeFn;

        public AsyncTestProposer(string name, Func<ProposerState<string>, Task<IReadOnlyList<ConstrainedSignal>>> proposeFn)
        {
            Name = name;
            FactsSchemaId = $"{name}.v1";
            _proposeFn = proposeFn;
        }

        public override string Name { get; }
        public override string FactsSchemaId { get; }

        public override Task<IReadOnlyList<ConstrainedSignal>> ProposeAsync(
            ProposerState<string> state,
            CancellationToken cancellationToken = default)
        {
            return _proposeFn(state);
        }
    }
}
