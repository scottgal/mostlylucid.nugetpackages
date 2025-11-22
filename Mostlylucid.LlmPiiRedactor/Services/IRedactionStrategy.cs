using Mostlylucid.LlmPiiRedactor.Models;

namespace Mostlylucid.LlmPiiRedactor.Services;

/// <summary>
/// Interface for PII redaction strategies.
/// </summary>
public interface IRedactionStrategy
{
    /// <summary>
    /// The style this strategy implements.
    /// </summary>
    RedactionStyle Style { get; }

    /// <summary>
    /// Redact a PII value.
    /// </summary>
    /// <param name="originalValue">The original PII value.</param>
    /// <param name="piiType">The type of PII.</param>
    /// <param name="options">Redaction options.</param>
    /// <returns>The redacted value.</returns>
    string Redact(string originalValue, PiiType piiType, PiiRedactionOptions options);
}
