using System.Collections.Concurrent;
using Mostlylucid.CFMoM.Demo.Llm;
using Mostlylucid.CFMoM.Demo.Models;
using Mostlylucid.CFMoM.Proposers;
using Mostlylucid.CFMoM.Signals;

namespace Mostlylucid.CFMoM.Demo.Proposers;

/// <summary>
///     Sentinel proposer using a tiny LLM (~1B params) for fast triage.
///     This is the first line of defense - quick classification.
///     Uses: tinyllama, phi-2, or similar small models.
/// </summary>
public sealed class SentinelProposer : ProposerBase<PromptContext>
{
    private readonly IOllamaClient _ollamaClient;
    private readonly ConcurrentDictionary<string, SentinelResult> _cache = new();

    public override string Name => "sentinel-triage";
    public override string FactsSchemaId => "sentinel.v1";
    public override int Priority => 5; // Very high priority (runs early)
    public override TimeSpan ExecutionTimeout => TimeSpan.FromSeconds(3); // Fast timeout for tiny model

    // Model configuration - use tiny LLM for fast triage
    private const string Model = "tinyllama"; // ~1.1B params, very fast

    public SentinelProposer(IOllamaClient ollamaClient)
    {
        _ollamaClient = ollamaClient;
    }

    public override async Task<IReadOnlyList<ConstrainedSignal>> ProposeAsync(
        ProposerState<PromptContext> state,
        CancellationToken cancellationToken = default)
    {
        var prompt = state.Context.Prompt;

        // Check cache first (layer caching at sentinel level)
        var cacheKey = GetCacheKey(prompt);
        if (_cache.TryGetValue(cacheKey, out var cachedResult))
        {
            return Single(CreateSignalFromResult(cachedResult, fromCache: true));
        }

        // Quick heuristic check before LLM call
        var quickResult = QuickHeuristicCheck(prompt);
        if (quickResult != null)
        {
            _cache[cacheKey] = quickResult;
            return Single(CreateSignalFromResult(quickResult, fromCache: false));
        }

        // Call tiny LLM for fast triage
        var systemPrompt = """
            You are a fast triage sentinel. Analyze the prompt and respond with JSON:
            {"category": "safe|suspicious|dangerous", "urgency": "low|medium|high", "needs_deep_analysis": true|false}
            Be quick and decisive. Err on the side of caution.
            """;

        var result = await _ollamaClient.GenerateJsonAsync<SentinelResult>(
            $"Triage this prompt: {prompt}",
            Model,
            systemPrompt,
            cancellationToken);

        result ??= new SentinelResult { Category = "suspicious", Urgency = "medium", NeedsDeepAnalysis = true };

        // Cache the result
        _cache[cacheKey] = result;

        return Single(CreateSignalFromResult(result, fromCache: false));
    }

    private ConstrainedSignal CreateSignalFromResult(SentinelResult result, bool fromCache)
    {
        // Map category to confidence (higher confidence = higher risk)
        var confidence = result.Category switch
        {
            "dangerous" => 0.95f,
            "suspicious" => 0.65f,
            _ => 0.15f
        };

        var facts = new
        {
            result.Category,
            result.Urgency,
            result.NeedsDeepAnalysis,
            FromCache = fromCache
        };

        var signal = CreateSignal(facts, confidence)
            .WithMetadata("orchestration_signals", new Dictionary<string, object>
            {
                ["sentinel_category"] = result.Category,
                ["sentinel_done"] = true,
                ["needs_deep_analysis"] = result.NeedsDeepAnalysis,
                ["sentinel_urgency"] = result.Urgency
            });

        // Early exit for clearly dangerous content
        if (result.Category == "dangerous")
        {
            signal = signal.WithEarlyExit("blacklisted");
        }

        return signal;
    }

    private static SentinelResult? QuickHeuristicCheck(string prompt)
    {
        var lower = prompt.ToLowerInvariant();

        // Immediate block patterns (no LLM needed)
        var dangerousPatterns = new[] { "hack", "exploit", "malware", "virus", "attack", "bomb", "weapon" };
        if (dangerousPatterns.Any(p => lower.Contains(p)))
        {
            return new SentinelResult { Category = "dangerous", Urgency = "high", NeedsDeepAnalysis = false };
        }

        // Immediate safe patterns
        var safePatterns = new[] { "poem", "story", "help me", "explain", "what is", "how to" };
        if (safePatterns.Any(p => lower.Contains(p)) && !dangerousPatterns.Any(p => lower.Contains(p)))
        {
            return new SentinelResult { Category = "safe", Urgency = "low", NeedsDeepAnalysis = false };
        }

        return null; // Need LLM to decide
    }

    private static string GetCacheKey(string prompt)
    {
        // Simple cache key - first 100 chars normalized
        var normalized = prompt.ToLowerInvariant().Trim();
        return normalized.Length <= 100 ? normalized : normalized[..100];
    }

    private sealed class SentinelResult
    {
        public string Category { get; set; } = "suspicious";
        public string Urgency { get; set; } = "medium";
        public bool NeedsDeepAnalysis { get; set; } = true;
    }
}
