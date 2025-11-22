namespace Mostlylucid.LlmPiiRedactor.Models;

/// <summary>
/// Represents a detected PII match within text.
/// </summary>
public sealed class PiiMatch
{
    /// <summary>
    /// The type of PII detected.
    /// </summary>
    public required PiiType Type { get; init; }

    /// <summary>
    /// The original matched value.
    /// </summary>
    public required string OriginalValue { get; init; }

    /// <summary>
    /// The redacted replacement value.
    /// </summary>
    public required string RedactedValue { get; init; }

    /// <summary>
    /// Starting index in the original text.
    /// </summary>
    public required int StartIndex { get; init; }

    /// <summary>
    /// Length of the matched text.
    /// </summary>
    public required int Length { get; init; }

    /// <summary>
    /// Confidence score (0.0 to 1.0) for pattern-based detection.
    /// </summary>
    public double Confidence { get; init; } = 1.0;

    /// <summary>
    /// Name of the detector that found this match.
    /// </summary>
    public string? DetectorName { get; init; }
}
