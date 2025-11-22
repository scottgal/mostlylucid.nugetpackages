# Mostlylucid.LlmSeoMetadata

AI-powered SEO metadata generation using local LLMs via Ollama. Generate meta descriptions, OpenGraph tags, and JSON-LD structured data for your content without sending data to external SaaS services.

## Features

- **Meta Description Generation**: Create compelling, SEO-optimized meta descriptions
- **OpenGraph Tags**: Generate og:title, og:description, og:type, and Twitter Card metadata
- **JSON-LD Structured Data**: Generate schema.org structured data for:
  - Article, BlogPosting, NewsArticle
  - Product (with pricing and ratings)
  - Service, Organization, Person
  - Event, Recipe, FAQPage, HowTo
- **Two Operation Modes**:
  - **Design-time**: Generate templates during development/build
  - **Runtime**: On-demand suggestions via API endpoint
- **Database Caching**: Store generated metadata in SQLite or PostgreSQL for CMS integration
- **ASP.NET Core TagHelper**: Easy integration with Razor views
- **Privacy-First**: All processing done locally with your own LLM

## Installation

```bash
dotnet add package Mostlylucid.LlmSeoMetadata
```

## Prerequisites

- [Ollama](https://ollama.ai/) running locally with a suitable model
- Recommended models:
  - `llama3.2:3b` (default, good balance of speed/quality)
  - `mistral:7b` (higher quality)
  - `qwen2.5:7b` (good for multilingual content)

```bash
# Install and run a model
ollama pull llama3.2:3b
ollama serve
```

## Quick Start

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

### Generate Metadata

```csharp
public class BlogController : Controller
{
    private readonly ISeoMetadataService _seoService;

    public BlogController(ISeoMetadataService seoService)
    {
        _seoService = seoService;
    }

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

Or with pre-generated metadata:

```html
<head>
    <seo-metadata metadata="@ViewBag.SeoMetadata" />
</head>
```

## Configuration Options

```csharp
builder.Services.AddSeoMetadata(options =>
{
    // Ollama settings
    options.OllamaEndpoint = "http://localhost:11434";
    options.Model = "llama3.2:3b";
    options.Temperature = 0.3f;
    options.MaxTokens = 512;
    options.TimeoutSeconds = 60;

    // SEO settings
    options.MaxMetaDescriptionLength = 160;
    options.MaxOgDescriptionLength = 300;
    options.DefaultLanguage = "en";

    // Site info for OpenGraph
    options.SiteName = "My Website";
    options.DefaultOgImage = "https://mysite.com/og-default.jpg";
    options.TwitterCardType = "summary_large_image";
    options.TwitterSite = "@mysite";

    // Caching
    options.CacheDuration = TimeSpan.FromHours(24);

    // Modes
    options.EnableDesignTimeGeneration = true;
    options.EnableRuntimeSuggestions = true;

    // Debugging
    options.EnableDiagnosticLogging = false;
});
```

## Database Caching

For CMS integration, enable database caching to persist generated metadata:

### SQLite (Default)

```csharp
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

### PostgreSQL

```csharp
builder.Services
    .AddSeoMetadataPostgresDatabase("Host=localhost;Database=mydb;Username=user;Password=pass")
    .AddSeoMetadata(configureCache: cache => cache.Enabled = true);
```

## Convenience Methods

```csharp
// For blog sites
builder.Services.AddSeoMetadataForBlog("My Blog", "@myblog");

// For e-commerce
builder.Services.AddSeoMetadataForEcommerce("My Store", "USD");
```

## Content Types

```csharp
public enum SeoContentType
{
    Article,        // Generic article
    BlogPosting,    // Blog post
    NewsArticle,    // News article
    Product,        // Product page
    Service,        // Service page
    Organization,   // About/company page
    Person,         // Author/profile page
    Event,          // Event page
    Recipe,         // Recipe content
    FAQPage,        // FAQ page
    HowTo,          // How-to guide
    WebPage         // Generic page
}
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

## Custom Prompts

Customize the prompts used for generation:

```csharp
options.MetaDescriptionPromptTemplate = @"
Generate a meta description in {language} for:
Title: {title}
Content: {content}
Keep it under {maxLength} characters. Be engaging and include keywords.";

options.OpenGraphPromptTemplate = @"
Create social sharing text for {contentType} content:
Title: {title}
Respond with JSON: {""title"": ""..."", ""description"": ""...""}";
```

## API Endpoints

Create a runtime suggestion endpoint:

```csharp
app.MapPost("/api/seo/generate", async (
    GenerationRequest request,
    ISeoMetadataService seoService) =>
{
    var result = await seoService.GenerateMetadataAsync(request);
    return result.Success ? Results.Ok(result) : Results.BadRequest(result);
});

app.MapGet("/api/seo/stats", (ISeoMetadataService seoService) =>
{
    return Results.Ok(seoService.GetStatistics());
});
```

## Generated Output Example

### Meta Tags

```html
<meta name="description" content="Learn how to implement AI-powered SEO metadata generation using local LLMs. This guide covers setup, configuration, and best practices." />
<meta name="keywords" content="SEO, AI, LLM, metadata, Ollama, ASP.NET Core" />
<link rel="canonical" href="https://myblog.com/posts/ai-seo-guide" />
```

### OpenGraph Tags

```html
<meta property="og:title" content="AI-Powered SEO: A Complete Guide" />
<meta property="og:description" content="Discover how to leverage local LLMs for SEO optimization without relying on external services." />
<meta property="og:type" content="article" />
<meta property="og:url" content="https://myblog.com/posts/ai-seo-guide" />
<meta property="og:image" content="https://myblog.com/images/ai-seo.jpg" />
<meta property="og:site_name" content="My Blog" />
<meta name="twitter:card" content="summary_large_image" />
<meta name="twitter:site" content="@myblog" />
```

### JSON-LD

```html
<script type="application/ld+json">
{
  "@context": "https://schema.org",
  "@type": "BlogPosting",
  "headline": "AI-Powered SEO: A Complete Guide",
  "description": "Learn how to implement AI-powered SEO metadata generation using local LLMs.",
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

## License

This project is released under the Unlicense (public domain).
