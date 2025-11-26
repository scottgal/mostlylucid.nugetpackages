using System;

namespace Mostlylucid.TinyLlm.Chat.Models;

/// <summary>
/// Represents a single chat message in the conversation
/// </summary>
public class ChatMessage
{
    /// <summary>
    /// Unique identifier for the message
    /// </summary>
    public Guid Id { get; init; } = Guid.NewGuid();

    /// <summary>
    /// The content of the message
    /// </summary>
    public string Content { get; init; } = string.Empty;

    /// <summary>
    /// True if this message is from the user, false if from the assistant
    /// </summary>
    public bool IsUser { get; init; }

    /// <summary>
    /// Timestamp when the message was created
    /// </summary>
    public DateTime Timestamp { get; init; } = DateTime.Now;

    /// <summary>
    /// Token count for this message (useful for tracking context usage)
    /// </summary>
    public int TokenCount { get; init; }

    /// <summary>
    /// Gets the CSS class for the message container based on the sender
    /// </summary>
    public string MessageStyleClass => IsUser ? "user-message" : "assistant-message";

    /// <summary>
    /// Gets the CSS class for the message text based on the sender
    /// </summary>
    public string MessageTextStyleClass => IsUser ? "user-message-text" : "assistant-message-text";
}
