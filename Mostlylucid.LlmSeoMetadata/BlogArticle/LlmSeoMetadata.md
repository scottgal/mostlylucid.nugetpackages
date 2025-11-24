# AI-Powered SEO Metadata Generation with Local LLMs

Content-heavy websites need SEO optimization, but sending your content to external SaaS tools raises privacy concerns
and can get expensive. Enter **Mostlylucid.LlmSeoMetadata** - a NuGet package that generates meta descriptions,
OpenGraph tags, and JSON-LD structured data using local LLMs through Ollama.

## Why Local LLMs for SEO?

Traditional SEO tools require you to send content to third-party services. This creates several problems:

- **Privacy concerns**: Your unpublished content is sent to external servers
- **Cost**: API calls add up quickly for content-heavy sites
- **Dependency**: Your workflow depends on external service availability
- **Rate limits**: Many services throttle requests

With local LLMs, you get:

- **Complete data privacy**: Content never leaves your infrastructure
- **Zero API costs**: Run as many generations as needed
- **No rate limits**: Generate metadata for thousands of pages
- **Full control**: Customize prompts and models to your needs

## Features

The package provides comprehensive SEO metadata generation:

### Meta Descriptions

Generate compelling, keyword-rich descriptions optimized for search results (max 160 characters).

### OpenGraph Tags

Full social sharing metadata including:

- `og:title`, `og:description`, `og:type`, `og:url`
- `og:image`, `og:site_name`, `og:locale`
- Twitter Card support (`twitter:card`, `twitter:site`)
- Article metadata (`article:published_time`, `article:author`, `article:tag`)

### JSON-LD Structured Data

Schema.org structured data for rich snippets:

- **Article/BlogPosting**: Headlines, authors, dates, word counts
- **Product**: Pricing, availability, ratings, SKU
- **Other types**: Organization, Person, Event, Recipe, FAQ, HowTo

### Operation Modes

1. **Runtime Suggestions**: Generate on-demand via API endpoint
2. **Design-Time Templates**: Generate during development/build
3. **CMS Integration**: Store in SQLite/PostgreSQL for content management

## Getting Started

### Prerequisites

