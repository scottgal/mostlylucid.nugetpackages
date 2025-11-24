namespace Mostlylucid.LlmPiiRedactor.Models;

/// <summary>
///     Defines how PII should be redacted in the output.
/// </summary>
public enum RedactionStyle
{
    /// <summary>
    ///     Full mask replacement (e.g., "********")
    /// </summary>
    FullMask,

    /// <summary>
    ///     Partial mask showing some characters (e.g., "jo****@gmail.com", "****-****-****-1234")
    /// </summary>
    PartialMask,

    /// <summary>
    ///     Replace with a consistent tokenized ID (e.g., "[EMAIL_001]", "[PHONE_002]")
    ///     Useful for debugging while maintaining privacy
    /// </summary>
    Tokenized,

    /// <summary>
    ///     Replace with the PII type label (e.g., "[EMAIL]", "[PHONE]")
    /// </summary>
    TypeLabel,

    /// <summary>
    ///     Hash the value for consistent but irreversible replacement
    /// </summary>
    Hashed,

    /// <summary>
    ///     Complete removal of the PII (empty string)
    /// </summary>
    Remove
}