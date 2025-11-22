using System.Text;
using System.Text.Encodings.Web;
using System.Text.Json;
using Microsoft.AspNetCore.Razor.TagHelpers;
using Mostlylucid.LlmSeoMetadata.Models;
using Mostlylucid.LlmSeoMetadata.Services;

namespace Mostlylucid.LlmSeoMetadata.TagHelpers;

/// <summary>
///     Tag helper for rendering SEO metadata tags in the document head
/// </summary>
/// <example>
///     <seo-metadata title="My Page" content="@Model.Content" content-type="BlogPosting" />
/// </example>
[HtmlTargetElement("seo-metadata")]
public class SeoMetadataTagHelper : TagHelper
{
    private readonly ISeoMetadataService _seoService;

    public SeoMetadataTagHelper(ISeoMetadataService seoService)
    {
        _seoService = seoService;
    }

    /// <summary>
    ///     Page title
    /// </summary>
    [HtmlAttributeName("title")]
    public string Title { get; set; } = string.Empty;

    /// <summary>
    ///     Page content (text or HTML)
    /// </summary>
    [HtmlAttributeName("content")]
    public string Content { get; set; } = string.Empty;

    /// <summary>
    ///     Content type for structured data
    /// </summary>
    [HtmlAttributeName("content-type")]
    public SeoContentType ContentType { get; set; } = SeoContentType.Article;

    /// <summary>
    ///     Page URL
    /// </summary>
    [HtmlAttributeName("url")]
    public string? Url { get; set; }

    /// <summary>
    ///     Featured image URL
    /// </summary>
    [HtmlAttributeName("image")]
    public string? ImageUrl { get; set; }

    /// <summary>
    ///     Author name
    /// </summary>
    [HtmlAttributeName("author")]
    public string? Author { get; set; }

    /// <summary>
    ///     Publication date
    /// </summary>
    [HtmlAttributeName("published")]
    public DateTime? PublishedDate { get; set; }

    /// <summary>
    ///     Last modified date
    /// </summary>
    [HtmlAttributeName("modified")]
    public DateTime? ModifiedDate { get; set; }

    /// <summary>
    ///     Content category
    /// </summary>
    [HtmlAttributeName("category")]
    public string? Category { get; set; }

    /// <summary>
    ///     Cache key for storing generated metadata
    /// </summary>
    [HtmlAttributeName("cache-key")]
    public string? CacheKey { get; set; }

    /// <summary>
    ///     Pre-generated metadata (optional, to avoid runtime generation)
    /// </summary>
    [HtmlAttributeName("metadata")]
    public SeoMetadata? Metadata { get; set; }

    /// <summary>
    ///     Generate meta description tag
    /// </summary>
    [HtmlAttributeName("render-meta")]
    public bool RenderMetaDescription { get; set; } = true;

    /// <summary>
    ///     Generate OpenGraph tags
    /// </summary>
    [HtmlAttributeName("render-og")]
    public bool RenderOpenGraph { get; set; } = true;

    /// <summary>
    ///     Generate JSON-LD script
    /// </summary>
    [HtmlAttributeName("render-jsonld")]
    public bool RenderJsonLd { get; set; } = true;

    /// <summary>
    ///     Generate canonical URL link
    /// </summary>
    [HtmlAttributeName("render-canonical")]
    public bool RenderCanonical { get; set; } = true;

