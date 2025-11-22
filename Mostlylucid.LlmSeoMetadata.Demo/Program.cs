using System.Text.Json;
using Mostlylucid.LlmSeoMetadata.Extensions;
using Mostlylucid.LlmSeoMetadata.Models;
using Mostlylucid.LlmSeoMetadata.Services;

var builder = WebApplication.CreateBuilder(args);

// Add SEO metadata service with Ollama
builder.Services.AddSeoMetadata(options =>
{
    options.OllamaEndpoint = builder.Configuration["Ollama:Endpoint"] ?? "http://localhost:11434";
    options.Model = builder.Configuration["Ollama:Model"] ?? "llama3.2:3b";
    options.SiteName = "SEO Metadata Demo";
    options.TwitterSite = "@seometademo";
    options.DefaultOgImage = "https://example.com/og-image.jpg";
    options.EnableDiagnosticLogging = true;
    options.CacheDuration = TimeSpan.FromMinutes(30);
});

// Optional: Add database caching
// builder.Services.AddSeoMetadataDatabase();

builder.Services.AddCors(options =>
{
    options.AddDefaultPolicy(policy =>
    {
        policy.AllowAnyOrigin()
            .AllowAnyMethod()
            .AllowAnyHeader();
    });
});

var app = builder.Build();

app.UseCors();
app.UseStaticFiles();

// API Endpoints

// Health check
app.MapGet("/api/health", (ISeoMetadataService seoService) =>
{
    return Results.Ok(new
    {
        Ready = seoService.IsReady,
        Service = "LlmSeoMetadata"
    });
}).WithName("HealthCheck").WithOpenApi();

// Get service statistics
app.MapGet("/api/stats", (ISeoMetadataService seoService) =>
{
    return Results.Ok(seoService.GetStatistics());
}).WithName("GetStatistics").WithOpenApi();

// Generate complete SEO metadata
app.MapPost("/api/generate", async (
    GenerationRequest request,
    ISeoMetadataService seoService,
    CancellationToken cancellationToken) =>
{
    var result = await seoService.GenerateMetadataAsync(request, cancellationToken);
    return result.Success ? Results.Ok(result) : Results.BadRequest(result);
}).WithName("GenerateMetadata").WithOpenApi();

// Generate only meta description
app.MapPost("/api/meta-description", async (
    ContentInput content,
    ISeoMetadataService seoService,
    CancellationToken cancellationToken) =>
{
    var description = await seoService.GenerateMetaDescriptionAsync(content, cancellationToken);
    return description != null
        ? Results.Ok(new { metaDescription = description })
        : Results.BadRequest(new { error = "Failed to generate meta description" });
}).WithName("GenerateMetaDescription").WithOpenApi();

// Generate only OpenGraph metadata
app.MapPost("/api/opengraph", async (
    ContentInput content,
    ISeoMetadataService seoService,
    CancellationToken cancellationToken) =>
{
    var og = await seoService.GenerateOpenGraphAsync(content, cancellationToken);
    return og != null
        ? Results.Ok(og)
        : Results.BadRequest(new { error = "Failed to generate OpenGraph metadata" });
}).WithName("GenerateOpenGraph").WithOpenApi();

// Generate only JSON-LD structured data
app.MapPost("/api/jsonld", async (
    ContentInput content,
    ISeoMetadataService seoService,
    CancellationToken cancellationToken) =>
{
    var jsonLd = await seoService.GenerateJsonLdAsync(content, cancellationToken);
    return jsonLd != null
        ? Results.Ok(jsonLd)
        : Results.BadRequest(new { error = "Failed to generate JSON-LD" });
}).WithName("GenerateJsonLd").WithOpenApi();

// Generate keywords
app.MapPost("/api/keywords", async (
    ContentInput content,
    ISeoMetadataService seoService,
    int maxKeywords = 10,
    CancellationToken cancellationToken = default) =>
{
    var keywords = await seoService.GenerateKeywordsAsync(content, maxKeywords, cancellationToken);
    return Results.Ok(new { keywords });
}).WithName("GenerateKeywords").WithOpenApi();

// Quick test endpoint with sample content
app.MapGet("/api/demo", async (ISeoMetadataService seoService, CancellationToken cancellationToken) =>
{
    var sampleContent = new ContentInput
    {
        Title = "Getting Started with AI-Powered SEO Optimization",
        Content = @"
            In this comprehensive guide, we'll explore how to leverage local Large Language Models (LLMs)
            for SEO optimization. Using tools like Ollama, you can generate high-quality meta descriptions,
            OpenGraph tags, and JSON-LD structured data without sending your content to external services.

            Key benefits include:
            - Complete privacy and data control
            - No API costs or rate limits
            - Customizable prompts and models
            - Fast local processing

            We'll cover installation, configuration, and best practices for integrating AI-powered SEO
            into your ASP.NET Core applications. By the end of this guide, you'll have a fully functional
            SEO metadata generation system running entirely on your own infrastructure.
        ",
        ContentType = SeoContentType.BlogPosting,
        Url = "https://example.com/blog/ai-seo-guide",
        Author = "John Developer",
        PublishedDate = DateTime.UtcNow.AddDays(-7),
        ModifiedDate = DateTime.UtcNow,
        ImageUrl = "https://example.com/images/ai-seo.jpg",
        Category = "Technology",
        Tags = ["AI", "SEO", "LLM", "ASP.NET Core", "Ollama"]
    };

    var request = new GenerationRequest
    {
        Content = sampleContent,
        GenerateMetaDescription = true,
        GenerateOpenGraph = true,
        GenerateJsonLd = true,
        GenerateKeywords = true
    };

    var result = await seoService.GenerateMetadataAsync(request, cancellationToken);
    return Results.Ok(result);
}).WithName("DemoGeneration").WithOpenApi();

