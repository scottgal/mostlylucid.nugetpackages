using Mostlylucid.LlmPiiRedactor.Models;

namespace Mostlylucid.LlmPiiRedactor.Services;

/// <summary>
///     Redacts by replacing with a simple type label.
///     Example: "john@example.com" -> "[EMAIL]"
/// </summary>
public class TypeLabelStrategy : IRedactionStrategy
{
    public RedactionStyle Style => RedactionStyle.TypeLabel;

    public string Redact(string originalValue, PiiType piiType, PiiRedactionOptions options)
    {
        if (string.IsNullOrEmpty(originalValue))
            return originalValue;

        return $"[{GetTypeLabel(piiType)}]";
    }

    private static string GetTypeLabel(PiiType piiType)
    {
        return piiType switch
        {
            PiiType.Email => "EMAIL",
            PiiType.PhoneNumber => "PHONE",
            PiiType.CreditCard => "CREDIT_CARD",
            PiiType.Ssn => "SSN",
            PiiType.IpAddress => "IP_ADDRESS",
            PiiType.Name => "NAME",
            PiiType.Address => "ADDRESS",
            PiiType.PostCode => "POSTCODE",
            PiiType.BankAccount => "BANK_ACCOUNT",
            PiiType.Passport => "PASSPORT",
            PiiType.DriversLicense => "DRIVERS_LICENSE",
            PiiType.DateOfBirth => "DOB",
            PiiType.AccountId => "ACCOUNT_ID",
            PiiType.ApiKey => "API_KEY",
            PiiType.NationalInsurance => "NI_NUMBER",
            _ => "REDACTED"
        };
    }
}