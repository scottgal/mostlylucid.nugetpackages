namespace Mostlylucid.CFMoM.Demo.Learning;

/// <summary>
///     A learned decision stored in the learning database.
/// </summary>
public sealed class LearnedDecision
{
    public required Guid Id { get; init; }
    public required string PromptText { get; init; }
    public required float[] PromptEmbedding { get; init; }
    public required string Decision { get; init; }
    public string? Reason { get; init; }
    public double Score { get; init; }
    public double Confidence { get; init; }
    public DateTimeOffset CreatedAt { get; init; }
    public DateTimeOffset UpdatedAt { get; set; }
    public int HitCount { get; set; }
    public double? FeedbackScore { get; set; }
}
