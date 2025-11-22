using System.Security.Cryptography;
using System.Text;
using Mostlylucid.LlmPiiRedactor.Models;

namespace Mostlylucid.LlmPiiRedactor.Services;

/// <summary>
/// Redacts by replacing with a hash of the original value.
/// Provides consistent replacement for the same input while being irreversible.
/// Example: "john@example.com" -> "[EMAIL:a3f2b1c4]"
/// </summary>
public class HashedStrategy : IRedactionStrategy
{
    public RedactionStyle Style => RedactionStyle.Hashed;

    public string Redact(string originalValue, PiiType piiType, PiiRedactionOptions options)
    {
        if (string.IsNullOrEmpty(originalValue))
            return originalValue;

        var hash = ComputeHash(originalValue);
        var typeLabel = GetTypeLabel(piiType);

        return $"[{typeLabel}:{hash}]";
    }

    private static string ComputeHash(string value)
    {
        var bytes = Encoding.UTF8.GetBytes(value.ToUpperInvariant());
        var hashBytes = SHA256.HashData(bytes);

        // Return first 8 hex characters for brevity
        return Convert.ToHexString(hashBytes)[..8].ToLowerInvariant();
    }

    private static string GetTypeLabel(PiiType piiType)
    {
        return piiType switch
        {
            PiiType.Email => "EMAIL",
            PiiType.PhoneNumber => "PHONE",
            PiiType.CreditCard => "CARD",
            PiiType.Ssn => "SSN",
            PiiType.IpAddress => "IP",
            PiiType.Name => "NAME",
            PiiType.Address => "ADDR",
            PiiType.PostCode => "ZIP",
            PiiType.BankAccount => "BANK",
            PiiType.Passport => "PASS",
            PiiType.DriversLicense => "DL",
            PiiType.DateOfBirth => "DOB",
            PiiType.AccountId => "ID",
            PiiType.ApiKey => "KEY",
            PiiType.NationalInsurance => "NI",
            _ => "PII"
        };
    }
}
