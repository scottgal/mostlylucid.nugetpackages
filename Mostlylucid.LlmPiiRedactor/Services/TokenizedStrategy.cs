using System.Collections.Concurrent;
using Mostlylucid.LlmPiiRedactor.Models;

namespace Mostlylucid.LlmPiiRedactor.Services;

/// <summary>
///     Redacts by replacing with tokenized identifiers that remain consistent for the same input.
///     Example: "john@example.com" -> "[EMAIL_001]", same email always gets same token.
///     Useful for debugging while maintaining privacy.
/// </summary>
public class TokenizedStrategy : IRedactionStrategy
{
    private readonly object _counterLock = new();
    private readonly ConcurrentDictionary<(PiiType, string), string> _tokenCache = new();
    private readonly ConcurrentDictionary<PiiType, int> _tokenCounters = new();

    public RedactionStyle Style => RedactionStyle.Tokenized;

    public string Redact(string originalValue, PiiType piiType, PiiRedactionOptions options)
    {
        if (string.IsNullOrEmpty(originalValue))
            return originalValue;

        var key = (piiType, originalValue.ToUpperInvariant());

        return _tokenCache.GetOrAdd(key, _ =>
        {
            var counter = GetNextCounter(piiType);
            var typeLabel = GetTypeLabel(piiType);
            return $"[{typeLabel}_{counter:D3}]";
        });
    }

    private int GetNextCounter(PiiType piiType)
    {
        lock (_counterLock)
        {
            if (!_tokenCounters.TryGetValue(piiType, out var current)) current = 0;

            _tokenCounters[piiType] = current + 1;
            return current + 1;
        }
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
            PiiType.Passport => "PASSPORT",
            PiiType.DriversLicense => "LICENSE",
            PiiType.DateOfBirth => "DOB",
            PiiType.AccountId => "ID",
            PiiType.ApiKey => "KEY",
            PiiType.NationalInsurance => "NI",
            _ => "PII"
        };
    }

    /// <summary>
    ///     Clears the token cache. Useful for testing or when starting a new session.
    /// </summary>
    public void ClearCache()
    {
        _tokenCache.Clear();
        _tokenCounters.Clear();
    }

    /// <summary>
    ///     Gets the current token count for a PII type.
    /// </summary>
    public int GetTokenCount(PiiType piiType)
    {
        return _tokenCounters.GetValueOrDefault(piiType, 0);
    }
}