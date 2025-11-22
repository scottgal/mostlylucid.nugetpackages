using System.Text.RegularExpressions;
using Mostlylucid.LlmPiiRedactor.Models;

namespace Mostlylucid.LlmPiiRedactor.Detectors;

/// <summary>
/// Detects postal/ZIP codes from various countries.
/// </summary>
public class PostCodeDetector : BasePiiDetector
{
    public override PiiType PiiType => PiiType.PostCode;
    public override string Name => "PostCodeDetector";
    public override int Priority => 45;
    protected override double DefaultConfidence => 0.8;

    // Combined pattern for various postal code formats
    // US: 12345 or 12345-6789
    // UK: SW1A 1AA, M1 1AE, etc.
    // Canada: A1A 1A1
    // Germany: 12345
    // France: 75001
    protected override string Pattern =>
        @"(?<!\d)(?:\d{5}(?:-\d{4})?|[A-Z]{1,2}\d{1,2}[A-Z]?\s*\d[A-Z]{2}|[A-Z]\d[A-Z]\s*\d[A-Z]\d)(?!\d)";

    protected override RegexOptions AdditionalRegexOptions => RegexOptions.IgnoreCase;

    protected override bool ValidateMatch(Match match, string originalText)
    {
        var postcode = match.Value.Trim();

        // US ZIP code validation
        if (Regex.IsMatch(postcode, @"^\d{5}(-\d{4})?$"))
        {
            var zip = postcode.Split('-')[0];
            var zipInt = int.Parse(zip);
            // Valid US ZIP codes range (exclude obvious invalid ranges)
            return zipInt >= 501 && zipInt <= 99950;
        }

        // UK postcode validation
        if (Regex.IsMatch(postcode, @"^[A-Z]{1,2}\d{1,2}[A-Z]?\s*\d[A-Z]{2}$", RegexOptions.IgnoreCase))
        {
            return true;
        }

        // Canadian postcode validation (letter-digit-letter digit-letter-digit)
        if (Regex.IsMatch(postcode, @"^[A-Z]\d[A-Z]\s*\d[A-Z]\d$", RegexOptions.IgnoreCase))
        {
            return true;
        }

        return true;
    }

    protected override double CalculateConfidence(Match match, string originalText)
    {
        var postcode = match.Value.Trim();
        var confidence = 0.75;

        // Check context for postal-related keywords
        var contextStart = Math.Max(0, match.Index - 30);
        var contextEnd = Math.Min(originalText.Length, match.Index + match.Length + 30);
        var context = originalText.Substring(contextStart, contextEnd - contextStart).ToLowerInvariant();

        var postcodeKeywords = new[] { "zip", "postal", "postcode", "post code" };
        if (postcodeKeywords.Any(context.Contains))
            confidence += 0.2;

        // Higher confidence for formatted postcodes
        if (postcode.Contains(' ') || postcode.Contains('-'))
            confidence += 0.1;

        return Math.Clamp(confidence, 0.0, 1.0);
    }
}
