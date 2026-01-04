using System.Text.Json;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using Mostlylucid.CFMoM.Aggregation;
using Mostlylucid.CFMoM.ConsensusSpace;
using Mostlylucid.CFMoM.Constrainers;
using Mostlylucid.CFMoM.Extensions;
using Mostlylucid.CFMoM.Orchestration;
using Mostlylucid.CFMoM.Proposers;
using Mostlylucid.CFMoM.Signals;
using Xunit;

namespace Mostlylucid.CFMoM.Tests.Integration;

public class EndToEndTests
{
    public record PromptContext(string Prompt);

    public record IntentFacts(string Intent, double Confidence);
    public record SentimentFacts(string Sentiment, double Intensity);
    public record TopicFacts(string[] Topics, string Primary);

    public class IntentProposer : ProposerBase<PromptContext>
    {
        public override string Name => "intent-classifier";
        public override string FactsSchemaId => "intent.v1";
        public override int Priority => 10;

        public override Task<IReadOnlyList<ConstrainedSignal>> ProposeAsync(
            ProposerState<PromptContext> state,
            CancellationToken cancellationToken = default)
        {
            var prompt = state.Context.Prompt.ToLowerInvariant();

            string intent;
            float confidence;

            if (prompt.Contains("write") || prompt.Contains("create") || prompt.Contains("poem"))
            {
                intent = "creative";
                confidence = 0.9f;
            }
            else if (prompt.Contains("what") || prompt.Contains("how") || prompt.Contains("?"))
            {
                intent = "question";
                confidence = 0.85f;
            }
            else
            {
                intent = "chat";
                confidence = 0.6f;
            }

            var facts = new IntentFacts(intent, confidence);
            var signal = CreateSignal(facts, confidence)
                .WithMetadata("orchestration_signals", new Dictionary<string, object>
                {
                    ["intent"] = intent,
                    ["intent_done"] = true
                });

            return Task.FromResult<IReadOnlyList<ConstrainedSignal>>(Single(signal));
        }
    }

    public class SentimentProposer : ProposerBase<PromptContext>
    {
        public override string Name => "sentiment-analyzer";
        public override string FactsSchemaId => "sentiment.v1";
        public override int Priority => 10;

        public override Task<IReadOnlyList<ConstrainedSignal>> ProposeAsync(
            ProposerState<PromptContext> state,
            CancellationToken cancellationToken = default)
        {
            var prompt = state.Context.Prompt.ToLowerInvariant();

            var positiveWords = new[] { "please", "thanks", "good", "great", "love" };
            var negativeWords = new[] { "hate", "bad", "angry", "kill", "destroy" };

            var positiveCount = positiveWords.Count(w => prompt.Contains(w));
            var negativeCount = negativeWords.Count(w => prompt.Contains(w));

            string sentiment;
            double intensity;

            if (positiveCount > negativeCount)
            {
                sentiment = "positive";
                intensity = Math.Min(1.0, 0.5 + positiveCount * 0.15);
            }
            else if (negativeCount > positiveCount)
            {
                sentiment = "negative";
                intensity = Math.Min(1.0, 0.5 + negativeCount * 0.15);
            }
            else
            {
                sentiment = "neutral";
                intensity = 0.3;
            }

            var facts = new SentimentFacts(sentiment, intensity);
            // Convert sentiment to confidence: positive = low risk (0.3), negative = high risk (0.8)
            var confidence = sentiment == "negative" ? 0.8f : 0.3f;
            var signal = CreateSignal(facts, confidence)
                .WithMetadata("orchestration_signals", new Dictionary<string, object>
                {
                    ["sentiment"] = sentiment,
                    ["sentiment_done"] = true
                });

            return Task.FromResult<IReadOnlyList<ConstrainedSignal>>(Single(signal));
        }
    }

    public class TopicProposer : ProposerBase<PromptContext>
    {
        public override string Name => "topic-classifier";
        public override string FactsSchemaId => "topic.v1";
        public override int Priority => 50;

        public override IReadOnlyList<TriggerCondition> TriggerConditions =>
            new[] { Triggers.WhenSignalExists("intent_done") };

        public override Task<IReadOnlyList<ConstrainedSignal>> ProposeAsync(
            ProposerState<PromptContext> state,
            CancellationToken cancellationToken = default)
        {
            var prompt = state.Context.Prompt.ToLowerInvariant();
            var topics = new List<string>();

            if (prompt.Contains("poem") || prompt.Contains("story") || prompt.Contains("write"))
                topics.Add("creative-writing");
            if (prompt.Contains("code") || prompt.Contains("programming") || prompt.Contains("bug"))
                topics.Add("technology");
            if (prompt.Contains("ocean") || prompt.Contains("nature") || prompt.Contains("animal"))
                topics.Add("nature");

            if (topics.Count == 0)
                topics.Add("general");

            var facts = new TopicFacts(topics.ToArray(), topics.First());
            var signal = CreateSignal(facts, 0.4f); // Topics don't affect risk score much

            return Task.FromResult<IReadOnlyList<ConstrainedSignal>>(Single(signal));
        }
    }

