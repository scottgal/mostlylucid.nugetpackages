using System.Text.Json.Serialization;

namespace Mostlylucid.LlmSeoMetadata.Models;

/// <summary>
///     Complete SEO metadata for a page
/// </summary>
public class SeoMetadata
{
    /// <summary>
    ///     Meta description for search engines (max 160 characters)
    /// </summary>
    public string? MetaDescription { get; set; }

    /// <summary>
    ///     OpenGraph metadata for social sharing
    /// </summary>
    public OpenGraphMetadata? OpenGraph { get; set; }

    /// <summary>
    ///     JSON-LD structured data
    /// </summary>
    public JsonLdMetadata? JsonLd { get; set; }

    /// <summary>
    ///     Additional meta keywords (optional, less important for modern SEO)
    /// </summary>
    public List<string>? Keywords { get; set; }

    /// <summary>
    ///     Canonical URL for the page
    /// </summary>
    public string? CanonicalUrl { get; set; }

    /// <summary>
    ///     Robots meta directive (index, follow, noindex, nofollow)
    /// </summary>
    public string? Robots { get; set; }

    /// <summary>
    ///     When the metadata was generated
    /// </summary>
    public DateTime GeneratedAt { get; set; } = DateTime.UtcNow;

    /// <summary>
    ///     Model used to generate the metadata
    /// </summary>
    public string? GeneratedByModel { get; set; }

    /// <summary>
    ///     Generation mode (DesignTime or Runtime)
    /// </summary>
    public GenerationMode Mode { get; set; }
}

/// <summary>
///     OpenGraph metadata for social sharing
/// </summary>
public class OpenGraphMetadata
{
    /// <summary>
    ///     og:title - Title for social sharing
    /// </summary>
    public string? Title { get; set; }

    /// <summary>
    ///     og:description - Description for social sharing
    /// </summary>
    public string? Description { get; set; }

    /// <summary>
    ///     og:type - Content type (article, website, product, etc.)
    /// </summary>
    public string Type { get; set; } = "website";

    /// <summary>
    ///     og:url - Canonical URL
    /// </summary>
    public string? Url { get; set; }

    /// <summary>
    ///     og:image - Image URL for sharing
    /// </summary>
    public string? Image { get; set; }

    /// <summary>
    ///     og:image:alt - Alt text for the image
    /// </summary>
    public string? ImageAlt { get; set; }

    /// <summary>
    ///     og:site_name - Name of the website
    /// </summary>
    public string? SiteName { get; set; }

    /// <summary>
    ///     og:locale - Locale (e.g., en_US)
    /// </summary>
    public string? Locale { get; set; }

    /// <summary>
    ///     article:published_time - Publication date (for articles)
    /// </summary>
    public DateTime? PublishedTime { get; set; }

    /// <summary>
    ///     article:modified_time - Last modified date (for articles)
    /// </summary>
    public DateTime? ModifiedTime { get; set; }

    /// <summary>
    ///     article:author - Author name or URL (for articles)
    /// </summary>
    public string? Author { get; set; }

    /// <summary>
    ///     article:section - Content section/category
    /// </summary>
    public string? Section { get; set; }

    /// <summary>
    ///     article:tag - Content tags
    /// </summary>
    public List<string>? Tags { get; set; }

    /// <summary>
    ///     Twitter card type
    /// </summary>
    public string? TwitterCard { get; set; }

    /// <summary>
    ///     Twitter site handle
    /// </summary>
    public string? TwitterSite { get; set; }

    /// <summary>
    ///     Twitter creator handle
    /// </summary>
    public string? TwitterCreator { get; set; }
}

/// <summary>
///     JSON-LD structured data for search engines
/// </summary>
public class JsonLdMetadata
{
    /// <summary>
    ///     @context - Always "https://schema.org"
    /// </summary>
    [JsonPropertyName("@context")]
    public string Context { get; set; } = "https://schema.org";

    /// <summary>
    ///     @type - Schema.org type (Article, BlogPosting, Product, etc.)
    /// </summary>
    [JsonPropertyName("@type")]
    public string Type { get; set; } = "Article";

    /// <summary>
    ///     Headline/title of the content
    /// </summary>
    [JsonPropertyName("headline")]
    public string? Headline { get; set; }

    /// <summary>
    ///     Description of the content
    /// </summary>
    [JsonPropertyName("description")]
    public string? Description { get; set; }

    /// <summary>
    ///     Main image URL
    /// </summary>
    [JsonPropertyName("image")]
    public string? Image { get; set; }

    /// <summary>
    ///     Author information
    /// </summary>
    [JsonPropertyName("author")]
    public JsonLdAuthor? Author { get; set; }

    /// <summary>
    ///     Publisher information
    /// </summary>
    [JsonPropertyName("publisher")]
    public JsonLdOrganization? Publisher { get; set; }

    /// <summary>
    ///     Date published (ISO 8601 format)
    /// </summary>
    [JsonPropertyName("datePublished")]
    public string? DatePublished { get; set; }

