using System.Text.RegularExpressions;
using Mostlylucid.LlmPiiRedactor.Models;

namespace Mostlylucid.LlmPiiRedactor.Detectors;

/// <summary>
/// Detects API keys, tokens, and secrets.
/// </summary>
public class ApiKeyDetector : BasePiiDetector
{
    public override PiiType PiiType => PiiType.ApiKey;
    public override string Name => "ApiKeyDetector";
    public override int Priority => 1; // Highest priority for security

    // Patterns for common API key formats
    // AWS: AKIA[0-9A-Z]{16}
    // GitHub: ghp_[a-zA-Z0-9]{36}
    // Stripe: sk_live_[a-zA-Z0-9]{24}
    // Generic: long alphanumeric strings with mixed case
    protected override string Pattern =>
        @"(?<!\w)(?:AKIA[0-9A-Z]{16}|ghp_[a-zA-Z0-9]{36}|gho_[a-zA-Z0-9]{36}|sk_live_[a-zA-Z0-9]{24,}|sk_test_[a-zA-Z0-9]{24,}|pk_live_[a-zA-Z0-9]{24,}|pk_test_[a-zA-Z0-9]{24,}|xox[baprs]-[a-zA-Z0-9-]{10,}|sq0atp-[a-zA-Z0-9_-]{22}|sq0csp-[a-zA-Z0-9_-]{43}|AIza[0-9A-Za-z_-]{35}|[a-zA-Z0-9]{32,64})(?!\w)";

    // Known API key prefixes with their services
    private static readonly Dictionary<string, string> KnownPrefixes = new(StringComparer.OrdinalIgnoreCase)
    {
        { "AKIA", "AWS Access Key" },
        { "ghp_", "GitHub Personal Access Token" },
        { "gho_", "GitHub OAuth Token" },
        { "sk_live_", "Stripe Live Secret Key" },
        { "sk_test_", "Stripe Test Secret Key" },
        { "pk_live_", "Stripe Live Publishable Key" },
        { "pk_test_", "Stripe Test Publishable Key" },
        { "xoxb-", "Slack Bot Token" },
        { "xoxp-", "Slack User Token" },
        { "xoxa-", "Slack App Token" },
        { "sq0atp-", "Square Access Token" },
        { "sq0csp-", "Square OAuth Secret" },
        { "AIza", "Google API Key" }
    };

    protected override bool ValidateMatch(Match match, string originalText)
    {
        var value = match.Value;

        // Check for known prefixes - these are definitely API keys
        if (KnownPrefixes.Keys.Any(prefix => value.StartsWith(prefix, StringComparison.OrdinalIgnoreCase)))
            return true;

        // For generic long strings, apply additional validation
        if (value.Length >= 32)
        {
            // Must have mixed characters (not all same char)
            if (value.Distinct().Count() < 10)
                return false;

            // Should have mix of uppercase, lowercase, or digits
            var hasUpper = value.Any(char.IsUpper);
            var hasLower = value.Any(char.IsLower);
            var hasDigit = value.Any(char.IsDigit);

            // At least 2 of the 3 character types
            var typeCount = (hasUpper ? 1 : 0) + (hasLower ? 1 : 0) + (hasDigit ? 1 : 0);
            if (typeCount < 2)
                return false;

            // Check context - needs API/key/token nearby
            var contextStart = Math.Max(0, match.Index - 50);
            var contextEnd = Math.Min(originalText.Length, match.Index + match.Length + 20);
            var context = originalText.Substring(contextStart, contextEnd - contextStart).ToLowerInvariant();

            var apiKeywords = new[] { "key", "token", "api", "secret", "auth", "bearer", "password", "credential" };
            return apiKeywords.Any(context.Contains);
        }

        return false;
    }

    protected override double CalculateConfidence(Match match, string originalText)
    {
        var value = match.Value;

        // Known prefixes have very high confidence
        if (KnownPrefixes.Keys.Any(prefix => value.StartsWith(prefix, StringComparison.OrdinalIgnoreCase)))
            return 0.99;

        // Context-based generic keys
        return 0.85;
    }
}
