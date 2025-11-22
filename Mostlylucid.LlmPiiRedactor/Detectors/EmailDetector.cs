using System.Text.RegularExpressions;
using Mostlylucid.LlmPiiRedactor.Models;

namespace Mostlylucid.LlmPiiRedactor.Detectors;

/// <summary>
/// Detects email addresses.
/// </summary>
public class EmailDetector : BasePiiDetector
{
    public override PiiType PiiType => PiiType.Email;
    public override string Name => "EmailDetector";
    public override int Priority => 10;

    // RFC 5322 compliant email pattern (simplified)
    protected override string Pattern =>
        @"[a-zA-Z0-9._%+-]+@[a-zA-Z0-9.-]+\.[a-zA-Z]{2,}";

    protected override RegexOptions AdditionalRegexOptions => RegexOptions.IgnoreCase;

    protected override bool ValidateMatch(Match match, string originalText)
    {
        var email = match.Value;

        // Basic validation
        if (email.Length < 5 || email.Length > 254)
            return false;

        // Must have exactly one @
        if (email.Count(c => c == '@') != 1)
            return false;

        var parts = email.Split('@');
        if (parts.Length != 2)
            return false;

        var local = parts[0];
        var domain = parts[1];

        // Local part validation
        if (string.IsNullOrEmpty(local) || local.Length > 64)
            return false;

        // Domain validation
        if (string.IsNullOrEmpty(domain) || domain.Length > 253)
            return false;

        // Domain must have at least one dot
        if (!domain.Contains('.'))
            return false;

        // TLD must be at least 2 characters
        var tld = domain.Split('.').Last();
        if (tld.Length < 2)
            return false;

        return true;
    }

    protected override double CalculateConfidence(Match match, string originalText)
    {
        var email = match.Value;
        var confidence = 0.9;

        // Higher confidence for common domains
        var commonDomains = new[] { "gmail.com", "yahoo.com", "hotmail.com", "outlook.com", "icloud.com" };
        if (commonDomains.Any(d => email.EndsWith(d, StringComparison.OrdinalIgnoreCase)))
            confidence = 1.0;

        // Lower confidence for very short local parts
        var localPart = email.Split('@')[0];
        if (localPart.Length < 3)
            confidence *= 0.9;

        return confidence;
    }
}
