using System.Text.RegularExpressions;
using Mostlylucid.LlmPiiRedactor.Models;

namespace Mostlylucid.LlmPiiRedactor.Detectors;

/// <summary>
///     Detects US Social Security Numbers.
/// </summary>
public class SsnDetector : BasePiiDetector
{
    // Invalid area numbers (first 3 digits)
    private static readonly HashSet<string> InvalidAreaNumbers = new()
    {
        "000", "666"
    };

    public override PiiType PiiType => PiiType.Ssn;
    public override string Name => "SsnDetector";
    public override int Priority => 5; // High priority for security

    // SSN format: XXX-XX-XXXX or XXXXXXXXX
    protected override string Pattern =>
        @"(?<!\d)(?!000|666|9\d{2})\d{3}[-\s]?(?!00)\d{2}[-\s]?(?!0000)\d{4}(?!\d)";

    protected override bool ValidateMatch(Match match, string originalText)
    {
        var ssn = match.Value;
        var digitsOnly = new string(ssn.Where(char.IsDigit).ToArray());

        // Must be exactly 9 digits
        if (digitsOnly.Length != 9)
            return false;

        var area = digitsOnly[..3];
        var group = digitsOnly.Substring(3, 2);
        var serial = digitsOnly.Substring(5, 4);

        // Area number cannot be 000, 666, or 900-999
        if (InvalidAreaNumbers.Contains(area) || area.StartsWith('9'))
            return false;

        // Group number cannot be 00
        if (group == "00")
            return false;

        // Serial number cannot be 0000
        if (serial == "0000")
            return false;

        return true;
    }

    protected override double CalculateConfidence(Match match, string originalText)
    {
        var ssn = match.Value;

        // Higher confidence if formatted with dashes
        if (ssn.Contains('-'))
            return 0.95;

        // Check context for SSN-related keywords
        var contextStart = Math.Max(0, match.Index - 30);
        var contextEnd = Math.Min(originalText.Length, match.Index + match.Length + 30);
        var context = originalText.Substring(contextStart, contextEnd - contextStart).ToLowerInvariant();

        var ssnKeywords = new[] { "ssn", "social security", "social-security", "ss#", "ss #" };
        if (ssnKeywords.Any(context.Contains))
            return 1.0;

        return 0.85;
    }
}