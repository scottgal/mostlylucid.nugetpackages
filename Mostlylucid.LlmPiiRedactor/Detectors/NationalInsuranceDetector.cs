using System.Text.RegularExpressions;
using Mostlylucid.LlmPiiRedactor.Models;

namespace Mostlylucid.LlmPiiRedactor.Detectors;

/// <summary>
/// Detects UK National Insurance Numbers.
/// </summary>
public class NationalInsuranceDetector : BasePiiDetector
{
    public override PiiType PiiType => PiiType.NationalInsurance;
    public override string Name => "NationalInsuranceDetector";
    public override int Priority => 15;

    // UK NI Number: 2 letters, 6 digits, 1 letter (A, B, C, or D)
    // Example: AB123456C
    protected override string Pattern =>
        @"(?<![A-Z])[A-CEGHJ-PR-TW-Z]{2}\s*\d{2}\s*\d{2}\s*\d{2}\s*[A-D](?![A-Z])";

    protected override RegexOptions AdditionalRegexOptions => RegexOptions.IgnoreCase;

    // Invalid prefixes for NI numbers
    private static readonly HashSet<string> InvalidPrefixes = new(StringComparer.OrdinalIgnoreCase)
    {
        "BG", "GB", "NK", "KN", "TN", "NT", "ZZ"
    };

    protected override bool ValidateMatch(Match match, string originalText)
    {
        var nino = match.Value.ToUpperInvariant().Replace(" ", "");

        // Must be 9 characters
        if (nino.Length != 9)
            return false;

        // Check prefix
        var prefix = nino[..2];
        if (InvalidPrefixes.Contains(prefix))
            return false;

        // First letter cannot be D, F, I, Q, U, or V
        var invalidFirstChars = "DFIQUV";
        if (invalidFirstChars.Contains(nino[0]))
            return false;

        // Second letter cannot be D, F, I, O, Q, U, or V
        var invalidSecondChars = "DFIOQU";
        if (invalidSecondChars.Contains(nino[1]))
            return false;

        return true;
    }

    protected override double CalculateConfidence(Match match, string originalText)
    {
        var nino = match.Value;
        var confidence = 0.85;

        // Higher confidence if formatted with spaces
        if (nino.Contains(' '))
            confidence += 0.05;

        // Check context for NI-related keywords
        var contextStart = Math.Max(0, match.Index - 40);
        var contextEnd = Math.Min(originalText.Length, match.Index + match.Length + 40);
        var context = originalText.Substring(contextStart, contextEnd - contextStart).ToLowerInvariant();

        var niKeywords = new[] { "national insurance", "ni number", "nino", "insurance number" };
        if (niKeywords.Any(context.Contains))
            confidence = 1.0;

        return Math.Clamp(confidence, 0.0, 1.0);
    }
}