    public override async Task ProcessAsync(TagHelperContext context, TagHelperOutput output)
    {
        output.TagName = null; // Don't render a wrapper element

        // Use provided metadata or generate new
        var metadata = Metadata;

        if (metadata == null && !string.IsNullOrEmpty(Title) && !string.IsNullOrEmpty(Content))
        {
            var contentInput = new ContentInput
            {
                Title = Title,
                Content = Content,
                ContentType = ContentType,
                Url = Url,
                ImageUrl = ImageUrl,
                Author = Author,
                PublishedDate = PublishedDate,
                ModifiedDate = ModifiedDate,
                Category = Category,
                CacheKey = CacheKey
            };

            var request = new GenerationRequest
            {
                Content = contentInput,
                GenerateMetaDescription = RenderMetaDescription,
                GenerateOpenGraph = RenderOpenGraph,
                GenerateJsonLd = RenderJsonLd,
                GenerateKeywords = true,
                UseCache = true
            };

            var result = await _seoService.GenerateMetadataAsync(request);
            if (result.Success)
            {
                metadata = result.Metadata;
            }
        }

        if (metadata == null)
        {
            output.SuppressOutput();
            return;
        }

        var sb = new StringBuilder();

        // Meta description
        if (RenderMetaDescription && !string.IsNullOrEmpty(metadata.MetaDescription))
        {
            sb.AppendLine($"<meta name=\"description\" content=\"{HtmlEncoder.Default.Encode(metadata.MetaDescription)}\" />");
        }

        // Keywords
        if (metadata.Keywords?.Count > 0)
        {
            sb.AppendLine($"<meta name=\"keywords\" content=\"{HtmlEncoder.Default.Encode(string.Join(", ", metadata.Keywords))}\" />");
        }

        // Canonical URL
        if (RenderCanonical && !string.IsNullOrEmpty(metadata.CanonicalUrl))
        {
            sb.AppendLine($"<link rel=\"canonical\" href=\"{HtmlEncoder.Default.Encode(metadata.CanonicalUrl)}\" />");
        }

        // Robots
        if (!string.IsNullOrEmpty(metadata.Robots))
        {
            sb.AppendLine($"<meta name=\"robots\" content=\"{HtmlEncoder.Default.Encode(metadata.Robots)}\" />");
        }

        // OpenGraph tags
        if (RenderOpenGraph && metadata.OpenGraph != null)
        {
            var og = metadata.OpenGraph;

            if (!string.IsNullOrEmpty(og.Title))
                sb.AppendLine($"<meta property=\"og:title\" content=\"{HtmlEncoder.Default.Encode(og.Title)}\" />");

            if (!string.IsNullOrEmpty(og.Description))
                sb.AppendLine($"<meta property=\"og:description\" content=\"{HtmlEncoder.Default.Encode(og.Description)}\" />");

            if (!string.IsNullOrEmpty(og.Type))
                sb.AppendLine($"<meta property=\"og:type\" content=\"{og.Type}\" />");

            if (!string.IsNullOrEmpty(og.Url))
                sb.AppendLine($"<meta property=\"og:url\" content=\"{HtmlEncoder.Default.Encode(og.Url)}\" />");

            if (!string.IsNullOrEmpty(og.Image))
                sb.AppendLine($"<meta property=\"og:image\" content=\"{HtmlEncoder.Default.Encode(og.Image)}\" />");

            if (!string.IsNullOrEmpty(og.ImageAlt))
                sb.AppendLine($"<meta property=\"og:image:alt\" content=\"{HtmlEncoder.Default.Encode(og.ImageAlt)}\" />");

            if (!string.IsNullOrEmpty(og.SiteName))
                sb.AppendLine($"<meta property=\"og:site_name\" content=\"{HtmlEncoder.Default.Encode(og.SiteName)}\" />");

            if (!string.IsNullOrEmpty(og.Locale))
                sb.AppendLine($"<meta property=\"og:locale\" content=\"{og.Locale}\" />");

            if (og.PublishedTime.HasValue)
                sb.AppendLine($"<meta property=\"article:published_time\" content=\"{og.PublishedTime.Value:yyyy-MM-ddTHH:mm:ssZ}\" />");

            if (og.ModifiedTime.HasValue)
                sb.AppendLine($"<meta property=\"article:modified_time\" content=\"{og.ModifiedTime.Value:yyyy-MM-ddTHH:mm:ssZ}\" />");

            if (!string.IsNullOrEmpty(og.Author))
                sb.AppendLine($"<meta property=\"article:author\" content=\"{HtmlEncoder.Default.Encode(og.Author)}\" />");

            if (!string.IsNullOrEmpty(og.Section))
                sb.AppendLine($"<meta property=\"article:section\" content=\"{HtmlEncoder.Default.Encode(og.Section)}\" />");

            if (og.Tags?.Count > 0)
            {
                foreach (var tag in og.Tags)
                {
                    sb.AppendLine($"<meta property=\"article:tag\" content=\"{HtmlEncoder.Default.Encode(tag)}\" />");
                }
            }

            // Twitter Card tags
            if (!string.IsNullOrEmpty(og.TwitterCard))
                sb.AppendLine($"<meta name=\"twitter:card\" content=\"{og.TwitterCard}\" />");

            if (!string.IsNullOrEmpty(og.TwitterSite))
                sb.AppendLine($"<meta name=\"twitter:site\" content=\"{HtmlEncoder.Default.Encode(og.TwitterSite)}\" />");

            if (!string.IsNullOrEmpty(og.TwitterCreator))
                sb.AppendLine($"<meta name=\"twitter:creator\" content=\"{HtmlEncoder.Default.Encode(og.TwitterCreator)}\" />");
        }

        // JSON-LD structured data
        if (RenderJsonLd && metadata.JsonLd != null)
        {
            var jsonLdOptions = new JsonSerializerOptions
            {
                WriteIndented = false,
                DefaultIgnoreCondition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull
            };
            var jsonLd = JsonSerializer.Serialize(metadata.JsonLd, jsonLdOptions);
            sb.AppendLine($"<script type=\"application/ld+json\">{jsonLd}</script>");
        }

        output.Content.SetHtmlContent(sb.ToString());
    }
}
