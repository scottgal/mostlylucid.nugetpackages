using Mostlylucid.CFMoM.Signals;

namespace Mostlylucid.CFMoM.Aggregation;

/// <summary>
///     Aggregates signals into a score and classification.
///     Uses weighted averaging with configurable strategies.
/// </summary>
public interface IAggregator
{
    /// <summary>
    ///     Aggregate all signals into a result.
    /// </summary>
    AggregatedResult Aggregate(IReadOnlyList<ConstrainedSignal> signals);
}

/// <summary>
///     Result of aggregating signals.
/// </summary>
public sealed record AggregatedResult
{
    /// <summary>
    ///     All signals that contributed.
    /// </summary>
    public required IReadOnlyList<ConstrainedSignal> Signals { get; init; }

    /// <summary>
    ///     Final aggregated score (0.0 to 1.0).
    ///     Interpretation depends on use case.
    /// </summary>
    public required double Score { get; init; }

    /// <summary>
    ///     Confidence in the score (based on evidence strength).
    /// </summary>
    public required double Confidence { get; init; }

    /// <summary>
    ///     Classification band based on score.
    /// </summary>
    public required ClassificationBand Band { get; init; }

    /// <summary>
    ///     Whether an early exit was triggered.
    /// </summary>
    public bool EarlyExit { get; init; }

    /// <summary>
    ///     Early exit classification if applicable.
    /// </summary>
    public string? EarlyExitClassification { get; init; }

    /// <summary>
    ///     Breakdown by schema for explainability.
    /// </summary>
    public IReadOnlyDictionary<string, SchemaBreakdown> SchemaBreakdown { get; init; } =
        new Dictionary<string, SchemaBreakdown>();

    /// <summary>
    ///     Which proposers contributed.
    /// </summary>
    public required IReadOnlySet<string> ContributingProposers { get; init; }

    /// <summary>
    ///     Total processing time in milliseconds.
    /// </summary>
    public double TotalProcessingTimeMs { get; init; }
}

/// <summary>
///     Score breakdown for a single schema.
/// </summary>
public sealed record SchemaBreakdown
{
    public required string SchemaId { get; init; }
    public required double Score { get; init; }
    public required int SignalCount { get; init; }
    public required double AverageConfidence { get; init; }
}

/// <summary>
///     Classification bands based on aggregated score.
/// </summary>
public enum ClassificationBand
{
    /// <summary>No classification available.</summary>
    Unknown = 0,

    /// <summary>Very low score.</summary>
    VeryLow = 1,

    /// <summary>Low score.</summary>
    Low = 2,

    /// <summary>Medium score.</summary>
    Medium = 3,

    /// <summary>High score.</summary>
    High = 4,

    /// <summary>Very high score.</summary>
    VeryHigh = 5,

    /// <summary>Verified (confirmed by external verification).</summary>
    Verified = 6
}
