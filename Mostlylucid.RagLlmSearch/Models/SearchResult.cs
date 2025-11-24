namespace Mostlylucid.RagLlmSearch.Models;

/// <summary>
///     Represents a search result from any search provider
/// </summary>
public class SearchResult
{
    /// <summary>
    ///     Unique identifier for this search result
    /// </summary>
    public string Id { get; set; } = Guid.NewGuid().ToString();

    /// <summary>
    ///     Title of the search result
    /// </summary>
    public string Title { get; set; } = string.Empty;

    /// <summary>
    ///     URL of the source
    /// </summary>
    public string Url { get; set; } = string.Empty;

    /// <summary>
    ///     Snippet or description from the search result
    /// </summary>
    public string Snippet { get; set; } = string.Empty;

    /// <summary>
    ///     Full content if available
    /// </summary>
    public string? Content { get; set; }

    /// <summary>
    ///     The search provider that returned this result
    /// </summary>
    public string Provider { get; set; } = string.Empty;

    /// <summary>
    ///     When this result was retrieved
    /// </summary>
    public DateTime RetrievedAt { get; set; } = DateTime.UtcNow;

    /// <summary>
    ///     Relevance score (0.0 - 1.0) if provided by the search engine
    /// </summary>
    public float? Score { get; set; }

    /// <summary>
    ///     Additional metadata from the search provider
    /// </summary>
    public Dictionary<string, string> Metadata { get; set; } = new();
}

/// <summary>
///     Represents a collection of search results
/// </summary>
public class SearchResponse
{
    /// <summary>
    ///     The original search query
    /// </summary>
    public string Query { get; set; } = string.Empty;

    /// <summary>
    ///     List of search results
    /// </summary>
    public List<SearchResult> Results { get; set; } = new();

    /// <summary>
    ///     Total number of results found
    /// </summary>
    public int TotalResults { get; set; }

    /// <summary>
    ///     The provider used for this search
    /// </summary>
    public string Provider { get; set; } = string.Empty;

    /// <summary>
    ///     Time taken to perform the search in milliseconds
    /// </summary>
    public long SearchTimeMs { get; set; }

    /// <summary>
    ///     Any error that occurred during the search
    /// </summary>
    public string? Error { get; set; }

    /// <summary>
    ///     Whether the search was successful
    /// </summary>
    public bool Success => string.IsNullOrEmpty(Error) && Results.Count > 0;
}