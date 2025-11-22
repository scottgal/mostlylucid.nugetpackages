namespace Mostlylucid.RagLlmSearch.Models;

/// <summary>
/// Represents a document stored in the RAG system
/// </summary>
public class RagDocument
{
    /// <summary>
    /// Unique identifier for this document
    /// </summary>
    public string Id { get; set; } = Guid.NewGuid().ToString();

    /// <summary>
    /// The text content of the document
    /// </summary>
    public string Content { get; set; } = string.Empty;

    /// <summary>
    /// Title or summary of the document
    /// </summary>
    public string Title { get; set; } = string.Empty;

    /// <summary>
    /// Source URL if from web search
    /// </summary>
    public string? SourceUrl { get; set; }

    /// <summary>
    /// The type of document (search_result, user_input, fact, etc.)
    /// </summary>
    public string DocumentType { get; set; } = "general";

    /// <summary>
    /// When this document was added
    /// </summary>
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    /// <summary>
    /// When this document was last accessed
    /// </summary>
    public DateTime LastAccessedAt { get; set; } = DateTime.UtcNow;

    /// <summary>
    /// Number of times this document was retrieved
    /// </summary>
    public int AccessCount { get; set; }

    /// <summary>
    /// Custom metadata for this document
    /// </summary>
    public Dictionary<string, string> Metadata { get; set; } = new();

    /// <summary>
    /// The embedding vector (populated when stored)
    /// </summary>
    public float[]? Embedding { get; set; }
}

/// <summary>
/// Result from a RAG similarity search
/// </summary>
public class RagSearchResult
{
    /// <summary>
    /// The document that matched
    /// </summary>
    public RagDocument Document { get; set; } = new();

    /// <summary>
    /// Similarity score (0.0 - 1.0)
    /// </summary>
    public float Score { get; set; }
}
