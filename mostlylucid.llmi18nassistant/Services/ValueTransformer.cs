using System.Text.RegularExpressions;
using Microsoft.Extensions.Logging;
using Mostlylucid.LlmI18nAssistant.Models;

namespace Mostlylucid.LlmI18nAssistant.Services;

/// <summary>
///     Service for transforming values while preserving special content
/// </summary>
public partial class ValueTransformer : IValueTransformer
{
    private readonly ILogger<ValueTransformer> _logger;

    public ValueTransformer(ILogger<ValueTransformer> logger)
    {
        _logger = logger;
    }

    /// <inheritdoc />
    public (string ProcessedValue, Dictionary<string, string> ProtectedTokens) ExtractProtectedContent(
        string value, TranslationOptions options)
    {
        var protectedTokens = new Dictionary<string, string>();
        var processedValue = value;
        var tokenIndex = 0;

        // Protect .NET format strings like {0}, {1:N2}, {0:yyyy-MM-dd}
        if (options.PreserveFormatStrings)
            processedValue = ProtectPatterns(
                processedValue,
                FormatStringRegex(),
                protectedTokens,
                ref tokenIndex,
                "FMT");

        // Protect named placeholders like {{name}}, {userName}, ${variable}
        if (options.PreserveFormatStrings)
        {
            processedValue = ProtectPatterns(
                processedValue,
                NamedPlaceholderRegex(),
                protectedTokens,
                ref tokenIndex,
                "PH");

            processedValue = ProtectPatterns(
                processedValue,
                DoubleBracePlaceholderRegex(),
                protectedTokens,
                ref tokenIndex,
                "DBP");
        }

        // Protect HTML tags
        if (options.PreserveHtmlTags)
            processedValue = ProtectPatterns(
                processedValue,
                HtmlTagRegex(),
                protectedTokens,
                ref tokenIndex,
                "HTML");

        if (protectedTokens.Count > 0)
            _logger.LogDebug("Protected {Count} tokens in value", protectedTokens.Count);

        return (processedValue, protectedTokens);
    }

    /// <inheritdoc />
    public string RestoreProtectedContent(string translatedValue, Dictionary<string, string> protectedTokens)
    {
        if (protectedTokens.Count == 0)
            return translatedValue;

        var result = translatedValue;

        foreach (var (placeholder, original) in protectedTokens)
            // Try exact replacement first
            if (result.Contains(placeholder))
            {
                result = result.Replace(placeholder, original);
            }
            else
            {
                // Try case-insensitive replacement
                var index = result.IndexOf(placeholder, StringComparison.OrdinalIgnoreCase);
                if (index >= 0)
                    result = result[..index] + original + result[(index + placeholder.Length)..];
                else
                    // If placeholder is completely missing, try to find a reasonable insertion point
                    _logger.LogWarning("Protected token {Placeholder} not found in translation", placeholder);
            }

        return result;
    }

    private static string ProtectPatterns(
        string input,
        Regex pattern,
        Dictionary<string, string> protectedTokens,
        ref int tokenIndex,
        string prefix)
    {
        // Copy ref parameter to local variable for use in lambda
        var currentIndex = tokenIndex;
        var result = pattern.Replace(input, match =>
        {
            var placeholder = $"[[{prefix}{currentIndex++}]]";
            protectedTokens[placeholder] = match.Value;
            return placeholder;
        });
        // Update the ref parameter with the final value
        tokenIndex = currentIndex;
        return result;
    }

    // Matches .NET format strings like {0}, {1:N2}, {0:yyyy-MM-dd HH:mm}
    [GeneratedRegex(@"\{(\d+)(:[^}]+)?\}")]
    private static partial Regex FormatStringRegex();

    // Matches named placeholders like {userName}, {item.Name}
    [GeneratedRegex(@"\{[a-zA-Z_][a-zA-Z0-9_\.]*\}")]
    private static partial Regex NamedPlaceholderRegex();

    // Matches double-brace placeholders like {{name}}, {{variable}}
    [GeneratedRegex(@"\{\{[^}]+\}\}")]
    private static partial Regex DoubleBracePlaceholderRegex();

    // Matches HTML tags
    [GeneratedRegex(@"<[^>]+>")]
    private static partial Regex HtmlTagRegex();
}