Install [Ollama](https://ollama.ai/) and pull a model:

```bash
ollama pull llama3.2:3b
ollama serve
```

### Installation

```bash
dotnet add package Mostlylucid.LlmSeoMetadata
```

### Basic Setup

```csharp
// Program.cs
builder.Services.AddSeoMetadata(options =>
{
    options.OllamaEndpoint = "http://localhost:11434";
    options.Model = "llama3.2:3b";
    options.SiteName = "My Blog";
    options.TwitterSite = "@myblog";
});
```

## Usage Examples

### Generate Metadata in a Controller

```csharp
public class BlogController : Controller
{
    private readonly ISeoMetadataService _seoService;

    public async Task<IActionResult> Post(string slug)
    {
        var post = await GetPost(slug);

        var request = new GenerationRequest
        {
            Content = new ContentInput
            {
                Title = post.Title,
                Content = post.Body,
                ContentType = SeoContentType.BlogPosting,
                Url = $"https://myblog.com/posts/{slug}",
                Author = post.Author,
                PublishedDate = post.PublishedAt,
                ImageUrl = post.FeaturedImage,
                Category = post.Category,
                Tags = post.Tags
            }
        };

        var result = await _seoService.GenerateMetadataAsync(request);
        ViewBag.SeoMetadata = result.Metadata;

        return View(post);
    }
}
```

### Using the TagHelper

The easiest way to render SEO tags in Razor views:

```html
@addTagHelper *, Mostlylucid.LlmSeoMetadata

<head>
    <seo-metadata
        title="@Model.Title"
        content="@Model.Content"
        content-type="BlogPosting"
        url="@Model.Url"
        image="@Model.FeaturedImage"
        author="@Model.Author"
        published="@Model.PublishedDate"
        category="@Model.Category" />
</head>
```

This automatically generates all the meta tags and JSON-LD script.

### API Endpoint for Runtime Suggestions

Create an endpoint for your CMS or editorial workflow:

```csharp
app.MapPost("/api/seo/generate", async (
    GenerationRequest request,
    ISeoMetadataService seoService) =>
{
    var result = await seoService.GenerateMetadataAsync(request);
    return result.Success ? Results.Ok(result) : Results.BadRequest(result);
});
```

## Generated Output

### Meta Tags

```html
<meta name="description" content="Learn how to leverage local LLMs for SEO optimization. Generate meta descriptions, OpenGraph tags, and JSON-LD without external services." />
<meta name="keywords" content="SEO, AI, LLM, Ollama, metadata" />
<link rel="canonical" href="https://myblog.com/posts/ai-seo-guide" />
```

### OpenGraph Tags

```html
<meta property="og:title" content="AI-Powered SEO: A Complete Guide" />
<meta property="og:description" content="Discover how to leverage local LLMs for SEO optimization." />
<meta property="og:type" content="article" />
<meta property="og:url" content="https://myblog.com/posts/ai-seo-guide" />
<meta property="og:image" content="https://myblog.com/images/ai-seo.jpg" />
<meta property="og:site_name" content="My Blog" />
<meta name="twitter:card" content="summary_large_image" />
```

### JSON-LD Structured Data

```html
<script type="application/ld+json">
{
  "@context": "https://schema.org",
  "@type": "BlogPosting",
  "headline": "AI-Powered SEO: A Complete Guide",
  "description": "Learn how to leverage local LLMs for SEO optimization.",
  "image": "https://myblog.com/images/ai-seo.jpg",
  "author": {
    "@type": "Person",
    "name": "John Doe"
  },
  "datePublished": "2024-01-15T10:00:00Z",
  "dateModified": "2024-01-16T14:30:00Z",
  "keywords": "SEO, AI, LLM, metadata",
  "wordCount": 2500
}
</script>
```

## Product Pages

For e-commerce, include product-specific data:

```csharp
var request = new GenerationRequest
{
    Content = new ContentInput
    {
        Title = "Wireless Headphones",
        Content = "High-quality wireless headphones with noise cancellation...",
        ContentType = SeoContentType.Product,
        Price = 199.99m,
        Currency = "USD",
        Availability = "InStock",
        Sku = "WH-1234",
        Brand = "AudioTech",
        Rating = 4.5,
        ReviewCount = 128
    }
};
```

This generates Product schema with pricing and rating information for rich snippets.

## Database Caching for CMS Integration

For content management systems, enable database caching to store and retrieve generated metadata:

```csharp
// Add SQLite database for caching
builder.Services
    .AddSeoMetadataDatabase("Data Source=data/seometadata.db")
    .AddSeoMetadata(
        configure: options => { /* ... */ },
        configureCache: cache =>
        {
            cache.Enabled = true;
            cache.CacheExpiration = TimeSpan.FromDays(30);
        });
```

This allows your CMS to:

- Store generated metadata with content
- Avoid regenerating for unchanged content
- Manually edit/approve generated suggestions
- Track generation statistics

## Custom Prompts

Customize the prompts to match your brand voice:

```csharp
options.MetaDescriptionPromptTemplate = @"
Generate a meta description in {language} for:
Title: {title}
Content: {content}

Requirements:
- Maximum {maxLength} characters
- Include the primary keyword
- Use an active, engaging tone
- Match our brand voice: professional but approachable
";
```

## Model Recommendations

Different models offer different trade-offs:

| Model         | Speed  | Quality | Memory |
|---------------|--------|---------|--------|
| `llama3.2:3b` | Fast   | Good    | ~4GB   |
| `mistral:7b`  | Medium | Better  | ~8GB   |
| `qwen2.5:7b`  | Medium | Better  | ~8GB   |
| `llama3.1:8b` | Slower | Best    | ~10GB  |

For most use cases, `llama3.2:3b` provides excellent results with minimal resource usage.

## Performance Considerations

### Caching Strategy

The package implements two-level caching:

1. **Memory cache**: Fast, in-process cache for hot data
2. **Database cache**: Persistent storage for CMS integration

### Batch Processing

For bulk generation (e.g., migrating existing content), use batch processing:

```csharp
var posts = await GetAllPosts();
foreach (var post in posts)
{
    var request = new GenerationRequest
    {
        Content = MapToContentInput(post),
        UseCache = true // Skip if already generated
    };

    var result = await seoService.GenerateMetadataAsync(request);
    if (result.Success && !result.FromCache)
    {
        await SaveMetadataToDatabase(post.Id, result.Metadata);
    }
}
```

## Monitoring

Track generation statistics:

```csharp
app.MapGet("/api/seo/stats", (ISeoMetadataService seoService) =>
{
    var stats = seoService.GetStatistics();
    return Results.Ok(new
    {
        stats.TotalRequests,
        stats.SuccessfulGenerations,
        stats.FailedGenerations,
        stats.CacheHits,
        stats.AverageGenerationTimeMs,
        stats.LlmConnectionHealthy
    });
});
```

## Conclusion

**Mostlylucid.LlmSeoMetadata** brings the power of AI-generated SEO metadata to your ASP.NET Core applications while
keeping your data private and your costs at zero. With support for meta descriptions, OpenGraph tags, and JSON-LD
structured data, it's a complete solution for content-heavy websites.

The package fits naturally into both new projects and existing CMS implementations, with flexible caching options and
easy Razor integration through TagHelpers.

Get started today:

```bash
dotnet add package Mostlylucid.LlmSeoMetadata
```

And check out the [demo project](https://github.com/scottgal/mostlylucid.nugetpackages) for a complete working example.
