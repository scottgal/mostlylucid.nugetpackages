using System.Text.RegularExpressions;
using Mostlylucid.LlmPiiRedactor.Models;

namespace Mostlylucid.LlmPiiRedactor.Detectors;

/// <summary>
/// Detects generic account IDs, user IDs, and similar identifiers.
/// </summary>
public class AccountIdDetector : BasePiiDetector
{
    public override PiiType PiiType => PiiType.AccountId;
    public override string Name => "AccountIdDetector";
    public override int Priority => 80; // Lower priority, relies heavily on context
    protected override double DefaultConfidence => 0.65;

    // Pattern for IDs in various formats
    // UUID: 550e8400-e29b-41d4-a716-446655440000
    // Numeric IDs with prefixes: USER-12345, ACC-67890, ID:12345
    // Pure numeric IDs need context
    protected override string Pattern =>
        @"(?<!\w)(?:[a-fA-F0-9]{8}-[a-fA-F0-9]{4}-[a-fA-F0-9]{4}-[a-fA-F0-9]{4}-[a-fA-F0-9]{12}|(?:USER|ACC|ID|CUST|CLIENT|MEMBER|ACCOUNT|UID|PID|CID)[-_:]?\d{4,12}|\d{6,12})(?!\w)";

    protected override RegexOptions AdditionalRegexOptions => RegexOptions.IgnoreCase;

    protected override bool ValidateMatch(Match match, string originalText)
    {
        var value = match.Value;

        // UUID format is always valid
        if (Regex.IsMatch(value, @"^[a-fA-F0-9]{8}-[a-fA-F0-9]{4}-[a-fA-F0-9]{4}-[a-fA-F0-9]{4}-[a-fA-F0-9]{12}$"))
            return true;

        // Prefixed IDs are always valid
        if (Regex.IsMatch(value, @"^(?:USER|ACC|ID|CUST|CLIENT|MEMBER|ACCOUNT|UID|PID|CID)", RegexOptions.IgnoreCase))
            return true;

        // Pure numeric IDs need context validation
        if (value.All(char.IsDigit))
        {
            var contextStart = Math.Max(0, match.Index - 30);
            var contextEnd = Math.Min(originalText.Length, match.Index + match.Length + 15);
            var context = originalText.Substring(contextStart, contextEnd - contextStart).ToLowerInvariant();

            var idKeywords = new[]
            {
                "id:", "id=", "user", "account", "customer", "client", "member",
                "userid", "accountid", "customerid", "clientid", "memberid"
            };

            return idKeywords.Any(context.Contains);
        }

        return false;
    }

    protected override double CalculateConfidence(Match match, string originalText)
    {
        var value = match.Value;

        // UUID format has high confidence
        if (Regex.IsMatch(value, @"^[a-fA-F0-9]{8}-[a-fA-F0-9]{4}-[a-fA-F0-9]{4}-[a-fA-F0-9]{4}-[a-fA-F0-9]{12}$"))
            return 0.9;

        // Prefixed IDs have good confidence
        if (Regex.IsMatch(value, @"^(?:USER|ACC|ID|CUST|CLIENT|MEMBER|ACCOUNT|UID|PID|CID)", RegexOptions.IgnoreCase))
            return 0.85;

        // Context-dependent numeric IDs
        return 0.7;
    }
}
