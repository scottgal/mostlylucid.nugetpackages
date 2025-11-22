using System.Text.RegularExpressions;
using Mostlylucid.LlmPiiRedactor.Models;

namespace Mostlylucid.LlmPiiRedactor.Detectors;

/// <summary>
/// Base class for regex-based PII detectors.
/// </summary>
public abstract class BasePiiDetector : IPiiDetector
{
    private readonly Lazy<Regex> _compiledRegex;

    protected BasePiiDetector()
    {
        _compiledRegex = new Lazy<Regex>(() =>
            new Regex(Pattern, RegexOptions.Compiled | AdditionalRegexOptions, TimeSpan.FromSeconds(1)));
    }

    /// <summary>
    /// The regex pattern for detection.
    /// </summary>
    protected abstract string Pattern { get; }

    /// <summary>
    /// Additional regex options beyond Compiled.
    /// </summary>
    protected virtual RegexOptions AdditionalRegexOptions => RegexOptions.None;

    /// <inheritdoc />
    public abstract PiiType PiiType { get; }

    /// <inheritdoc />
    public abstract string Name { get; }

    /// <inheritdoc />
    public virtual int Priority => 100;

    /// <inheritdoc />
    public virtual bool IsEnabled => true;

    /// <summary>
    /// Default confidence for matches from this detector.
    /// </summary>
    protected virtual double DefaultConfidence => 1.0;

    /// <inheritdoc />
    public virtual IEnumerable<PiiMatch> Detect(string text, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrEmpty(text) || !IsEnabled)
            yield break;

        var matches = _compiledRegex.Value.Matches(text);

        foreach (Match match in matches)
        {
            cancellationToken.ThrowIfCancellationRequested();

            if (ValidateMatch(match, text))
            {
                yield return new PiiMatch
                {
                    Type = PiiType,
                    OriginalValue = match.Value,
                    RedactedValue = string.Empty, // Will be set by redaction service
                    StartIndex = match.Index,
                    Length = match.Length,
                    Confidence = CalculateConfidence(match, text),
                    DetectorName = Name
                };
            }
        }
    }

    /// <summary>
    /// Validate a potential match (override for additional validation).
    /// </summary>
    protected virtual bool ValidateMatch(Match match, string originalText) => true;

    /// <summary>
    /// Calculate confidence score for a match.
    /// </summary>
    protected virtual double CalculateConfidence(Match match, string originalText) => DefaultConfidence;
}
