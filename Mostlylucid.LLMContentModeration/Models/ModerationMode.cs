namespace Mostlylucid.LLMContentModeration.Models;

/// <summary>
///     Defines how flagged content should be handled
/// </summary>
public enum ModerationMode
{
    /// <summary>
    ///     Only detect and log issues, allow content through
    /// </summary>
    DetectOnly,

    /// <summary>
    ///     Block flagged content with error response
    /// </summary>
    Block,

    /// <summary>
    ///     Mask/redact flagged content (especially PII) and allow through
    /// </summary>
    MaskAndAllow
}

/// <summary>
///     Categories of content that can be flagged
/// </summary>
public enum ContentCategory
{
    /// <summary>
    ///     Toxic, abusive, or hateful content
    /// </summary>
    Toxicity,

    /// <summary>
    ///     Spam or promotional content
    /// </summary>
    Spam,

    /// <summary>
    ///     Content related to self-harm or suicide
    /// </summary>
    SelfHarm,

    /// <summary>
    ///     Not safe for work / adult content
    /// </summary>
    Nsfw,

    /// <summary>
    ///     Personally identifiable information
    /// </summary>
    Pii
}

/// <summary>
///     Types of PII that can be detected
/// </summary>
public enum PiiType
{
    Email,
    Phone,
    Address,
    Iban,
    CreditCard,
    SocialSecurityNumber,
    Other
}