namespace Mostlylucid.RagLlmSearch.Configuration;

/// <summary>
///     Main configuration options for RAG LLM Search
/// </summary>
public class RagLlmSearchOptions
{
    /// <summary>
    ///     The Ollama server endpoint URL
    /// </summary>
    public string OllamaEndpoint { get; set; } = "http://localhost:11434";

    /// <summary>
    ///     The chat model to use (e.g., llama3.2, mistral, phi3)
    /// </summary>
    public string ChatModel { get; set; } = "llama3.2";

    /// <summary>
    ///     The embedding model to use for RAG
    /// </summary>
    public string EmbeddingModel { get; set; } = "nomic-embed-text";

    /// <summary>
    ///     SQLite database path for conversation history and RAG storage
    /// </summary>
    public string DatabasePath { get; set; } = "ragllmsearch.db";

    /// <summary>
    ///     Maximum number of search results to retrieve per query
    /// </summary>
    public int MaxSearchResults { get; set; } = 5;

    /// <summary>
    ///     Maximum number of RAG context items to include in prompts
    /// </summary>
    public int MaxRagContextItems { get; set; } = 3;

    /// <summary>
    ///     Enable automatic fact-checking for LLM responses
    /// </summary>
    public bool EnableFactChecking { get; set; } = true;

    /// <summary>
    ///     System prompt for the LLM
    /// </summary>
    public string SystemPrompt { get; set; } = """
                                               You are a helpful AI assistant with access to web search capabilities.
                                               When answering questions:
                                               1. Use the provided search context to give accurate, up-to-date information
                                               2. Always cite your sources when using search results
                                               3. If you're unsure about something, say so
                                               4. Be concise but thorough in your responses
                                               """;

    /// <summary>
    ///     Temperature for LLM responses (0.0 - 1.0)
    /// </summary>
    public float Temperature { get; set; } = 0.7f;

    /// <summary>
    ///     Maximum tokens for LLM responses
    /// </summary>
    public int MaxTokens { get; set; } = 2048;

    /// <summary>
    ///     Cache duration for search results in minutes
    /// </summary>
    public int SearchCacheMinutes { get; set; } = 30;
}