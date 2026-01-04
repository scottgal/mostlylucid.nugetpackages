using Mostlylucid.CFMoM.Aggregation;
using Mostlylucid.CFMoM.Signals;

namespace Mostlylucid.CFMoM.Orchestration;

/// <summary>
///     Result from a CFMoM orchestration run.
/// </summary>
/// <typeparam name="TDecision">The decision type.</typeparam>
public sealed record CFMoMResult<TDecision>
{
    /// <summary>
    ///     Correlation ID for this orchestration run.
    /// </summary>
    public required string CorrelationId { get; init; }

    /// <summary>
    ///     The decision made by the constrainer.
    /// </summary>
    public required TDecision Decision { get; init; }

    /// <summary>
    ///     Reason for the decision.
    /// </summary>
    public string? Reason { get; init; }

    /// <summary>
    ///     Aggregated result from all signals.
    /// </summary>
    public required AggregatedResult Aggregation { get; init; }

    /// <summary>
    ///     All signals collected during orchestration.
    /// </summary>
    public required IReadOnlyList<ConstrainedSignal> Signals { get; init; }

    /// <summary>
    ///     Proposers that completed successfully.
    /// </summary>
    public required IReadOnlySet<string> CompletedProposers { get; init; }

    /// <summary>
    ///     Proposers that failed.
    /// </summary>
    public required IReadOnlySet<string> FailedProposers { get; init; }

    /// <summary>
    ///     Number of waves executed.
    /// </summary>
    public int WaveCount { get; init; }

    /// <summary>
    ///     Total duration in milliseconds.
    /// </summary>
    public double TotalDurationMs { get; init; }

    /// <summary>
    ///     The final score from aggregation.
    /// </summary>
    public double Score => Aggregation.Score;

    /// <summary>
    ///     The classification band from aggregation.
    /// </summary>
    public ClassificationBand Band => Aggregation.Band;

    /// <summary>
    ///     Whether an early exit occurred.
    /// </summary>
    public bool EarlyExit => Aggregation.EarlyExit;
}
