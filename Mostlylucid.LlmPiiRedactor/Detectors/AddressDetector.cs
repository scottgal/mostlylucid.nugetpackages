using System.Text.RegularExpressions;
using Mostlylucid.LlmPiiRedactor.Models;

namespace Mostlylucid.LlmPiiRedactor.Detectors;

/// <summary>
/// Detects street addresses.
/// </summary>
public class AddressDetector : BasePiiDetector
{
    public override PiiType PiiType => PiiType.Address;
    public override string Name => "AddressDetector";
    public override int Priority => 60;
    protected override double DefaultConfidence => 0.75;

    // Pattern for street addresses: number + street name + suffix
    protected override string Pattern =>
        @"\d{1,5}\s+(?:[A-Z][a-z]+\s*){1,4}(?:Street|St|Avenue|Ave|Road|Rd|Boulevard|Blvd|Drive|Dr|Lane|Ln|Way|Court|Ct|Place|Pl|Circle|Cir|Terrace|Ter|Highway|Hwy|Parkway|Pkwy)\.?(?:\s*(?:#|Apt|Suite|Ste|Unit|Floor|Fl)\.?\s*\w+)?";

    protected override RegexOptions AdditionalRegexOptions => RegexOptions.IgnoreCase;

    // Common street suffixes for validation
    private static readonly HashSet<string> StreetSuffixes = new(StringComparer.OrdinalIgnoreCase)
    {
        "Street", "St", "Avenue", "Ave", "Road", "Rd", "Boulevard", "Blvd",
        "Drive", "Dr", "Lane", "Ln", "Way", "Court", "Ct", "Place", "Pl",
        "Circle", "Cir", "Terrace", "Ter", "Highway", "Hwy", "Parkway", "Pkwy",
        "Trail", "Trl", "Square", "Sq", "Loop", "Alley", "Path", "Row"
    };

    protected override bool ValidateMatch(Match match, string originalText)
    {
        var address = match.Value;

        // Must have at least a number and some text
        if (!Regex.IsMatch(address, @"^\d+\s+\w"))
            return false;

        // Must contain a street suffix
        var words = address.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        return words.Any(w => StreetSuffixes.Contains(w.TrimEnd('.', ',')));
    }

    protected override double CalculateConfidence(Match match, string originalText)
    {
        var address = match.Value;
        var confidence = 0.7;

        // Higher confidence with apartment/unit numbers
        if (Regex.IsMatch(address, @"(?:#|Apt|Suite|Ste|Unit)", RegexOptions.IgnoreCase))
            confidence += 0.15;

        // Check context for address-related keywords
        var contextStart = Math.Max(0, match.Index - 30);
        var contextEnd = Math.Min(originalText.Length, match.Index + match.Length + 30);
        var context = originalText.Substring(contextStart, contextEnd - contextStart).ToLowerInvariant();

        var addressKeywords = new[] { "address:", "location:", "ship to:", "deliver to:", "residence:", "home:", "office:" };
        if (addressKeywords.Any(context.Contains))
            confidence += 0.15;

        return Math.Clamp(confidence, 0.0, 1.0);
    }
}
