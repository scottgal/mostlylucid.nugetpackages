namespace Mostlylucid.RagLlmSearch.Models;

/// <summary>
/// Represents a chat message in a conversation
/// </summary>
public class ChatMessage
{
    /// <summary>
    /// Unique identifier for this message
    /// </summary>
    public string Id { get; set; } = Guid.NewGuid().ToString();

    /// <summary>
    /// The conversation this message belongs to
    /// </summary>
    public string ConversationId { get; set; } = string.Empty;

    /// <summary>
    /// Role of the message sender (user, assistant, system)
    /// </summary>
    public ChatRole Role { get; set; }

    /// <summary>
    /// The message content
    /// </summary>
    public string Content { get; set; } = string.Empty;

    /// <summary>
    /// When this message was created
    /// </summary>
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    /// <summary>
    /// Sources used to generate this message (for assistant messages)
    /// </summary>
    public List<SourceReference> Sources { get; set; } = new();

    /// <summary>
    /// Token count for this message
    /// </summary>
    public int? TokenCount { get; set; }

    /// <summary>
    /// Whether this message used RAG context
    /// </summary>
    public bool UsedRagContext { get; set; }

    /// <summary>
    /// Whether this message triggered a web search
    /// </summary>
    public bool TriggeredSearch { get; set; }
}

/// <summary>
/// Role of a chat message sender
/// </summary>
public enum ChatRole
{
    /// <summary>
    /// System message (instructions/context)
    /// </summary>
    System,

    /// <summary>
    /// User message
    /// </summary>
    User,

    /// <summary>
    /// Assistant response
    /// </summary>
    Assistant
}

/// <summary>
/// Reference to a source used in generating a response
/// </summary>
public class SourceReference
{
    /// <summary>
    /// Title of the source
    /// </summary>
    public string Title { get; set; } = string.Empty;

    /// <summary>
    /// URL of the source
    /// </summary>
    public string Url { get; set; } = string.Empty;

    /// <summary>
    /// Snippet from the source that was used
    /// </summary>
    public string Snippet { get; set; } = string.Empty;

    /// <summary>
    /// Relevance score to the query
    /// </summary>
    public float? RelevanceScore { get; set; }
}
