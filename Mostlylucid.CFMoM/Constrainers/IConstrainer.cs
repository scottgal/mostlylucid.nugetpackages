using Mostlylucid.CFMoM.Aggregation;

namespace Mostlylucid.CFMoM.Constrainers;

/// <summary>
///     The constrainer is the deterministic decision-maker.
///     It receives aggregated results and produces decisions.
///
///     Key principle: Only the constrainer can trigger side effects.
///     The constrainer contains NO probabilistic logic.
///     All decisions are based on deterministic rules.
/// </summary>
public interface IConstrainer<TContext, TDecision>
{
    /// <summary>
    ///     Evaluate the aggregated result and produce a decision.
    /// </summary>
    /// <param name="result">Aggregated signals from proposers.</param>
    /// <param name="context">Original context for additional checks.</param>
    /// <returns>The deterministic decision.</returns>
    ConstrainerResult<TDecision> Evaluate(AggregatedResult result, TContext context);
}

/// <summary>
///     Result from the constrainer evaluation.
/// </summary>
/// <typeparam name="TDecision">The decision type.</typeparam>
public sealed record ConstrainerResult<TDecision>
{
    /// <summary>
    ///     The decision made by the constrainer.
    /// </summary>
    public required TDecision Decision { get; init; }

    /// <summary>
    ///     Whether to continue processing (for orchestration).
    /// </summary>
    public bool ShouldContinue { get; init; } = true;

    /// <summary>
    ///     Reason for the decision (for audit/logging).
    /// </summary>
    public string? Reason { get; init; }

    /// <summary>
    ///     The rule or transition that triggered this decision.
    /// </summary>
    public string? TriggeredBy { get; init; }

    /// <summary>
    ///     Create a result that continues processing.
    /// </summary>
    public static ConstrainerResult<TDecision> Continue(TDecision decision, string? reason = null)
        => new() { Decision = decision, ShouldContinue = true, Reason = reason };

    /// <summary>
    ///     Create a result that stops processing.
    /// </summary>
    public static ConstrainerResult<TDecision> Stop(TDecision decision, string reason, string? triggeredBy = null)
        => new() { Decision = decision, ShouldContinue = false, Reason = reason, TriggeredBy = triggeredBy };
}

/// <summary>
///     Common decision types for typical use cases.
/// </summary>
public enum CommonDecision
{
    /// <summary>Allow/accept.</summary>
    Allow,

    /// <summary>Block/reject.</summary>
    Block,

    /// <summary>Challenge (request more verification).</summary>
    Challenge,

    /// <summary>Throttle (slow down).</summary>
    Throttle,

    /// <summary>Escalate (need more analysis).</summary>
    Escalate
}
