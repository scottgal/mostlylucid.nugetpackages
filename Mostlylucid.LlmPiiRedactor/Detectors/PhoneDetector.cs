using System.Text.RegularExpressions;
using Mostlylucid.LlmPiiRedactor.Models;

namespace Mostlylucid.LlmPiiRedactor.Detectors;

/// <summary>
///     Detects phone numbers in various international formats.
/// </summary>
public class PhoneDetector : BasePiiDetector
{
    public override PiiType PiiType => PiiType.PhoneNumber;
    public override string Name => "PhoneDetector";
    public override int Priority => 20;

    // Matches various phone formats:
    // +1-234-567-8901, (123) 456-7890, 123.456.7890, +44 20 7946 0958, etc.
    protected override string Pattern =>
        @"(?<!\d)(?:\+?[1-9]\d{0,2}[-.\s]?)?(?:\(?\d{2,4}\)?[-.\s]?)?\d{3,4}[-.\s]?\d{3,4}(?!\d)";

    protected override bool ValidateMatch(Match match, string originalText)
    {
        var phone = match.Value;

        // Remove non-digit characters for validation
        var digitsOnly = new string(phone.Where(char.IsDigit).ToArray());

        // Must have between 7 and 15 digits (ITU-T E.164)
        if (digitsOnly.Length < 7 || digitsOnly.Length > 15)
            return false;

        // Avoid matching things like years (1990-2024) or version numbers
        if (Regex.IsMatch(phone, @"^(19|20)\d{2}$"))
            return false;

        // Avoid IP addresses (already handled by IP detector, but double-check)
        if (Regex.IsMatch(phone, @"^\d{1,3}\.\d{1,3}\.\d{1,3}\.\d{1,3}$"))
            return false;

        // Check for repeated digits (unlikely to be real phone)
        if (digitsOnly.Distinct().Count() < 3)
            return false;

        return true;
    }

    protected override double CalculateConfidence(Match match, string originalText)
    {
        var phone = match.Value;
        var digitsOnly = new string(phone.Where(char.IsDigit).ToArray());
        var confidence = 0.8;

        // Higher confidence for formatted numbers
        if (phone.Contains('(') || phone.Contains('-') || phone.StartsWith('+'))
            confidence += 0.1;

        // Higher confidence for standard lengths
        if (digitsOnly.Length is 10 or 11)
            confidence += 0.1;

        // Lower confidence if surrounded by other digits
        var index = match.Index;
        if (index > 0 && char.IsDigit(originalText[index - 1]))
            confidence -= 0.3;
        if (index + match.Length < originalText.Length && char.IsDigit(originalText[index + match.Length]))
            confidence -= 0.3;

        return Math.Clamp(confidence, 0.0, 1.0);
    }
}