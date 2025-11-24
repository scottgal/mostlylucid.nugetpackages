using Mostlylucid.LlmI18nAssistant.Models;

namespace Mostlylucid.LlmI18nAssistant.Services;

/// <summary>
///     Service for transforming values while preserving special content
/// </summary>
public interface IValueTransformer
{
    /// <summary>
    ///     Extracts and protects special content from a value before translation
    /// </summary>
    /// <param name="value">The value to process</param>
    /// <param name="options">Translation options</param>
    /// <returns>Tuple of processed value and dictionary of protected tokens</returns>
    (string ProcessedValue, Dictionary<string, string> ProtectedTokens) ExtractProtectedContent(
        string value, TranslationOptions options);

    /// <summary>
    ///     Restores protected content after translation
    /// </summary>
    /// <param name="translatedValue">The translated value</param>
    /// <param name="protectedTokens">Dictionary of protected tokens to restore</param>
    /// <returns>Value with protected content restored</returns>
    string RestoreProtectedContent(string translatedValue, Dictionary<string, string> protectedTokens);
}