namespace Mostlylucid.LlmSeoMetadata.Models;

/// <summary>
///     Configuration options for SEO metadata generation
/// </summary>
public class SeoMetadataOptions
{
    /// <summary>
    ///     Enable SEO metadata generation (default: true)
    /// </summary>
    public bool Enabled { get; set; } = true;

    /// <summary>
    ///     Ollama API endpoint (default: http://localhost:11434)
    /// </summary>
    public string OllamaEndpoint { get; set; } = "http://localhost:11434";

    /// <summary>
    ///     Ollama model to use for generation (default: llama3.2:3b)
    ///     Recommended models: llama3.2:3b, mistral:7b, qwen2.5:7b
    /// </summary>
    public string Model { get; set; } = "llama3.2:3b";

    /// <summary>
    ///     Temperature for generation (0.0-1.0, lower = more focused, default: 0.3)
    /// </summary>
    public float Temperature { get; set; } = 0.3f;

    /// <summary>
    ///     Maximum tokens for generation (default: 512)
    /// </summary>
    public int MaxTokens { get; set; } = 512;

    /// <summary>
    ///     Timeout for LLM requests in seconds (default: 60)
    /// </summary>
    public int TimeoutSeconds { get; set; } = 60;

    /// <summary>
    ///     Maximum length for meta descriptions (default: 160 characters)
    /// </summary>
    public int MaxMetaDescriptionLength { get; set; } = 160;

    /// <summary>
    ///     Maximum length for OpenGraph descriptions (default: 300 characters)
    /// </summary>
    public int MaxOgDescriptionLength { get; set; } = 300;

    /// <summary>
    ///     Cache duration for generated metadata (default: 24 hours)
    /// </summary>
    public TimeSpan CacheDuration { get; set; } = TimeSpan.FromHours(24);

    /// <summary>
    ///     Default language for content (ISO code, default: en)
    /// </summary>
    public string DefaultLanguage { get; set; } = "en";

    /// <summary>
    ///     Site name for OpenGraph tags
    /// </summary>
    public string? SiteName { get; set; }

    /// <summary>
    ///     Default OpenGraph image URL
    /// </summary>
    public string? DefaultOgImage { get; set; }

    /// <summary>
    ///     Twitter card type (summary, summary_large_image)
    /// </summary>
    public string TwitterCardType { get; set; } = "summary_large_image";

    /// <summary>
    ///     Twitter site handle (e.g., @yoursite)
    /// </summary>
    public string? TwitterSite { get; set; }

    /// <summary>
    ///     Enable design-time template generation mode
    /// </summary>
    public bool EnableDesignTimeGeneration { get; set; } = true;

    /// <summary>
    ///     Enable runtime suggestion endpoint
    /// </summary>
    public bool EnableRuntimeSuggestions { get; set; } = true;

    /// <summary>
    ///     Custom prompt template for meta description generation
    ///     Placeholders: {title}, {content}, {maxLength}, {language}
    /// </summary>
    public string? MetaDescriptionPromptTemplate { get; set; }

    /// <summary>
    ///     Custom prompt template for OpenGraph generation
    ///     Placeholders: {title}, {content}, {maxLength}, {language}, {contentType}
    /// </summary>
    public string? OpenGraphPromptTemplate { get; set; }

    /// <summary>
    ///     Custom prompt template for JSON-LD generation
    ///     Placeholders: {title}, {content}, {contentType}, {language}
    /// </summary>
    public string? JsonLdPromptTemplate { get; set; }

    /// <summary>
    ///     Enable diagnostic logging of prompts and responses
    /// </summary>
    public bool EnableDiagnosticLogging { get; set; }
}

/// <summary>
///     Database cache options for storing generated metadata
/// </summary>
public class SeoCacheOptions
{
    /// <summary>
    ///     Enable database caching (default: false, uses memory-only)
    /// </summary>
    public bool Enabled { get; set; }

    /// <summary>
    ///     SQLite connection string (default: local file)
    /// </summary>
    public string ConnectionString { get; set; } = "Data Source=data/seometadata.db";

    /// <summary>
    ///     How long to cache generated metadata in the database
    /// </summary>
    public TimeSpan CacheExpiration { get; set; } = TimeSpan.FromDays(30);

    /// <summary>
    ///     Enable cleanup of expired entries
    /// </summary>
    public bool EnableCleanup { get; set; } = true;

    /// <summary>
    ///     How often to run cleanup (default: daily)
    /// </summary>
    public TimeSpan CleanupInterval { get; set; } = TimeSpan.FromDays(1);
}

/// <summary>
///     Content types for JSON-LD generation
/// </summary>
public enum SeoContentType
{
    /// <summary>
    ///     Generic article content
    /// </summary>
    Article,

    /// <summary>
    ///     Blog post content
    /// </summary>
    BlogPosting,

    /// <summary>
    ///     News article
    /// </summary>
    NewsArticle,

    /// <summary>
    ///     Product page
    /// </summary>
    Product,

    /// <summary>
    ///     Service page
    /// </summary>
    Service,

    /// <summary>
    ///     Organization/About page
    /// </summary>
    Organization,

    /// <summary>
    ///     Person/Author page
    /// </summary>
    Person,

    /// <summary>
    ///     Event page
    /// </summary>
    Event,

    /// <summary>
    ///     Recipe content
    /// </summary>
    Recipe,

    /// <summary>
    ///     FAQ page
    /// </summary>
    FAQPage,

    /// <summary>
    ///     How-to guide
    /// </summary>
    HowTo,

    /// <summary>
    ///     Generic web page
    /// </summary>
    WebPage
}