    /// <summary>
    ///     Date modified (ISO 8601 format)
    /// </summary>
    [JsonPropertyName("dateModified")]
    public string? DateModified { get; set; }

    /// <summary>
    ///     Main entity of the page (URL)
    /// </summary>
    [JsonPropertyName("mainEntityOfPage")]
    public JsonLdMainEntity? MainEntityOfPage { get; set; }

    /// <summary>
    ///     Keywords for the content
    /// </summary>
    [JsonPropertyName("keywords")]
    public string? Keywords { get; set; }

    /// <summary>
    ///     Language of the content
    /// </summary>
    [JsonPropertyName("inLanguage")]
    public string? InLanguage { get; set; }

    /// <summary>
    ///     Word count (for articles)
    /// </summary>
    [JsonPropertyName("wordCount")]
    public int? WordCount { get; set; }

    /// <summary>
    ///     Article body (optional, for full text)
    /// </summary>
    [JsonPropertyName("articleBody")]
    public string? ArticleBody { get; set; }

    /// <summary>
    ///     Article section/category
    /// </summary>
    [JsonPropertyName("articleSection")]
    public string? ArticleSection { get; set; }

    // Product-specific properties

    /// <summary>
    ///     Product name
    /// </summary>
    [JsonPropertyName("name")]
    public string? Name { get; set; }

    /// <summary>
    ///     Product offers/pricing
    /// </summary>
    [JsonPropertyName("offers")]
    public JsonLdOffer? Offers { get; set; }

    /// <summary>
    ///     Product brand
    /// </summary>
    [JsonPropertyName("brand")]
    public JsonLdBrand? Brand { get; set; }

    /// <summary>
    ///     Product SKU
    /// </summary>
    [JsonPropertyName("sku")]
    public string? Sku { get; set; }

    /// <summary>
    ///     Aggregate rating
    /// </summary>
    [JsonPropertyName("aggregateRating")]
    public JsonLdAggregateRating? AggregateRating { get; set; }
}

/// <summary>
///     JSON-LD Author type
/// </summary>
public class JsonLdAuthor
{
    [JsonPropertyName("@type")]
    public string Type { get; set; } = "Person";

    [JsonPropertyName("name")]
    public string? Name { get; set; }

    [JsonPropertyName("url")]
    public string? Url { get; set; }
}

/// <summary>
///     JSON-LD Organization type
/// </summary>
public class JsonLdOrganization
{
    [JsonPropertyName("@type")]
    public string Type { get; set; } = "Organization";

    [JsonPropertyName("name")]
    public string? Name { get; set; }

    [JsonPropertyName("url")]
    public string? Url { get; set; }

    [JsonPropertyName("logo")]
    public JsonLdImageObject? Logo { get; set; }
}

/// <summary>
///     JSON-LD Image Object type
/// </summary>
public class JsonLdImageObject
{
    [JsonPropertyName("@type")]
    public string Type { get; set; } = "ImageObject";

    [JsonPropertyName("url")]
    public string? Url { get; set; }

    [JsonPropertyName("width")]
    public int? Width { get; set; }

    [JsonPropertyName("height")]
    public int? Height { get; set; }
}

/// <summary>
///     JSON-LD Main Entity reference
/// </summary>
public class JsonLdMainEntity
{
    [JsonPropertyName("@type")]
    public string Type { get; set; } = "WebPage";

    [JsonPropertyName("@id")]
    public string? Id { get; set; }
}

/// <summary>
///     JSON-LD Offer for products
/// </summary>
public class JsonLdOffer
{
    [JsonPropertyName("@type")]
    public string Type { get; set; } = "Offer";

    [JsonPropertyName("price")]
    public string? Price { get; set; }

    [JsonPropertyName("priceCurrency")]
    public string? PriceCurrency { get; set; }

    [JsonPropertyName("availability")]
    public string? Availability { get; set; }

    [JsonPropertyName("url")]
    public string? Url { get; set; }

    [JsonPropertyName("priceValidUntil")]
    public string? PriceValidUntil { get; set; }
}

/// <summary>
///     JSON-LD Brand
/// </summary>
public class JsonLdBrand
{
    [JsonPropertyName("@type")]
    public string Type { get; set; } = "Brand";

    [JsonPropertyName("name")]
    public string? Name { get; set; }
}

/// <summary>
///     JSON-LD Aggregate Rating
/// </summary>
public class JsonLdAggregateRating
{
    [JsonPropertyName("@type")]
    public string Type { get; set; } = "AggregateRating";

    [JsonPropertyName("ratingValue")]
    public string? RatingValue { get; set; }

    [JsonPropertyName("reviewCount")]
    public string? ReviewCount { get; set; }

    [JsonPropertyName("bestRating")]
    public string? BestRating { get; set; }

    [JsonPropertyName("worstRating")]
    public string? WorstRating { get; set; }
}

/// <summary>
///     Generation mode for SEO metadata
/// </summary>
public enum GenerationMode
{
    /// <summary>
    ///     Generated at design/build time for templates
    /// </summary>
    DesignTime,

    /// <summary>
    ///     Generated at runtime on demand
    /// </summary>
    Runtime
}
