namespace Mostlylucid.RagLlmSearch.Models;

/// <summary>
///     Represents a conversation with history
/// </summary>
public class Conversation
{
    /// <summary>
    ///     Unique identifier for this conversation
    /// </summary>
    public string Id { get; set; } = Guid.NewGuid().ToString();

    /// <summary>
    ///     Optional user identifier
    /// </summary>
    public string? UserId { get; set; }

    /// <summary>
    ///     Title or summary of the conversation
    /// </summary>
    public string Title { get; set; } = "New Conversation";

    /// <summary>
    ///     When this conversation was created
    /// </summary>
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    /// <summary>
    ///     When this conversation was last updated
    /// </summary>
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

    /// <summary>
    ///     Messages in this conversation
    /// </summary>
    public List<ChatMessage> Messages { get; set; } = new();

    /// <summary>
    ///     Custom metadata for this conversation
    /// </summary>
    public Dictionary<string, string> Metadata { get; set; } = new();

    /// <summary>
    ///     Whether this conversation is active
    /// </summary>
    public bool IsActive { get; set; } = true;
}

/// <summary>
///     Request to send a chat message
/// </summary>
public class ChatRequest
{
    /// <summary>
    ///     The conversation ID (null for new conversation)
    /// </summary>
    public string? ConversationId { get; set; }

    /// <summary>
    ///     The user's message
    /// </summary>
    public string Message { get; set; } = string.Empty;

    /// <summary>
    ///     Optional user identifier
    /// </summary>
    public string? UserId { get; set; }

    /// <summary>
    ///     Whether to search the web for context
    /// </summary>
    public bool EnableWebSearch { get; set; } = true;

    /// <summary>
    ///     Whether to use RAG context
    /// </summary>
    public bool EnableRag { get; set; } = true;

    /// <summary>
    ///     Override the default search provider
    /// </summary>
    public string? SearchProvider { get; set; }

    /// <summary>
    ///     Whether to stream the response
    /// </summary>
    public bool Stream { get; set; } = true;
}

/// <summary>
///     Response from a chat request
/// </summary>
public class ChatResponse
{
    /// <summary>
    ///     The conversation ID
    /// </summary>
    public string ConversationId { get; set; } = string.Empty;

    /// <summary>
    ///     The assistant's response message
    /// </summary>
    public ChatMessage Message { get; set; } = new();

    /// <summary>
    ///     Whether the response is complete (for streaming)
    /// </summary>
    public bool IsComplete { get; set; }

    /// <summary>
    ///     Sources used in generating this response
    /// </summary>
    public List<SourceReference> Sources { get; set; } = new();

    /// <summary>
    ///     Any error that occurred
    /// </summary>
    public string? Error { get; set; }

    /// <summary>
    ///     Time taken to generate the response in milliseconds
    /// </summary>
    public long ResponseTimeMs { get; set; }
}

/// <summary>
///     Streaming chunk of a chat response
/// </summary>
public class ChatStreamChunk
{
    /// <summary>
    ///     The conversation ID
    /// </summary>
    public string ConversationId { get; set; } = string.Empty;

    /// <summary>
    ///     The message ID
    /// </summary>
    public string MessageId { get; set; } = string.Empty;

    /// <summary>
    ///     The text content of this chunk
    /// </summary>
    public string Content { get; set; } = string.Empty;

    /// <summary>
    ///     Whether this is the final chunk
    /// </summary>
    public bool IsFinal { get; set; }

    /// <summary>
    ///     Sources (included in final chunk)
    /// </summary>
    public List<SourceReference>? Sources { get; set; }

    /// <summary>
    ///     Any error that occurred
    /// </summary>
    public string? Error { get; set; }
}