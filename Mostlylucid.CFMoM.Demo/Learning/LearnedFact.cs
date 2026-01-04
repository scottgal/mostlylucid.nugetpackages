namespace Mostlylucid.CFMoM.Demo.Learning;

/// <summary>
///     A learned fact associated with a decision.
/// </summary>
public sealed class LearnedFact
{
    public required Guid Id { get; init; }
    public required Guid DecisionId { get; init; }
    public required string SchemaId { get; init; }
    public required string FactKey { get; init; }
    public required string FactValue { get; set; }
    public double Confidence { get; set; }
    public int OccurrenceCount { get; set; }
}