// Product demo endpoint
app.MapGet("/api/demo/product", async (ISeoMetadataService seoService, CancellationToken cancellationToken) =>
{
    var productContent = new ContentInput
    {
        Title = "Pro Wireless Noise-Cancelling Headphones",
        Content = @"
            Experience premium audio quality with our Pro Wireless Headphones. Featuring advanced
            active noise cancellation, 40-hour battery life, and premium drivers for exceptional
            sound clarity. The comfortable over-ear design with memory foam cushions makes them
            perfect for long listening sessions. Includes Bluetooth 5.3, multipoint connection,
            and a sleek carrying case.
        ",
        ContentType = SeoContentType.Product,
        Url = "https://store.example.com/products/pro-wireless-headphones",
        ImageUrl = "https://store.example.com/images/headphones.jpg",
        Price = 299.99m,
        Currency = "USD",
        Availability = "InStock",
        Sku = "PWH-2024-BLK",
        Brand = "AudioPro",
        Rating = 4.7,
        ReviewCount = 342,
        Category = "Electronics"
    };

    var request = new GenerationRequest
    {
        Content = productContent,
        GenerateMetaDescription = true,
        GenerateOpenGraph = true,
        GenerateJsonLd = true,
        GenerateKeywords = true
    };

    var result = await seoService.GenerateMetadataAsync(request, cancellationToken);
    return Results.Ok(result);
}).WithName("DemoProductGeneration").WithOpenApi();

// Render HTML with SEO tags
app.MapGet("/api/render", async (ISeoMetadataService seoService, CancellationToken cancellationToken) =>
{
    var content = new ContentInput
    {
        Title = "Sample Blog Post",
        Content = "This is a sample blog post about technology and programming.",
        ContentType = SeoContentType.BlogPosting,
        Url = "https://example.com/blog/sample-post",
        Author = "Jane Doe"
    };

    var request = new GenerationRequest { Content = content };
    var result = await seoService.GenerateMetadataAsync(request, cancellationToken);

    if (!result.Success || result.Metadata == null)
        return Results.BadRequest(new { error = "Failed to generate metadata" });

    var html = RenderSeoHtml(result.Metadata);
    return Results.Content(html, "text/html");
}).WithName("RenderSeoHtml").WithOpenApi();

// Serve index page
app.MapGet("/", () => Results.File(
    Path.Combine(Directory.GetCurrentDirectory(), "wwwroot", "index.html"),
    "text/html"));

app.Run();

static string RenderSeoHtml(SeoMetadata metadata)
{
    var sb = new System.Text.StringBuilder();
    sb.AppendLine("<!DOCTYPE html>");
    sb.AppendLine("<html>");
    sb.AppendLine("<head>");
    sb.AppendLine("  <meta charset=\"utf-8\">");
    sb.AppendLine($"  <title>SEO Demo</title>");

    if (!string.IsNullOrEmpty(metadata.MetaDescription))
        sb.AppendLine($"  <meta name=\"description\" content=\"{System.Web.HttpUtility.HtmlEncode(metadata.MetaDescription)}\">");

    if (metadata.Keywords?.Count > 0)
        sb.AppendLine($"  <meta name=\"keywords\" content=\"{System.Web.HttpUtility.HtmlEncode(string.Join(", ", metadata.Keywords))}\">");

    if (!string.IsNullOrEmpty(metadata.CanonicalUrl))
        sb.AppendLine($"  <link rel=\"canonical\" href=\"{metadata.CanonicalUrl}\">");

    if (metadata.OpenGraph != null)
    {
        var og = metadata.OpenGraph;
        if (!string.IsNullOrEmpty(og.Title))
            sb.AppendLine($"  <meta property=\"og:title\" content=\"{System.Web.HttpUtility.HtmlEncode(og.Title)}\">");
        if (!string.IsNullOrEmpty(og.Description))
            sb.AppendLine($"  <meta property=\"og:description\" content=\"{System.Web.HttpUtility.HtmlEncode(og.Description)}\">");
        if (!string.IsNullOrEmpty(og.Type))
            sb.AppendLine($"  <meta property=\"og:type\" content=\"{og.Type}\">");
        if (!string.IsNullOrEmpty(og.Url))
            sb.AppendLine($"  <meta property=\"og:url\" content=\"{og.Url}\">");
        if (!string.IsNullOrEmpty(og.Image))
            sb.AppendLine($"  <meta property=\"og:image\" content=\"{og.Image}\">");
        if (!string.IsNullOrEmpty(og.SiteName))
            sb.AppendLine($"  <meta property=\"og:site_name\" content=\"{System.Web.HttpUtility.HtmlEncode(og.SiteName)}\">");
    }

    if (metadata.JsonLd != null)
    {
        var jsonLdOptions = new JsonSerializerOptions
        {
            WriteIndented = true,
            DefaultIgnoreCondition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull
        };
        var jsonLd = JsonSerializer.Serialize(metadata.JsonLd, jsonLdOptions);
        sb.AppendLine($"  <script type=\"application/ld+json\">{jsonLd}</script>");
    }

    sb.AppendLine("</head>");
    sb.AppendLine("<body>");
    sb.AppendLine("  <h1>Generated SEO Metadata</h1>");
    sb.AppendLine("  <p>View page source to see the generated meta tags and JSON-LD.</p>");
    sb.AppendLine("</body>");
    sb.AppendLine("</html>");

    return sb.ToString();
}
