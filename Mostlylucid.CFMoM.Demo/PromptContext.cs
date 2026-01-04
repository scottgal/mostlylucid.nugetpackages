namespace Mostlylucid.CFMoM.Demo;

/// <summary>
///     The context for the prompt router demo.
///     Contains the user prompt and optional metadata.
/// </summary>
public sealed record PromptContext
{
    /// <summary>
    ///     The user's prompt text.
    /// </summary>
    public required string Prompt { get; init; }

    /// <summary>
    ///     Optional user ID for personalization.
    /// </summary>
    public string? UserId { get; init; }

    /// <summary>
    ///     Optional session ID for conversation tracking.
    /// </summary>
    public string? SessionId { get; init; }

    /// <summary>
    ///     Embedding of the prompt (populated during processing).
    /// </summary>
    public float[]? Embedding { get; init; }

    /// <summary>
    ///     Timestamp of when this prompt was received.
    /// </summary>
    public DateTimeOffset ReceivedAt { get; init; } = DateTimeOffset.UtcNow;
}
