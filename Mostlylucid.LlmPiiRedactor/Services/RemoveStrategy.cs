using Mostlylucid.LlmPiiRedactor.Models;

namespace Mostlylucid.LlmPiiRedactor.Services;

/// <summary>
///     Redacts by completely removing the PII value.
///     Example: "Contact john@example.com for info" -> "Contact  for info"
/// </summary>
public class RemoveStrategy : IRedactionStrategy
{
    public RedactionStyle Style => RedactionStyle.Remove;

    public string Redact(string originalValue, PiiType piiType, PiiRedactionOptions options)
    {
        return string.Empty;
    }
}