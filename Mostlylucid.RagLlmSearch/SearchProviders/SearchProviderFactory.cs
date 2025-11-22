using Microsoft.Extensions.Options;
using Mostlylucid.RagLlmSearch.Configuration;

namespace Mostlylucid.RagLlmSearch.SearchProviders;

/// <summary>
/// Factory for creating and selecting search providers
/// </summary>
public class SearchProviderFactory : ISearchProviderFactory
{
    private readonly IEnumerable<ISearchProvider> _providers;
    private readonly SearchProviderOptions _options;

    public SearchProviderFactory(
        IEnumerable<ISearchProvider> providers,
        IOptions<SearchProviderOptions> options)
    {
        _providers = providers;
        _options = options.Value;
    }

    /// <summary>
    /// Gets the default search provider based on configuration
    /// Falls back to DuckDuckGo if configured provider is unavailable
    /// </summary>
    public ISearchProvider GetDefaultProvider()
    {
        var providerName = _options.DefaultProvider switch
        {
            SearchProviderType.DuckDuckGo => "DuckDuckGo",
            SearchProviderType.Brave => "Brave",
            SearchProviderType.Tavily => "Tavily",
            SearchProviderType.SerpApi => "SerpApi",
            _ => "DuckDuckGo"
        };

        var provider = _providers.FirstOrDefault(p =>
            p.Name.Equals(providerName, StringComparison.OrdinalIgnoreCase) && p.IsAvailable);

        // Fall back to DuckDuckGo if configured provider is unavailable
        if (provider == null)
        {
            provider = _providers.FirstOrDefault(p =>
                p.Name.Equals("DuckDuckGo", StringComparison.OrdinalIgnoreCase));
        }

        return provider ?? throw new InvalidOperationException("No search provider available");
    }

    /// <summary>
    /// Gets a specific search provider by name
    /// </summary>
    public ISearchProvider? GetProvider(string providerName)
    {
        return _providers.FirstOrDefault(p =>
            p.Name.Equals(providerName, StringComparison.OrdinalIgnoreCase) && p.IsAvailable);
    }

    /// <summary>
    /// Gets all available (configured) providers
    /// </summary>
    public IEnumerable<ISearchProvider> GetAvailableProviders()
    {
        return _providers.Where(p => p.IsAvailable);
    }
}
