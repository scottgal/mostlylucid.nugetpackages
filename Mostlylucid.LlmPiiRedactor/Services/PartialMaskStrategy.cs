using Mostlylucid.LlmPiiRedactor.Models;

namespace Mostlylucid.LlmPiiRedactor.Services;

/// <summary>
/// Redacts by showing partial information while masking the sensitive parts.
/// Examples:
///   Email: "john.doe@example.com" -> "jo****@example.com"
///   Phone: "+1-234-567-8901" -> "****-****-****-8901"
///   Credit Card: "4111-1111-1111-1111" -> "****-****-****-1111"
/// </summary>
public class PartialMaskStrategy : IRedactionStrategy
{
    public RedactionStyle Style => RedactionStyle.PartialMask;

    public string Redact(string originalValue, PiiType piiType, PiiRedactionOptions options)
    {
        if (string.IsNullOrEmpty(originalValue))
            return originalValue;

        return piiType switch
        {
            PiiType.Email => RedactEmail(originalValue, options),
            PiiType.CreditCard => RedactCreditCard(originalValue, options),
            PiiType.PhoneNumber => RedactPhoneNumber(originalValue, options),
            PiiType.Ssn => RedactSsn(originalValue, options),
            PiiType.BankAccount => RedactBankAccount(originalValue, options),
            PiiType.IpAddress => RedactIpAddress(originalValue, options),
            _ => RedactGeneric(originalValue, options)
        };
    }

    private static string RedactEmail(string email, PiiRedactionOptions options)
    {
        var atIndex = email.IndexOf('@');
        if (atIndex <= 0)
            return RedactGeneric(email, options);

        var localPart = email[..atIndex];
        var domain = email[atIndex..];

        var visiblePrefix = Math.Min(options.PartialMaskPrefixLength, localPart.Length);
        var prefix = localPart[..visiblePrefix];
        var masked = new string(options.MaskCharacter, Math.Max(4, localPart.Length - visiblePrefix));

        return $"{prefix}{masked}{domain}";
    }

    private static string RedactCreditCard(string card, PiiRedactionOptions options)
    {
        // Remove separators to get digits only
        var digitsOnly = new string(card.Where(char.IsDigit).ToArray());

        if (digitsOnly.Length < 4)
            return RedactGeneric(card, options);

        // Show last 4 digits
        var lastFour = digitsOnly[^4..];
        var masked = new string(options.MaskCharacter, 4);

        // Reconstruct with original format if it had separators
        if (card.Contains('-'))
            return $"{masked}-{masked}-{masked}-{lastFour}";
        if (card.Contains(' '))
            return $"{masked} {masked} {masked} {lastFour}";

        return $"{new string(options.MaskCharacter, digitsOnly.Length - 4)}{lastFour}";
    }

    private static string RedactPhoneNumber(string phone, PiiRedactionOptions options)
    {
        var digitsOnly = new string(phone.Where(char.IsDigit).ToArray());

        if (digitsOnly.Length < 4)
            return RedactGeneric(phone, options);

        // Show last 4 digits
        var lastFour = digitsOnly[^4..];

        // Reconstruct with formatting hints from original
        if (phone.Contains('-'))
        {
            var parts = phone.Split('-');
            var maskedParts = parts[..^1].Select(p => new string(options.MaskCharacter, p.Length));
            return string.Join("-", maskedParts.Append(lastFour));
        }

        return $"{new string(options.MaskCharacter, digitsOnly.Length - 4)}{lastFour}";
    }

    private static string RedactSsn(string ssn, PiiRedactionOptions options)
    {
        var digitsOnly = new string(ssn.Where(char.IsDigit).ToArray());

        if (digitsOnly.Length != 9)
            return RedactGeneric(ssn, options);

        // Show last 4 digits
        var lastFour = digitsOnly[^4..];

        if (ssn.Contains('-'))
            return $"{new string(options.MaskCharacter, 3)}-{new string(options.MaskCharacter, 2)}-{lastFour}";

        return $"{new string(options.MaskCharacter, 5)}{lastFour}";
    }

    private static string RedactBankAccount(string account, PiiRedactionOptions options)
    {
        // For IBANs, show country code and last 4
        if (account.Length >= 2 && char.IsLetter(account[0]) && char.IsLetter(account[1]))
        {
            var countryCode = account[..2];
            var lastFour = account.Length > 4 ? account[^4..] : "";
            var middleLength = Math.Max(0, account.Length - 6);
            return $"{countryCode}{new string(options.MaskCharacter, middleLength)}{lastFour}";
        }

        return RedactGeneric(account, options);
    }

    private static string RedactIpAddress(string ip, PiiRedactionOptions options)
    {
        // For IPv4, show first octet
        if (ip.Contains('.'))
        {
            var parts = ip.Split('.');
            if (parts.Length == 4)
            {
                return $"{parts[0]}.{new string(options.MaskCharacter, 3)}.{new string(options.MaskCharacter, 3)}.{new string(options.MaskCharacter, 3)}";
            }
        }

        return RedactGeneric(ip, options);
    }

    private static string RedactGeneric(string value, PiiRedactionOptions options)
    {
        if (value.Length <= options.PartialMaskPrefixLength + options.PartialMaskSuffixLength)
            return new string(options.MaskCharacter, value.Length);

        var prefix = value[..options.PartialMaskPrefixLength];
        var suffix = value[^options.PartialMaskSuffixLength..];
        var middleLength = value.Length - options.PartialMaskPrefixLength - options.PartialMaskSuffixLength;

        return $"{prefix}{new string(options.MaskCharacter, middleLength)}{suffix}";
    }
}
