using System.Threading.Channels;
using Mostlylucid.CFMoM.Demo.Llm;
using Mostlylucid.CFMoM.Demo.Models;
using Mostlylucid.CFMoM.Proposers;
using Mostlylucid.CFMoM.Signals;

namespace Mostlylucid.CFMoM.Demo.Proposers;

/// <summary>
///     Safety evaluator using 7B class LLM (expensive).
///     Wave 1 proposer - triggered when sentinel flags as suspicious OR sentiment is negative.
///     Uses bounded async coordination for expensive model calls.
/// </summary>
public sealed class SafetyEvaluatorProposer : ProposerBase<PromptContext>
{
    private readonly IOllamaClient _ollamaClient;
    private readonly BoundedAsyncCoordinator _coordinator;

    public override string Name => "safety-evaluator";
    public override string FactsSchemaId => SafetyFacts.SchemaId;
    public override int Priority => 50;
    public override TimeSpan ExecutionTimeout => TimeSpan.FromSeconds(30); // Longer timeout for 7B model

    // Trigger: Run when sentinel flags suspicious OR sentiment is negative
    public override IReadOnlyList<TriggerCondition> TriggerConditions =>
    [
        Triggers.AnyOf(
            Triggers.WhenSignalEquals("sentinel_category", "suspicious"),
            Triggers.WhenSignalEquals("sentinel_category", "dangerous"),
            Triggers.WhenSignalEquals("sentiment", "negative"),
            Triggers.WhenSignalEquals("needs_deep_analysis", true)
        )
    ];

    // Model configuration - 7B class for thorough safety analysis
    private const string Model = "llama3.1:8b"; // Or "mistral:7b", "codellama:7b"

    public SafetyEvaluatorProposer(IOllamaClient ollamaClient)
    {
        _ollamaClient = ollamaClient;
        // Bounded coordinator: max 2 concurrent expensive LLM calls
        _coordinator = new BoundedAsyncCoordinator(maxConcurrent: 2);
    }

    public override async Task<IReadOnlyList<ConstrainedSignal>> ProposeAsync(
        ProposerState<PromptContext> state,
        CancellationToken cancellationToken = default)
    {
        var prompt = state.Context.Prompt;

        // Get context from previous waves
        var sentinelCategory = state.GetSignal<string>("sentinel_category") ?? "unknown";
        var sentiment = state.GetSignal<string>("sentiment") ?? "neutral";
        var intent = state.GetSignal<string>("intent") ?? "unknown";

        // Use bounded coordinator for expensive 7B model
        var result = await _coordinator.ExecuteAsync(async ct =>
        {
            var systemPrompt = $$"""
                You are a safety evaluator. Context from previous analysis:
                - Sentinel triage: {{sentinelCategory}}
                - Sentiment: {{sentiment}}
                - Intent: {{intent}}

                Perform deep safety analysis and respond with JSON:
                {
                    "is_safe": true|false,
                    "severity": 0.0-1.0,
                    "concerns": ["concern1", "concern2"],
                    "categories": ["category1"],
                    "recommended_action": "allow|challenge|block|escalate"
                }

                Categories can include: harmful_content, hate_speech, violence, illegal_activity, privacy_violation, manipulation, etc.
                """;

            return await _ollamaClient.GenerateJsonAsync<SafetyResponse>(
                $"Evaluate safety: {prompt}",
                Model,
                systemPrompt,
                ct);
        }, cancellationToken);

        // Fallback if LLM fails or times out
        result ??= FallbackSafetyCheck(prompt, sentinelCategory);

        var facts = new SafetyFacts
        {
            IsSafe = result.IsSafe,
            Severity = result.Severity,
            Concerns = result.Concerns ?? [],
            Categories = result.Categories ?? [],
            RecommendedAction = result.RecommendedAction ?? "allow"
        };

        // Safety severity directly maps to risk score
        var riskScore = result.IsSafe ? 0.1f : (float)(0.5 + result.Severity * 0.5);

        var signal = CreateSignal(facts, riskScore)
            .WithMetadata("orchestration_signals", new Dictionary<string, object>
            {
                ["safety_evaluated"] = true,
                ["is_safe"] = result.IsSafe,
                ["safety_severity"] = result.Severity,
                ["recommended_action"] = result.RecommendedAction ?? "allow"
            });

        // Immediate action on dangerous content
        if (!result.IsSafe && result.Severity > 0.8)
        {
            signal = signal.WithEarlyExit("blacklisted");
        }

        return Single(signal);
    }

    private static SafetyResponse FallbackSafetyCheck(string prompt, string sentinelCategory)
    {
        var lower = prompt.ToLowerInvariant();

        var dangerousPatterns = new[] { "hack", "exploit", "attack", "weapon", "bomb", "kill", "harm" };
        var hasDangerous = dangerousPatterns.Any(p => lower.Contains(p));

        if (hasDangerous || sentinelCategory == "dangerous")
        {
            return new SafetyResponse
            {
                IsSafe = false,
                Severity = 0.85,
                Concerns = ["Potentially harmful content detected"],
                Categories = ["potentially_harmful"],
                RecommendedAction = "block"
            };
        }

        if (sentinelCategory == "suspicious")
        {
            return new SafetyResponse
            {
                IsSafe = true,
                Severity = 0.3,
                Concerns = ["Flagged as suspicious by sentinel, but no clear issues found"],
                Categories = [],
                RecommendedAction = "challenge"
            };
        }

        return new SafetyResponse
        {
            IsSafe = true,
            Severity = 0.0,
            Concerns = [],
            Categories = [],
            RecommendedAction = "allow"
        };
    }

    private sealed class SafetyResponse
    {
        public bool IsSafe { get; set; } = true;
        public double Severity { get; set; }
        public string[]? Concerns { get; set; }
        public string[]? Categories { get; set; }
        public string? RecommendedAction { get; set; }
    }
}

/// <summary>
///     Bounded async coordinator for expensive operations.
///     Ensures only N concurrent expensive LLM calls at a time.
/// </summary>
public sealed class BoundedAsyncCoordinator
{
    private readonly SemaphoreSlim _semaphore;
    private readonly Channel<Func<CancellationToken, Task<object?>>> _queue;

    public int MaxConcurrent { get; }
    public int CurrentlyRunning => MaxConcurrent - _semaphore.CurrentCount;

    public BoundedAsyncCoordinator(int maxConcurrent)
    {
        MaxConcurrent = maxConcurrent;
        _semaphore = new SemaphoreSlim(maxConcurrent, maxConcurrent);
        _queue = Channel.CreateUnbounded<Func<CancellationToken, Task<object?>>>();
    }

    public async Task<T?> ExecuteAsync<T>(
        Func<CancellationToken, Task<T?>> operation,
        CancellationToken cancellationToken = default) where T : class
    {
        await _semaphore.WaitAsync(cancellationToken);
        try
        {
            return await operation(cancellationToken);
        }
        finally
        {
            _semaphore.Release();
        }
    }

    public async Task<T?> ExecuteWithTimeoutAsync<T>(
        Func<CancellationToken, Task<T?>> operation,
        TimeSpan timeout,
        CancellationToken cancellationToken = default) where T : class
    {
        using var cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        cts.CancelAfter(timeout);

        try
        {
            return await ExecuteAsync(operation, cts.Token);
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            // Timeout occurred, return null
            return null;
        }
    }
}
