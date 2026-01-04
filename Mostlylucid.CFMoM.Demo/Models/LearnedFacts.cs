namespace Mostlylucid.CFMoM.Demo.Models;

/// <summary>
///     Facts schema for learned/cached decision signals.
/// </summary>
public sealed record LearnedFacts
{
    /// <summary>
    ///     Schema ID for this facts type.
    /// </summary>
    public const string SchemaId = "learned.v1";

    /// <summary>
    ///     ID of the matched learned decision.
    /// </summary>
    public required Guid MatchedDecisionId { get; init; }

    /// <summary>
    ///     Similarity score between current prompt and matched prompt.
    /// </summary>
    public required double SimilarityScore { get; init; }

    /// <summary>
    ///     The original prompt that was matched.
    /// </summary>
    public required string MatchedPromptText { get; init; }

    /// <summary>
    ///     Fact match score from verification step.
    /// </summary>
    public required double FactMatchScore { get; init; }

    /// <summary>
    ///     The learned decision to apply.
    /// </summary>
    public required string LearnedDecision { get; init; }

    /// <summary>
    ///     Reason for the learned decision.
    /// </summary>
    public string? Reason { get; init; }

    /// <summary>
    ///     How many times this decision has been reused.
    /// </summary>
    public int HitCount { get; init; }
}
