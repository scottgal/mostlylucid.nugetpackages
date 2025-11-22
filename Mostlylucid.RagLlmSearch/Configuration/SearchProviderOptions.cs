namespace Mostlylucid.RagLlmSearch.Configuration;

/// <summary>
/// Configuration for search providers
/// </summary>
public class SearchProviderOptions
{
    /// <summary>
    /// The default search provider to use
    /// </summary>
    public SearchProviderType DefaultProvider { get; set; } = SearchProviderType.DuckDuckGo;

    /// <summary>
    /// DuckDuckGo specific settings
    /// </summary>
    public DuckDuckGoSettings DuckDuckGo { get; set; } = new();

    /// <summary>
    /// Brave Search API settings
    /// </summary>
    public BraveSearchSettings Brave { get; set; } = new();

    /// <summary>
    /// Tavily API settings
    /// </summary>
    public TavilySettings Tavily { get; set; } = new();

    /// <summary>
    /// SerpApi settings
    /// </summary>
    public SerpApiSettings SerpApi { get; set; } = new();
}

/// <summary>
/// Available search provider types
/// </summary>
public enum SearchProviderType
{
    /// <summary>
    /// DuckDuckGo Instant Answer API (free, no API key)
    /// </summary>
    DuckDuckGo,

    /// <summary>
    /// Brave Search API (free tier: 2000 queries/month)
    /// </summary>
    Brave,

    /// <summary>
    /// Tavily AI Search API (free tier available)
    /// </summary>
    Tavily,

    /// <summary>
    /// SerpApi (free tier: 100 searches/month)
    /// </summary>
    SerpApi
}

/// <summary>
/// DuckDuckGo Instant Answer API settings
/// </summary>
public class DuckDuckGoSettings
{
    /// <summary>
    /// Whether to enable safe search
    /// </summary>
    public bool SafeSearch { get; set; } = true;

    /// <summary>
    /// Region for search results (e.g., us-en, uk-en)
    /// </summary>
    public string Region { get; set; } = "us-en";
}

/// <summary>
/// Brave Search API settings (free tier: 2000 queries/month)
/// </summary>
public class BraveSearchSettings
{
    /// <summary>
    /// Brave Search API key
    /// </summary>
    public string? ApiKey { get; set; }

    /// <summary>
    /// Whether to enable safe search
    /// </summary>
    public bool SafeSearch { get; set; } = true;

    /// <summary>
    /// Country code for search results
    /// </summary>
    public string Country { get; set; } = "US";
}

/// <summary>
/// Tavily AI Search settings
/// </summary>
public class TavilySettings
{
    /// <summary>
    /// Tavily API key
    /// </summary>
    public string? ApiKey { get; set; }

    /// <summary>
    /// Search depth (basic or advanced)
    /// </summary>
    public string SearchDepth { get; set; } = "basic";

    /// <summary>
    /// Include answer in response
    /// </summary>
    public bool IncludeAnswer { get; set; } = true;
}

/// <summary>
/// SerpApi settings (free tier: 100 searches/month)
/// </summary>
public class SerpApiSettings
{
    /// <summary>
    /// SerpApi API key
    /// </summary>
    public string? ApiKey { get; set; }

    /// <summary>
    /// Search engine to use (google, bing, etc.)
    /// </summary>
    public string Engine { get; set; } = "google";

    /// <summary>
    /// Geographic location for results
    /// </summary>
    public string Location { get; set; } = "United States";
}
