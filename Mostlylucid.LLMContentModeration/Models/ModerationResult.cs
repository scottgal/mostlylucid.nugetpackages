namespace Mostlylucid.LLMContentModeration.Models;

/// <summary>
///     Result of content moderation analysis
/// </summary>
public class ModerationResult
{
    /// <summary>
    ///     Unique identifier for this moderation result
    /// </summary>
    public string Id { get; set; } = Guid.NewGuid().ToString("N");

    /// <summary>
    ///     Timestamp of moderation
    /// </summary>
    public DateTimeOffset Timestamp { get; set; } = DateTimeOffset.UtcNow;

    /// <summary>
    ///     The original content that was moderated
    /// </summary>
    public string OriginalContent { get; set; } = string.Empty;

    /// <summary>
    ///     The content after any masking/redaction (if MaskAndAllow mode)
    /// </summary>
    public string? ModeratedContent { get; set; }

    /// <summary>
    ///     Whether any content was flagged
    /// </summary>
    public bool IsFlagged => Flags.Count > 0 || PiiMatches.Count > 0;

    /// <summary>
    ///     Whether the content should be blocked (based on mode and flags)
    /// </summary>
    public bool IsBlocked { get; set; }

    /// <summary>
    ///     The moderation mode that was applied
    /// </summary>
    public ModerationMode Mode { get; set; }

    /// <summary>
    ///     Content classification flags
    /// </summary>
    public List<ContentFlag> Flags { get; set; } = [];

    /// <summary>
    ///     PII matches found in content
    /// </summary>
    public List<PiiMatch> PiiMatches { get; set; } = [];

    /// <summary>
    ///     Processing time in milliseconds
    /// </summary>
    public long ProcessingTimeMs { get; set; }

    /// <summary>
    ///     Any errors that occurred during moderation
    /// </summary>
    public List<string> Errors { get; set; } = [];

    /// <summary>
    ///     Whether moderation completed successfully
    /// </summary>
    public bool Success => Errors.Count == 0;

    /// <summary>
    ///     Summary of why content was flagged
    /// </summary>
    public string? Summary => IsFlagged
        ? $"Flagged for: {string.Join(", ", Flags.Select(f => f.Category.ToString()).Concat(PiiMatches.Any() ? ["PII"] : []))}"
        : null;
}

/// <summary>
///     Represents a content classification flag
/// </summary>
public class ContentFlag
{
    /// <summary>
    ///     Category of flagged content
    /// </summary>
    public ContentCategory Category { get; set; }

    /// <summary>
    ///     Confidence score (0.0 - 1.0)
    /// </summary>
    public float Confidence { get; set; }

    /// <summary>
    ///     Threshold that was exceeded
    /// </summary>
    public float Threshold { get; set; }

    /// <summary>
    ///     Additional explanation from LLM
    /// </summary>
    public string? Explanation { get; set; }

    /// <summary>
    ///     Specific text segment that triggered the flag
    /// </summary>
    public string? TriggerText { get; set; }
}

/// <summary>
///     Represents a PII match in content
/// </summary>
public class PiiMatch
{
    /// <summary>
    ///     Type of PII detected
    /// </summary>
    public PiiType Type { get; set; }

    /// <summary>
    ///     The original value found
    /// </summary>
    public string OriginalValue { get; set; } = string.Empty;

    /// <summary>
    ///     The masked value (if masking was applied)
    /// </summary>
    public string? MaskedValue { get; set; }

    /// <summary>
    ///     Start position in the original content
    /// </summary>
    public int StartIndex { get; set; }

    /// <summary>
    ///     End position in the original content
    /// </summary>
    public int EndIndex { get; set; }

    /// <summary>
    ///     Confidence of the detection (for LLM-enhanced detection)
    /// </summary>
    public float Confidence { get; set; } = 1.0f;
}

/// <summary>
///     Response DTO for blocked content
/// </summary>
public class ModerationBlockedResponse
{
    public string Error { get; set; } = "Content Blocked";
    public string Message { get; set; } = "Your content was blocked due to policy violations.";
    public string[] Reasons { get; set; } = [];
    public string? ModerationId { get; set; }
}