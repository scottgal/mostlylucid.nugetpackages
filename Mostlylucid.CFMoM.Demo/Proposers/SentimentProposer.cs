using Mostlylucid.CFMoM.Demo.Llm;
using Mostlylucid.CFMoM.Demo.Models;
using Mostlylucid.CFMoM.Proposers;
using Mostlylucid.CFMoM.Signals;

namespace Mostlylucid.CFMoM.Demo.Proposers;

/// <summary>
///     Sentiment analyzer using 3B class LLM.
///     Wave 0 proposer for parallel classification.
/// </summary>
public sealed class SentimentProposer : ProposerBase<PromptContext>
{
    private readonly IOllamaClient _ollamaClient;

    public override string Name => "sentiment-analyzer";
    public override string FactsSchemaId => SentimentFacts.SchemaId;
    public override int Priority => 10;
    public override TimeSpan ExecutionTimeout => TimeSpan.FromSeconds(10);

    // Model configuration - 3B class for good quality/speed balance
    private const string Model = "llama3.2:3b";

    public SentimentProposer(IOllamaClient ollamaClient)
    {
        _ollamaClient = ollamaClient;
    }

    public override async Task<IReadOnlyList<ConstrainedSignal>> ProposeAsync(
        ProposerState<PromptContext> state,
        CancellationToken cancellationToken = default)
    {
        var prompt = state.Context.Prompt;

        var systemPrompt = """
            You are a sentiment analyzer. Analyze the emotional tone of the prompt and respond with JSON:
            {
                "sentiment": "positive|negative|neutral",
                "intensity": 0.0-1.0,
                "emotions": ["emotion1", "emotion2"],
                "indicator_words": ["word1", "word2"]
            }
            """;

        var result = await _ollamaClient.GenerateJsonAsync<SentimentResponse>(
            $"Analyze sentiment: {prompt}",
            Model,
            systemPrompt,
            cancellationToken);

        // Fallback if LLM fails
        if (result == null)
        {
            result = FallbackSentimentDetection(prompt);
        }

        var facts = new SentimentFacts
        {
            Sentiment = result.Sentiment,
            Intensity = result.Intensity,
            Emotions = result.Emotions ?? [],
            IndicatorWords = result.IndicatorWords ?? []
        };

        // Map sentiment to risk score (negative sentiment is higher risk)
        var riskScore = result.Sentiment switch
        {
            "negative" => 0.5f + (float)(result.Intensity * 0.4),
            "neutral" => 0.3f,
            "positive" => 0.1f + (float)(result.Intensity * 0.1),
            _ => 0.4f
        };

        var signal = CreateSignal(facts, riskScore)
            .WithMetadata("orchestration_signals", new Dictionary<string, object>
            {
                ["sentiment"] = result.Sentiment,
                ["sentiment_done"] = true,
                ["sentiment_intensity"] = result.Intensity
            });

        return Single(signal);
    }

    private static SentimentResponse FallbackSentimentDetection(string prompt)
    {
        var lower = prompt.ToLowerInvariant();

        var positiveWords = new[] { "please", "thanks", "good", "great", "love", "happy", "appreciate" };
        var negativeWords = new[] { "hate", "bad", "angry", "kill", "destroy", "worst", "terrible" };

        var positiveCount = positiveWords.Count(w => lower.Contains(w));
        var negativeCount = negativeWords.Count(w => lower.Contains(w));

        if (positiveCount > negativeCount)
        {
            return new SentimentResponse
            {
                Sentiment = "positive",
                Intensity = Math.Min(1.0, 0.5 + positiveCount * 0.15),
                Emotions = ["happy"],
                IndicatorWords = positiveWords.Where(w => lower.Contains(w)).ToArray()
            };
        }

        if (negativeCount > positiveCount)
        {
            return new SentimentResponse
            {
                Sentiment = "negative",
                Intensity = Math.Min(1.0, 0.5 + negativeCount * 0.15),
                Emotions = ["frustrated"],
                IndicatorWords = negativeWords.Where(w => lower.Contains(w)).ToArray()
            };
        }

        return new SentimentResponse
        {
            Sentiment = "neutral",
            Intensity = 0.3,
            Emotions = [],
            IndicatorWords = []
        };
    }

    private sealed class SentimentResponse
    {
        public string Sentiment { get; set; } = "neutral";
        public double Intensity { get; set; } = 0.3;
        public string[]? Emotions { get; set; }
        public string[]? IndicatorWords { get; set; }
    }
}
