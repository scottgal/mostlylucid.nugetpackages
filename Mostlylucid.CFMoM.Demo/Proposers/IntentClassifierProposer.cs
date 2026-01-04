using Mostlylucid.CFMoM.Demo.Llm;
using Mostlylucid.CFMoM.Demo.Models;
using Mostlylucid.CFMoM.Proposers;
using Mostlylucid.CFMoM.Signals;

namespace Mostlylucid.CFMoM.Demo.Proposers;

/// <summary>
///     Intent classifier using 3B class LLM.
///     Wave 0 proposer for parallel classification.
/// </summary>
public sealed class IntentClassifierProposer : ProposerBase<PromptContext>
{
    private readonly IOllamaClient _ollamaClient;

    public override string Name => "intent-classifier";
    public override string FactsSchemaId => IntentFacts.SchemaId;
    public override int Priority => 10;
    public override TimeSpan ExecutionTimeout => TimeSpan.FromSeconds(10);

    // Model configuration - 3B class for good quality/speed balance
    private const string Model = "llama3.2:3b";

    public IntentClassifierProposer(IOllamaClient ollamaClient)
    {
        _ollamaClient = ollamaClient;
    }

    public override async Task<IReadOnlyList<ConstrainedSignal>> ProposeAsync(
        ProposerState<PromptContext> state,
        CancellationToken cancellationToken = default)
    {
        var prompt = state.Context.Prompt;

        var systemPrompt = """
            You are an intent classifier. Analyze the user's prompt and respond with JSON:
            {
                "intent": "question|command|creative|coding|chat|analysis",
                "confidence": 0.0-1.0,
                "is_follow_up": true|false,
                "key_indicators": ["word1", "word2"]
            }
            """;

        var result = await _ollamaClient.GenerateJsonAsync<IntentResponse>(
            $"Classify the intent: {prompt}",
            Model,
            systemPrompt,
            cancellationToken);

        // Fallback if LLM fails
        if (result == null)
        {
            result = FallbackIntentDetection(prompt);
        }

        var facts = new IntentFacts
        {
            Intent = result.Intent,
            Confidence = result.Confidence,
            IsFollowUp = result.IsFollowUp,
            KeyIndicators = result.KeyIndicators ?? []
        };

        // Map intent to risk score (some intents are higher risk)
        var riskScore = result.Intent switch
        {
            "command" => 0.6f,
            "coding" => 0.3f,
            "question" => 0.2f,
            "creative" => 0.1f,
            "chat" => 0.15f,
            _ => 0.4f
        };

        var signal = CreateSignal(facts, riskScore)
            .WithMetadata("orchestration_signals", new Dictionary<string, object>
            {
                ["intent"] = result.Intent,
                ["intent_done"] = true,
                ["intent_confidence"] = result.Confidence
            });

        return Single(signal);
    }

    private static IntentResponse FallbackIntentDetection(string prompt)
    {
        var lower = prompt.ToLowerInvariant();

        if (lower.Contains("write") || lower.Contains("create") || lower.Contains("poem") || lower.Contains("story"))
            return new IntentResponse { Intent = "creative", Confidence = 0.7, KeyIndicators = ["write", "creative"] };

        if (lower.Contains("code") || lower.Contains("debug") || lower.Contains("function") || lower.Contains("bug"))
            return new IntentResponse { Intent = "coding", Confidence = 0.7, KeyIndicators = ["code", "programming"] };

        if (lower.Contains("?") || lower.Contains("what") || lower.Contains("how") || lower.Contains("why"))
            return new IntentResponse { Intent = "question", Confidence = 0.7, KeyIndicators = ["question"] };

        if (lower.Contains("do") || lower.Contains("run") || lower.Contains("execute") || lower.Contains("delete"))
            return new IntentResponse { Intent = "command", Confidence = 0.6, KeyIndicators = ["command"] };

        return new IntentResponse { Intent = "chat", Confidence = 0.5, KeyIndicators = [] };
    }

    private sealed class IntentResponse
    {
        public string Intent { get; set; } = "chat";
        public double Confidence { get; set; } = 0.5;
        public bool IsFollowUp { get; set; }
        public string[]? KeyIndicators { get; set; }
    }
}
