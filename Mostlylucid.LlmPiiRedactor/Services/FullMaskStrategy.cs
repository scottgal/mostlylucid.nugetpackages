using Mostlylucid.LlmPiiRedactor.Models;

namespace Mostlylucid.LlmPiiRedactor.Services;

/// <summary>
/// Redacts by replacing the entire value with mask characters.
/// Example: "john@example.com" -> "****************"
/// </summary>
public class FullMaskStrategy : IRedactionStrategy
{
    public RedactionStyle Style => RedactionStyle.FullMask;

    public string Redact(string originalValue, PiiType piiType, PiiRedactionOptions options)
    {
        if (string.IsNullOrEmpty(originalValue))
            return originalValue;

        return new string(options.MaskCharacter, originalValue.Length);
    }
}
