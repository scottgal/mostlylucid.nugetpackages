namespace Mostlylucid.LlmPiiRedactor.Models;

/// <summary>
/// Types of Personally Identifiable Information that can be detected and redacted.
/// </summary>
[Flags]
public enum PiiType
{
    None = 0,

    /// <summary>
    /// Email addresses (e.g., john.doe@example.com)
    /// </summary>
    Email = 1 << 0,

    /// <summary>
    /// Phone numbers in various formats
    /// </summary>
    PhoneNumber = 1 << 1,

    /// <summary>
    /// Credit card numbers (Visa, MasterCard, Amex, etc.)
    /// </summary>
    CreditCard = 1 << 2,

    /// <summary>
    /// Social Security Numbers (US format)
    /// </summary>
    Ssn = 1 << 3,

    /// <summary>
    /// IP addresses (IPv4 and IPv6)
    /// </summary>
    IpAddress = 1 << 4,

    /// <summary>
    /// Personal names (first names, last names)
    /// </summary>
    Name = 1 << 5,

    /// <summary>
    /// Street addresses
    /// </summary>
    Address = 1 << 6,

    /// <summary>
    /// Postal/ZIP codes
    /// </summary>
    PostCode = 1 << 7,

    /// <summary>
    /// Bank account numbers (IBAN, account numbers)
    /// </summary>
    BankAccount = 1 << 8,

    /// <summary>
    /// Passport numbers
    /// </summary>
    Passport = 1 << 9,

    /// <summary>
    /// Driver's license numbers
    /// </summary>
    DriversLicense = 1 << 10,

    /// <summary>
    /// Date of birth
    /// </summary>
    DateOfBirth = 1 << 11,

    /// <summary>
    /// Generic account/user IDs
    /// </summary>
    AccountId = 1 << 12,

    /// <summary>
    /// API keys and tokens
    /// </summary>
    ApiKey = 1 << 13,

    /// <summary>
    /// UK National Insurance Numbers
    /// </summary>
    NationalInsurance = 1 << 14,

    /// <summary>
    /// All PII types
    /// </summary>
    All = Email | PhoneNumber | CreditCard | Ssn | IpAddress | Name | Address |
          PostCode | BankAccount | Passport | DriversLicense | DateOfBirth |
          AccountId | ApiKey | NationalInsurance
}
