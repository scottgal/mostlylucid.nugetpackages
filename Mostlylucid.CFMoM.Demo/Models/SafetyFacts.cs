namespace Mostlylucid.CFMoM.Demo.Models;

/// <summary>
///     Facts schema for safety/appropriateness signals.
/// </summary>
public sealed record SafetyFacts
{
    /// <summary>
    ///     Schema ID for this facts type.
    /// </summary>
    public const string SchemaId = "safety.v1";

    /// <summary>
    ///     Whether the content is safe to process.
    /// </summary>
    public required bool IsSafe { get; init; }

    /// <summary>
    ///     Severity score (0-1, higher = more severe concerns).
    /// </summary>
    public required double Severity { get; init; }

    /// <summary>
    ///     List of safety concerns identified.
    /// </summary>
    public string[] Concerns { get; init; } = [];

    /// <summary>
    ///     Categories of issues detected.
    /// </summary>
    public string[] Categories { get; init; } = [];

    /// <summary>
    ///     Recommended action.
    /// </summary>
    public string RecommendedAction { get; init; } = "allow";
}
