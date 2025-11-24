using System.Text.RegularExpressions;
using Mostlylucid.LlmPiiRedactor.Models;

namespace Mostlylucid.LlmPiiRedactor.Detectors;

/// <summary>
///     Detects credit card numbers with Luhn algorithm validation.
/// </summary>
public class CreditCardDetector : BasePiiDetector
{
    public override PiiType PiiType => PiiType.CreditCard;
    public override string Name => "CreditCardDetector";
    public override int Priority => 5; // High priority for security

    // Matches credit card formats with optional separators
    protected override string Pattern =>
        @"(?<!\d)(?:4[0-9]{3}|5[1-5][0-9]{2}|3[47][0-9]{2}|6(?:011|5[0-9]{2})|(?:2131|1800|35\d{2}))[- ]?[0-9]{4}[- ]?[0-9]{4}[- ]?[0-9]{1,4}(?!\d)";

    protected override bool ValidateMatch(Match match, string originalText)
    {
        var cardNumber = match.Value;
        var digitsOnly = new string(cardNumber.Where(char.IsDigit).ToArray());

        // Credit cards are 13-19 digits
        if (digitsOnly.Length < 13 || digitsOnly.Length > 19)
            return false;

        // Validate with Luhn algorithm
        return ValidateLuhn(digitsOnly);
    }

    /// <summary>
    ///     Validates a number using the Luhn algorithm (mod 10).
    /// </summary>
    private static bool ValidateLuhn(string number)
    {
        var sum = 0;
        var alternate = false;

        for (var i = number.Length - 1; i >= 0; i--)
        {
            var digit = number[i] - '0';

            if (alternate)
            {
                digit *= 2;
                if (digit > 9)
                    digit -= 9;
            }

            sum += digit;
            alternate = !alternate;
        }

        return sum % 10 == 0;
    }

    protected override double CalculateConfidence(Match match, string originalText)
    {
        var cardNumber = match.Value;
        var digitsOnly = new string(cardNumber.Where(char.IsDigit).ToArray());

        // Base confidence from Luhn validation
        var confidence = 0.95;

        // Identify card type for higher confidence
        var cardType = GetCardType(digitsOnly);
        if (!string.IsNullOrEmpty(cardType))
            confidence = 1.0;

        return confidence;
    }

    /// <summary>
    ///     Gets the card type based on the number prefix.
    /// </summary>
    private static string? GetCardType(string number)
    {
        if (number.StartsWith('4'))
            return "Visa";
        if (number.StartsWith("51") || number.StartsWith("52") || number.StartsWith("53") ||
            number.StartsWith("54") || number.StartsWith("55"))
            return "MasterCard";
        if (number.StartsWith("34") || number.StartsWith("37"))
            return "American Express";
        if (number.StartsWith("6011") || number.StartsWith("65"))
            return "Discover";
        if (number.StartsWith("35"))
            return "JCB";

        return null;
    }
}