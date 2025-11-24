using Mostlylucid.LLMContentModeration.Models;

namespace Mostlylucid.LLMContentModeration.Attributes;

/// <summary>
///     Attribute to configure content moderation policy per controller/action
/// </summary>
[AttributeUsage(AttributeTargets.Class | AttributeTargets.Method)]
public class ModerationPolicyAttribute : Attribute
{
    /// <summary>
    ///     Create a moderation policy with specified mode
    /// </summary>
    /// <param name="mode">Moderation mode to apply</param>
    public ModerationPolicyAttribute(ModerationMode mode)
    {
        Mode = mode;
    }

    /// <summary>
    ///     Create a moderation policy with default mode from configuration
    /// </summary>
    public ModerationPolicyAttribute()
    {
    }

    /// <summary>
    ///     Moderation mode for this endpoint
    /// </summary>
    public ModerationMode? Mode { get; set; }

    /// <summary>
    ///     Enable/disable toxicity detection
    /// </summary>
    public bool EnableToxicity { get; set; } = true;

    /// <summary>
    ///     Enable/disable spam detection
    /// </summary>
    public bool EnableSpam { get; set; } = true;

    /// <summary>
    ///     Enable/disable self-harm detection
    /// </summary>
    public bool EnableSelfHarm { get; set; } = true;

    /// <summary>
    ///     Enable/disable NSFW detection
    /// </summary>
    public bool EnableNsfw { get; set; } = true;

    /// <summary>
    ///     Enable/disable PII detection
    /// </summary>
    public bool EnablePii { get; set; } = true;

    /// <summary>
    ///     Skip moderation for this endpoint
    /// </summary>
    public bool Skip { get; set; }

    /// <summary>
    ///     Custom JSON property path to extract content from (for JSON bodies)
    /// </summary>
    public string? ContentJsonPath { get; set; }

    /// <summary>
    ///     Creates moderation options based on this attribute's settings
    /// </summary>
    internal ModerationOptions ToOptions(ModerationOptions baseOptions)
    {
        return new ModerationOptions
        {
            Enabled = !Skip,
            DefaultMode = Mode ?? baseOptions.DefaultMode,
            Ollama = baseOptions.Ollama,
            PiiDetection = new PiiDetectionOptions
            {
                Enabled = EnablePii,
                DetectEmail = baseOptions.PiiDetection.DetectEmail,
                DetectPhone = baseOptions.PiiDetection.DetectPhone,
                DetectAddress = baseOptions.PiiDetection.DetectAddress,
                DetectIban = baseOptions.PiiDetection.DetectIban,
                DetectCreditCard = baseOptions.PiiDetection.DetectCreditCard,
                DetectSocialSecurityNumber = baseOptions.PiiDetection.DetectSocialSecurityNumber,
                UseLlmEnhancement = baseOptions.PiiDetection.UseLlmEnhancement,
                MaskCharacter = baseOptions.PiiDetection.MaskCharacter,
                UnmaskedChars = baseOptions.PiiDetection.UnmaskedChars
            },
            ContentClassification = new ContentClassificationOptions
            {
                EnableToxicity = EnableToxicity,
                EnableSpam = EnableSpam,
                EnableSelfHarm = EnableSelfHarm,
                EnableNsfw = EnableNsfw,
                ToxicityThreshold = baseOptions.ContentClassification.ToxicityThreshold,
                SpamThreshold = baseOptions.ContentClassification.SpamThreshold,
                SelfHarmThreshold = baseOptions.ContentClassification.SelfHarmThreshold,
                NsfwThreshold = baseOptions.ContentClassification.NsfwThreshold
            },
            MaxContentLength = baseOptions.MaxContentLength,
            OnContentBlocked = baseOptions.OnContentBlocked,
            OnContentFlagged = baseOptions.OnContentFlagged
        };
    }
}

/// <summary>
///     Attribute to skip moderation for a controller/action
/// </summary>
[AttributeUsage(AttributeTargets.Class | AttributeTargets.Method)]
public class SkipModerationAttribute : Attribute
{
}