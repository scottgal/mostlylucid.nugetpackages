using System.Text.RegularExpressions;
using Microsoft.Extensions.Logging;
using Mostlylucid.LLMContentModeration.Models;

namespace Mostlylucid.LLMContentModeration.Services;

/// <summary>
///     Regex-based PII detector with support for common PII patterns
/// </summary>
public partial class PiiDetector : IPiiDetector
{
    private readonly ILogger<PiiDetector> _logger;

    public PiiDetector(ILogger<PiiDetector> logger)
    {
        _logger = logger;
    }

    public List<PiiMatch> DetectPii(string content, PiiDetectionOptions options)
    {
        var matches = new List<PiiMatch>();

        if (string.IsNullOrWhiteSpace(content))
            return matches;

        if (options.DetectEmail)
            matches.AddRange(FindMatches(content, EmailRegex(), PiiType.Email));

        if (options.DetectPhone)
            matches.AddRange(FindMatches(content, PhoneRegex(), PiiType.Phone));

        if (options.DetectIban)
            matches.AddRange(FindMatches(content, IbanRegex(), PiiType.Iban));

        if (options.DetectCreditCard)
            matches.AddRange(FindMatches(content, CreditCardRegex(), PiiType.CreditCard));

        if (options.DetectSocialSecurityNumber)
            matches.AddRange(FindMatches(content, SsnRegex(), PiiType.SocialSecurityNumber));

        if (options.DetectAddress)
            matches.AddRange(FindMatches(content, AddressRegex(), PiiType.Address));

        // Sort by position and remove overlapping matches
        matches = matches
            .OrderBy(m => m.StartIndex)
            .ToList();

        matches = RemoveOverlappingMatches(matches);

        _logger.LogDebug("Found {Count} PII matches in content", matches.Count);

        return matches;
    }

    public string MaskPii(string content, List<PiiMatch> matches, PiiDetectionOptions options)
    {
        if (!matches.Any() || string.IsNullOrWhiteSpace(content))
            return content;

        // Sort matches by position in reverse order to avoid index shifting
        var sortedMatches = matches
            .Where(m => m.StartIndex >= 0 && m.EndIndex <= content.Length)
            .OrderByDescending(m => m.StartIndex)
            .ToList();

        foreach (var match in sortedMatches)
        {
            var masked = CreateMaskedValue(match.OriginalValue, options);
            match.MaskedValue = masked;

            if (match.StartIndex >= 0 && match.EndIndex <= content.Length)
            {
                content = content.Remove(match.StartIndex, match.EndIndex - match.StartIndex);
                content = content.Insert(match.StartIndex, masked);
            }
        }

        return content;
    }

    private static List<PiiMatch> FindMatches(string content, Regex regex, PiiType type)
    {
        var matches = new List<PiiMatch>();
        var regexMatches = regex.Matches(content);

        foreach (Match match in regexMatches)
            matches.Add(new PiiMatch
            {
                Type = type,
                OriginalValue = match.Value,
                StartIndex = match.Index,
                EndIndex = match.Index + match.Length,
                Confidence = 1.0f
            });

        return matches;
    }

    private static List<PiiMatch> RemoveOverlappingMatches(List<PiiMatch> matches)
    {
        var result = new List<PiiMatch>();
        var lastEnd = -1;

        foreach (var match in matches)
            if (match.StartIndex >= lastEnd)
            {
                result.Add(match);
                lastEnd = match.EndIndex;
            }

        return result;
    }

    private static string CreateMaskedValue(string value, PiiDetectionOptions options)
    {
        if (string.IsNullOrEmpty(value))
            return value;

        var unmasked = options.UnmaskedChars;
        var maskChar = options.MaskCharacter;

        if (value.Length <= unmasked * 2)
            return new string(maskChar, value.Length);

        var start = value[..unmasked];
        var end = value[^unmasked..];
        var middle = new string(maskChar, value.Length - unmasked * 2);

        return $"{start}{middle}{end}";
    }

    #region Regex Patterns

    // Email pattern
    [GeneratedRegex(@"[a-zA-Z0-9._%+-]+@[a-zA-Z0-9.-]+\.[a-zA-Z]{2,}", RegexOptions.Compiled)]
    private static partial Regex EmailRegex();

    // Phone patterns (international formats)
    [GeneratedRegex(@"(?:\+\d{1,3}[-.\s]?)?\(?\d{1,4}\)?[-.\s]?\d{1,4}[-.\s]?\d{1,9}", RegexOptions.Compiled)]
    private static partial Regex PhoneRegex();

    // IBAN pattern (2 letter country code + 2 check digits + up to 30 alphanumeric)
    [GeneratedRegex(@"\b[A-Z]{2}\d{2}[A-Z0-9]{4,30}\b", RegexOptions.Compiled | RegexOptions.IgnoreCase)]
    private static partial Regex IbanRegex();

    // Credit card patterns (major card formats)
    [GeneratedRegex(
        @"\b(?:4[0-9]{12}(?:[0-9]{3})?|5[1-5][0-9]{14}|3[47][0-9]{13}|6(?:011|5[0-9]{2})[0-9]{12}|(?:2131|1800|35\d{3})\d{11})\b",
        RegexOptions.Compiled)]
    private static partial Regex CreditCardRegex();

    // US Social Security Number
    [GeneratedRegex(@"\b\d{3}-\d{2}-\d{4}\b", RegexOptions.Compiled)]
    private static partial Regex SsnRegex();

    // Simple address pattern (number + street name + common suffixes)
    [GeneratedRegex(
        @"\b\d{1,5}\s+[\w\s]{1,50}\s+(?:Street|St|Avenue|Ave|Road|Rd|Boulevard|Blvd|Lane|Ln|Drive|Dr|Court|Ct|Way|Place|Pl)\b",
        RegexOptions.Compiled | RegexOptions.IgnoreCase)]
    private static partial Regex AddressRegex();

    #endregion
}