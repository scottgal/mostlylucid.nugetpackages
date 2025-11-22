namespace Mostlylucid.LlmSeoMetadata.Models;

/// <summary>
///     Input model for SEO metadata generation
/// </summary>
public class ContentInput
{
    /// <summary>
    ///     Title of the content/page
    /// </summary>
    public required string Title { get; set; }

    /// <summary>
    ///     Main content body (HTML or plain text)
    /// </summary>
    public required string Content { get; set; }

    /// <summary>
    ///     Type of content for schema.org structured data
    /// </summary>
    public SeoContentType ContentType { get; set; } = SeoContentType.Article;

    /// <summary>
    ///     URL of the page (for canonical and og:url)
    /// </summary>
    public string? Url { get; set; }

    /// <summary>
    ///     Language code (ISO 639-1, e.g., "en", "es", "fr")
    /// </summary>
    public string Language { get; set; } = "en";

    /// <summary>
    ///     Author name
    /// </summary>
    public string? Author { get; set; }

    /// <summary>
    ///     Author URL/profile
    /// </summary>
    public string? AuthorUrl { get; set; }

    /// <summary>
    ///     Publication date
    /// </summary>
    public DateTime? PublishedDate { get; set; }

    /// <summary>
    ///     Last modified date
    /// </summary>
    public DateTime? ModifiedDate { get; set; }

    /// <summary>
    ///     Featured image URL
    /// </summary>
    public string? ImageUrl { get; set; }

    /// <summary>
    ///     Image alt text
    /// </summary>
    public string? ImageAlt { get; set; }

    /// <summary>
    ///     Content category/section
    /// </summary>
    public string? Category { get; set; }

    /// <summary>
    ///     Content tags
    /// </summary>
    public List<string>? Tags { get; set; }

    /// <summary>
    ///     Unique identifier for caching (e.g., slug, ID)
    /// </summary>
    public string? CacheKey { get; set; }

    // Product-specific properties

    /// <summary>
    ///     Product price (for Product content type)
    /// </summary>
    public decimal? Price { get; set; }

    /// <summary>
    ///     Currency code (ISO 4217, e.g., "USD", "EUR")
    /// </summary>
    public string? Currency { get; set; }

    /// <summary>
    ///     Product availability (InStock, OutOfStock, PreOrder)
    /// </summary>
    public string? Availability { get; set; }

    /// <summary>
    ///     Product SKU
    /// </summary>
    public string? Sku { get; set; }

    /// <summary>
    ///     Brand name
    /// </summary>
    public string? Brand { get; set; }

    /// <summary>
    ///     Average rating (0-5)
    /// </summary>
    public double? Rating { get; set; }

    /// <summary>
    ///     Number of reviews
    /// </summary>
    public int? ReviewCount { get; set; }

    /// <summary>
    ///     Create a cache key from the content
    /// </summary>
    public string GetCacheKey()
    {
        if (!string.IsNullOrEmpty(CacheKey))
            return CacheKey;

        // Generate a hash-based key from title and content
        var combined = $"{Title}:{Content.Length}:{ContentType}";
        return Convert.ToBase64String(
            System.Security.Cryptography.SHA256.HashData(
                System.Text.Encoding.UTF8.GetBytes(combined)))[..16];
    }
}

/// <summary>
///     Request for generating specific metadata types
/// </summary>
public class GenerationRequest
{
    /// <summary>
    ///     Content to generate metadata for
    /// </summary>
    public required ContentInput Content { get; set; }

    /// <summary>
    ///     Generate meta description
    /// </summary>
    public bool GenerateMetaDescription { get; set; } = true;

    /// <summary>
    ///     Generate OpenGraph metadata
    /// </summary>
    public bool GenerateOpenGraph { get; set; } = true;

    /// <summary>
    ///     Generate JSON-LD structured data
    /// </summary>
    public bool GenerateJsonLd { get; set; } = true;

    /// <summary>
    ///     Generate keywords
    /// </summary>
    public bool GenerateKeywords { get; set; } = true;

    /// <summary>
    ///     Use cached result if available
    /// </summary>
    public bool UseCache { get; set; } = true;

    /// <summary>
    ///     Force regeneration even if cached
    /// </summary>
    public bool ForceRegenerate { get; set; }
}

/// <summary>
///     Response from metadata generation
/// </summary>
public class GenerationResponse
{
    /// <summary>
    ///     Whether generation was successful
    /// </summary>
    public bool Success { get; set; }

    /// <summary>
    ///     Generated metadata
    /// </summary>
    public SeoMetadata? Metadata { get; set; }

    /// <summary>
    ///     Error message if generation failed
    /// </summary>
    public string? Error { get; set; }

    /// <summary>
    ///     Whether the result was from cache
    /// </summary>
    public bool FromCache { get; set; }

    /// <summary>
    ///     Time taken for generation (ms)
    /// </summary>
    public long GenerationTimeMs { get; set; }

    /// <summary>
    ///     Cache key used
    /// </summary>
    public string? CacheKey { get; set; }
}
