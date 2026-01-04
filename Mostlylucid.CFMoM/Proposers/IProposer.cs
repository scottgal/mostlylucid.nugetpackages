using Mostlylucid.CFMoM.Signals;

namespace Mostlylucid.CFMoM.Proposers;

/// <summary>
///     A proposer emits signals (evidence) rather than verdicts.
///     Part of the CFMoM architecture - proposers contribute probabilistic signals,
///     the constrainer makes deterministic decisions.
///
///     Key principle: Proposers interpret reality, they never define it.
/// </summary>
/// <typeparam name="TContext">The context type passed to proposers (e.g., HttpContext, document, etc.)</typeparam>
public interface IProposer<TContext>
{
    /// <summary>
    ///     Unique name of this proposer.
    /// </summary>
    string Name { get; }

    /// <summary>
    ///     Priority determines execution order when multiple proposers can run.
    ///     Lower = runs first. Critical (0) runs before Normal (100).
    /// </summary>
    int Priority => 100;

    /// <summary>
    ///     Whether this proposer is enabled.
    ///     Checked before running - allows runtime disable.
    /// </summary>
    bool IsEnabled => true;

    /// <summary>
    ///     Trigger conditions that must be met before this proposer runs.
    ///     Empty = no conditions, runs in the first wave.
    /// </summary>
    IReadOnlyList<TriggerCondition> TriggerConditions => Array.Empty<TriggerCondition>();

    /// <summary>
    ///     Maximum time to wait for trigger conditions before skipping.
    ///     Default: 500ms
    /// </summary>
    TimeSpan TriggerTimeout => TimeSpan.FromMilliseconds(500);

    /// <summary>
    ///     Maximum time allowed for this proposer to execute.
    ///     Default: 2 seconds
    /// </summary>
    TimeSpan ExecutionTimeout => TimeSpan.FromSeconds(2);

    /// <summary>
    ///     Whether this proposer can be skipped if it times out or fails.
    ///     Default: true (most proposers are optional)
    /// </summary>
    bool IsOptional => true;

    /// <summary>
    ///     The schema ID for signals this proposer emits.
    ///     Used for validation at ingestion.
    /// </summary>
    string FactsSchemaId { get; }

    /// <summary>
    ///     Run the proposer and return zero or more signals.
    ///     Proposer receives the orchestration state and can read signals from prior proposers.
    /// </summary>
    Task<IReadOnlyList<ConstrainedSignal>> ProposeAsync(
        ProposerState<TContext> state,
        CancellationToken cancellationToken = default);
}

/// <summary>
///     State passed to proposers during orchestration.
///     Contains all signals from prior proposers plus the context.
/// </summary>
/// <typeparam name="TContext">The context type.</typeparam>
public sealed class ProposerState<TContext>
{
    /// <summary>
    ///     The context being analyzed (e.g., HTTP request, document, etc.)
    /// </summary>
    public required TContext Context { get; init; }

    /// <summary>
    ///     All orchestration signals collected so far (not ConstrainedSignals, but system signals).
    ///     Used for trigger conditions and inter-proposer communication.
    /// </summary>
    public required IReadOnlyDictionary<string, object> Signals { get; init; }

    /// <summary>
    ///     All ConstrainedSignals collected so far.
    /// </summary>
    public required IReadOnlyList<ConstrainedSignal> CollectedSignals { get; init; }

    /// <summary>
    ///     Current aggregated score (0.0 to 1.0) from collected signals.
    /// </summary>
    public double CurrentScore { get; init; }

    /// <summary>
    ///     Which proposers have already run.
    /// </summary>
    public required IReadOnlySet<string> CompletedProposers { get; init; }

    /// <summary>
    ///     Which proposers failed.
    /// </summary>
    public required IReadOnlySet<string> FailedProposers { get; init; }

    /// <summary>
    ///     Correlation ID for this orchestration run.
    /// </summary>
    public required string CorrelationId { get; init; }

    /// <summary>
    ///     Time elapsed since orchestration started.
    /// </summary>
    public TimeSpan Elapsed { get; init; }

    /// <summary>
    ///     Get a typed signal value.
    /// </summary>
    public T? GetSignal<T>(string key)
    {
        return Signals.TryGetValue(key, out var value) && value is T typed ? typed : default;
    }

    /// <summary>
    ///     Check if a signal exists.
    /// </summary>
    public bool HasSignal(string key)
    {
        return Signals.ContainsKey(key);
    }

    /// <summary>
    ///     Get signals from a specific proposer.
    /// </summary>
    public IEnumerable<ConstrainedSignal> GetSignalsFrom(string proposerName)
    {
        return CollectedSignals.Where(s => s.SourceId == proposerName);
    }

    /// <summary>
    ///     Get signals with a specific schema.
    /// </summary>
    public IEnumerable<ConstrainedSignal> GetSignalsBySchema(string schemaId)
    {
        return CollectedSignals.Where(s => s.FactsSchemaId == schemaId);
    }
}

/// <summary>
///     Base class for proposers with common functionality.
/// </summary>
/// <typeparam name="TContext">The context type.</typeparam>
public abstract class ProposerBase<TContext> : IProposer<TContext>
{
    public abstract string Name { get; }
    public abstract string FactsSchemaId { get; }

    public virtual int Priority => 100;
    public virtual bool IsEnabled => true;
    public virtual IReadOnlyList<TriggerCondition> TriggerConditions => Triggers.None;
    public virtual TimeSpan TriggerTimeout => TimeSpan.FromMilliseconds(500);
    public virtual TimeSpan ExecutionTimeout => TimeSpan.FromSeconds(2);
    public virtual bool IsOptional => true;

    public abstract Task<IReadOnlyList<ConstrainedSignal>> ProposeAsync(
        ProposerState<TContext> state,
        CancellationToken cancellationToken = default);

    /// <summary>
    ///     Helper to return a single signal.
    /// </summary>
    protected static IReadOnlyList<ConstrainedSignal> Single(ConstrainedSignal signal)
        => new[] { signal };

    /// <summary>
    ///     Helper to return multiple signals.
    /// </summary>
    protected static IReadOnlyList<ConstrainedSignal> Multiple(params ConstrainedSignal[] signals)
        => signals;

    /// <summary>
    ///     Helper to return no signals.
    /// </summary>
    protected static IReadOnlyList<ConstrainedSignal> None()
        => Array.Empty<ConstrainedSignal>();

    /// <summary>
    ///     Create a signal with this proposer as source.
    /// </summary>
    protected ConstrainedSignal CreateSignal<TFacts>(
        TFacts facts,
        float confidence,
        IReadOnlyList<EvidenceRef>? evidence = null,
        string? correlationId = null,
        string? subjectId = null)
    {
        return ConstrainedSignal.Create(
            Name,
            FactsSchemaId,
            facts,
            confidence,
            evidence,
            correlationId,
            subjectId);
    }
}
