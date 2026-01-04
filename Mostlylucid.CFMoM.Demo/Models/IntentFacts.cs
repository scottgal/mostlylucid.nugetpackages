namespace Mostlylucid.CFMoM.Demo.Models;

/// <summary>
///     Facts schema for intent classification signals.
/// </summary>
public sealed record IntentFacts
{
    /// <summary>
    ///     Schema ID for this facts type.
    /// </summary>
    public const string SchemaId = "intent.v1";

    /// <summary>
    ///     The detected intent type.
    /// </summary>
    public required string Intent { get; init; }

    /// <summary>
    ///     Confidence of the classification (0-1).
    /// </summary>
    public required double Confidence { get; init; }

    /// <summary>
    ///     Whether this is a follow-up question.
    /// </summary>
    public bool IsFollowUp { get; init; }

    /// <summary>
    ///     Keywords that influenced the classification.
    /// </summary>
    public string[] KeyIndicators { get; init; } = [];
}