    [Fact]
    public async Task FullPipeline_CreativePrompt_ReturnsAllow()
    {
        // Arrange
        var services = new ServiceCollection();
        services.AddCFMoMWithThresholds<PromptContext>();
        services.AddCFMoMProposer<PromptContext, IntentProposer>();
        services.AddCFMoMProposer<PromptContext, SentimentProposer>();
        services.AddCFMoMProposer<PromptContext, TopicProposer>();

        var provider = services.BuildServiceProvider();
        var orchestrator = provider.GetRequiredService<CFMoMOrchestrator<PromptContext, CommonDecision>>();

        var context = new PromptContext("Write me a poem about the ocean please");

        // Act
        var result = await orchestrator.ExecuteAsync(context);

        // Assert
        result.Decision.Should().Be(CommonDecision.Allow);
        result.Signals.Should().HaveCount(3); // Intent, Sentiment, Topic
        result.CompletedProposers.Should().Contain("intent-classifier");
        result.CompletedProposers.Should().Contain("sentiment-analyzer");
        result.CompletedProposers.Should().Contain("topic-classifier");
        result.WaveCount.Should().BeGreaterOrEqualTo(1);
    }

    [Fact]
    public async Task FullPipeline_Wave1ProposerTriggered_ByWave0Signal()
    {
        // Arrange
        var services = new ServiceCollection();
        services.AddCFMoMWithThresholds<PromptContext>();
        services.AddCFMoMProposer<PromptContext, IntentProposer>();
        services.AddCFMoMProposer<PromptContext, TopicProposer>();

        var provider = services.BuildServiceProvider();
        var orchestrator = provider.GetRequiredService<CFMoMOrchestrator<PromptContext, CommonDecision>>();

        var context = new PromptContext("What is the meaning of life?");

        // Act
        var result = await orchestrator.ExecuteAsync(context);

        // Assert
        result.CompletedProposers.Should().Contain("intent-classifier");
        result.CompletedProposers.Should().Contain("topic-classifier");
        // Topic should run after Intent (triggered by intent_done signal)
        result.WaveCount.Should().BeGreaterOrEqualTo(1);
    }

    [Fact]
    public async Task FullPipeline_AggregationCorrectlyComputed()
    {
        // Arrange
        var services = new ServiceCollection();
        services.AddCFMoMWithThresholds<PromptContext>();
        services.AddCFMoMProposer<PromptContext, IntentProposer>();
        services.AddCFMoMProposer<PromptContext, SentimentProposer>();

        var provider = services.BuildServiceProvider();
        var orchestrator = provider.GetRequiredService<CFMoMOrchestrator<PromptContext, CommonDecision>>();

        var context = new PromptContext("Thanks for the help!");

        // Act
        var result = await orchestrator.ExecuteAsync(context);

        // Assert
        result.Aggregation.Should().NotBeNull();
        result.Aggregation.Signals.Should().HaveCount(2);
        result.Aggregation.ContributingProposers.Should().HaveCount(2);
        result.Aggregation.Score.Should().BeInRange(0, 1);
        result.Aggregation.Confidence.Should().BeInRange(0, 1);
    }

    [Fact]
    public async Task DIContainer_ResolvesAllComponents()
    {
        // Arrange
        var services = new ServiceCollection();
        services.AddCFMoMWithThresholds<PromptContext>();
        services.AddCFMoMProposer<PromptContext, IntentProposer>();

        var provider = services.BuildServiceProvider();

        // Assert - All components resolve correctly
        provider.GetRequiredService<IConsensusSpace>().Should().NotBeNull();
        provider.GetRequiredService<IAggregator>().Should().NotBeNull();
        provider.GetRequiredService<IConstrainer<PromptContext, CommonDecision>>().Should().NotBeNull();
        provider.GetRequiredService<CFMoMOrchestrator<PromptContext, CommonDecision>>().Should().NotBeNull();
    }

    [Fact]
    public async Task MultipleRuns_AreIsolated()
    {
        // Arrange
        var services = new ServiceCollection();
        services.AddCFMoMWithThresholds<PromptContext>();
        services.AddCFMoMProposer<PromptContext, IntentProposer>();

        var provider = services.BuildServiceProvider();
        var orchestrator = provider.GetRequiredService<CFMoMOrchestrator<PromptContext, CommonDecision>>();

        // Act
        var result1 = await orchestrator.ExecuteAsync(new PromptContext("First prompt"));
        var result2 = await orchestrator.ExecuteAsync(new PromptContext("Second prompt"));

        // Assert
        result1.CorrelationId.Should().NotBe(result2.CorrelationId);
        result1.Signals.Should().HaveCount(1);
        result2.Signals.Should().HaveCount(1);
    }

    [Fact]
    public async Task SchemaBreakdown_ProvidedInResult()
    {
        // Arrange
        var services = new ServiceCollection();
        services.AddCFMoMWithThresholds<PromptContext>();
        services.AddCFMoMProposer<PromptContext, IntentProposer>();
        services.AddCFMoMProposer<PromptContext, SentimentProposer>();

        var provider = services.BuildServiceProvider();
        var orchestrator = provider.GetRequiredService<CFMoMOrchestrator<PromptContext, CommonDecision>>();

        // Act
        var result = await orchestrator.ExecuteAsync(new PromptContext("Tell me a joke"));

        // Assert
        result.Aggregation.SchemaBreakdown.Should().ContainKey("intent.v1");
        result.Aggregation.SchemaBreakdown.Should().ContainKey("sentiment.v1");
    }
}
