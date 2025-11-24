using Mostlylucid.RagLlmSearch.Models;

namespace Mostlylucid.RagLlmSearch.SearchProviders;

/// <summary>
///     Interface for search providers
/// </summary>
public interface ISearchProvider
{
    /// <summary>
    ///     The name of this search provider
    /// </summary>
    string Name { get; }

    /// <summary>
    ///     Whether this provider is available (API key configured, etc.)
    /// </summary>
    bool IsAvailable { get; }

    /// <summary>
    ///     Performs a search query
    /// </summary>
    /// <param name="query">The search query</param>
    /// <param name="maxResults">Maximum number of results to return</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Search response with results</returns>
    Task<SearchResponse> SearchAsync(string query, int maxResults = 5, CancellationToken cancellationToken = default);
}

/// <summary>
///     Factory for creating and selecting search providers
/// </summary>
public interface ISearchProviderFactory
{
    /// <summary>
    ///     Gets the default search provider
    /// </summary>
    ISearchProvider GetDefaultProvider();

    /// <summary>
    ///     Gets a specific search provider by name
    /// </summary>
    /// <param name="providerName">The provider name (duckduckgo, brave, tavily, serpapi)</param>
    ISearchProvider? GetProvider(string providerName);

    /// <summary>
    ///     Gets all available providers
    /// </summary>
    IEnumerable<ISearchProvider> GetAvailableProviders();
}