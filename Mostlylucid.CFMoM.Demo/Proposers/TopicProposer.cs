using Mostlylucid.CFMoM.Demo.Llm;
using Mostlylucid.CFMoM.Demo.Models;
using Mostlylucid.CFMoM.Proposers;
using Mostlylucid.CFMoM.Signals;

namespace Mostlylucid.CFMoM.Demo.Proposers;

/// <summary>
///     Topic classifier using 3B class LLM.
///     Wave 1 proposer - triggered after intent classification.
/// </summary>
public sealed class TopicProposer : ProposerBase<PromptContext>
{
    private readonly IOllamaClient _ollamaClient;

    public override string Name => "topic-classifier";
    public override string FactsSchemaId => TopicFacts.SchemaId;
    public override int Priority => 50;
    public override TimeSpan ExecutionTimeout => TimeSpan.FromSeconds(15);

    // Trigger: Only run after intent classification is done
    public override IReadOnlyList<TriggerCondition> TriggerConditions =>
        [Triggers.WhenSignalExists("intent_done")];

    private const string Model = "llama3.2:3b";

    public TopicProposer(IOllamaClient ollamaClient)
    {
        _ollamaClient = ollamaClient;
    }

    public override async Task<IReadOnlyList<ConstrainedSignal>> ProposeAsync(
        ProposerState<PromptContext> state,
        CancellationToken cancellationToken = default)
    {
        var prompt = state.Context.Prompt;

        // Get intent from previous wave for context
        var intent = state.GetSignal<string>("intent") ?? "unknown";

        var systemPrompt = $$"""
            You are a topic classifier. The user's intent is: {{intent}}
            Analyze the prompt and respond with JSON:
            {
                "topics": ["topic1", "topic2"],
                "primary": "main_topic",
                "primary_relevance": 0.0-1.0,
                "requires_expertise": true|false
            }
            Topics can include: technology, creative-writing, science, business, nature, health, education, entertainment, etc.
            """;

        var result = await _ollamaClient.GenerateJsonAsync<TopicResponse>(
            $"Classify topics: {prompt}",
            Model,
            systemPrompt,
            cancellationToken);

        // Fallback
        if (result == null)
        {
            result = FallbackTopicDetection(prompt);
        }

        var facts = new TopicFacts
        {
            Topics = result.Topics ?? ["general"],
            Primary = result.Primary ?? "general",
            PrimaryRelevance = result.PrimaryRelevance,
            RequiresExpertise = result.RequiresExpertise
        };

        // Topics don't heavily influence risk score
        var riskScore = result.RequiresExpertise ? 0.4f : 0.2f;

        var signal = CreateSignal(facts, riskScore)
            .WithMetadata("orchestration_signals", new Dictionary<string, object>
            {
                ["topic"] = result.Primary ?? "general",
                ["topic_done"] = true
            });

        return Single(signal);
    }

    private static TopicResponse FallbackTopicDetection(string prompt)
    {
        var lower = prompt.ToLowerInvariant();

        var topicKeywords = new Dictionary<string, string[]>
        {
            ["technology"] = ["code", "programming", "computer", "software", "api"],
            ["creative-writing"] = ["poem", "story", "write", "fiction"],
            ["nature"] = ["ocean", "sea", "forest", "animal", "nature"],
            ["science"] = ["research", "experiment", "physics", "chemistry"],
            ["business"] = ["company", "market", "sales", "revenue"]
        };

        var detectedTopics = topicKeywords
            .Where(kv => kv.Value.Any(keyword => lower.Contains(keyword)))
            .Select(kv => kv.Key)
            .ToList();

        if (detectedTopics.Count == 0)
            detectedTopics.Add("general");

        return new TopicResponse
        {
            Topics = detectedTopics.ToArray(),
            Primary = detectedTopics[0],
            PrimaryRelevance = 0.7,
            RequiresExpertise = detectedTopics.Contains("technology") || detectedTopics.Contains("science")
        };
    }

    private sealed class TopicResponse
    {
        public string[]? Topics { get; set; }
        public string? Primary { get; set; }
        public double PrimaryRelevance { get; set; } = 0.5;
        public bool RequiresExpertise { get; set; }
    }
}
