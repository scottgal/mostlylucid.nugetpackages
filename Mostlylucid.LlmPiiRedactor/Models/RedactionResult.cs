namespace Mostlylucid.LlmPiiRedactor.Models;

/// <summary>
///     Result of a PII redaction operation.
/// </summary>
public sealed class RedactionResult
{
    /// <summary>
    ///     The redacted text with all PII replaced.
    /// </summary>
    public required string RedactedText { get; init; }

    /// <summary>
    ///     The original input text.
    /// </summary>
    public required string OriginalText { get; init; }

    /// <summary>
    ///     Collection of all PII matches found.
    /// </summary>
    public IReadOnlyList<PiiMatch> Matches { get; init; } = [];

    /// <summary>
    ///     Whether any PII was detected and redacted.
    /// </summary>
    public bool ContainedPii => Matches.Count > 0;

    /// <summary>
    ///     Count of unique PII types found.
    /// </summary>
    public int UniqueTypesCount => Matches.Select(m => m.Type).Distinct().Count();

    /// <summary>
    ///     Summary of PII types found and their counts.
    /// </summary>
    public IReadOnlyDictionary<PiiType, int> TypeCounts =>
        Matches.GroupBy(m => m.Type).ToDictionary(g => g.Key, g => g.Count());

    /// <summary>
    ///     Creates an empty result indicating no PII was found.
    /// </summary>
    public static RedactionResult NoMatch(string originalText)
    {
        return new RedactionResult
        {
            OriginalText = originalText,
            RedactedText = originalText,
            Matches = []
        };
    }
}