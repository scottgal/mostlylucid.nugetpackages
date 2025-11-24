using System.Text.RegularExpressions;
using Mostlylucid.LlmPiiRedactor.Models;

namespace Mostlylucid.LlmPiiRedactor.Detectors;

/// <summary>
///     Detects bank account numbers and IBANs.
/// </summary>
public class BankAccountDetector : BasePiiDetector
{
    public override PiiType PiiType => PiiType.BankAccount;
    public override string Name => "BankAccountDetector";
    public override int Priority => 10; // High priority for financial data

    // IBAN pattern (international) and generic account number pattern
    protected override string Pattern =>
        @"(?<!\w)(?:[A-Z]{2}\d{2}[A-Z0-9]{4}\d{7}(?:[A-Z0-9]?){0,16}|\d{8,17})(?!\w)";

    protected override bool ValidateMatch(Match match, string originalText)
    {
        var value = match.Value;

        // IBAN validation
        if (Regex.IsMatch(value, @"^[A-Z]{2}\d{2}")) return ValidateIban(value);

        // Generic account number - must be reasonable length
        if (value.All(char.IsDigit))
        {
            // Filter out obvious non-account numbers
            var digitsOnly = value;

            // Too short or too long
            if (digitsOnly.Length < 8 || digitsOnly.Length > 17)
                return false;

            // All same digit is unlikely to be real
            if (digitsOnly.Distinct().Count() < 3)
                return false;

            return true;
        }

        return false;
    }

    /// <summary>
    ///     Validates an IBAN using the mod-97 algorithm.
    /// </summary>
    private static bool ValidateIban(string iban)
    {
        // Remove spaces and convert to uppercase
        iban = iban.Replace(" ", "").ToUpperInvariant();

        if (iban.Length < 15 || iban.Length > 34)
            return false;

        // Move first 4 characters to end
        var rearranged = iban[4..] + iban[..4];

        // Replace letters with numbers (A=10, B=11, etc.)
        var numericString = string.Concat(rearranged.Select(c =>
            char.IsLetter(c) ? (c - 'A' + 10).ToString() : c.ToString()));

        // Perform mod-97 check
        try
        {
            var checksum = 0;
            foreach (var c in numericString) checksum = (checksum * 10 + (c - '0')) % 97;
            return checksum == 1;
        }
        catch
        {
            return false;
        }
    }

    protected override double CalculateConfidence(Match match, string originalText)
    {
        var value = match.Value;

        // IBAN with valid checksum has high confidence
        if (Regex.IsMatch(value, @"^[A-Z]{2}\d{2}") && ValidateIban(value))
            return 0.95;

        // Check context for banking-related keywords
        var contextStart = Math.Max(0, match.Index - 40);
        var contextEnd = Math.Min(originalText.Length, match.Index + match.Length + 40);
        var context = originalText.Substring(contextStart, contextEnd - contextStart).ToLowerInvariant();

        var bankKeywords = new[] { "account", "iban", "bank", "routing", "swift", "bic", "sort code" };
        if (bankKeywords.Any(context.Contains))
            return 0.9;

        return 0.7;
    }
}