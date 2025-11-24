# Mostlylucid.LlmAltText

[![NuGet](https://img.shields.io/nuget/v/Mostlylucid.LlmAltText.svg)](https://www.nuget.org/packages/Mostlylucid.LlmAltText)
[![License: Unlicense](https://img.shields.io/badge/license-Unlicense-blue.svg)](http://unlicense.org/)

> **Note**: These packages are provided as-is. I'll get them working good enough to release but I can't commit to
> support. However they are Unlicense so have at it!

AI-powered alt text generation and OCR using Microsoft's Florence-2 Vision Language Model. Automatically generates
descriptive, accessible alt text for images and extracts text content.

## Features

- **Automatic Alt Text Generation**: Creates descriptive, accessibility-friendly alt text for any image
- **OCR Text Extraction**: Extracts text content from images with high accuracy
- **Complete Image Analysis**: Get both alt text and extracted text in one call
- **Image Content Classification**: Detects if image is a photograph, document, screenshot, chart, etc.
- **Auto Alt Text TagHelper**: Automatically populates missing alt text on `<img>` tags in Razor views
- **Multiple Input Sources**: Load images from files, URLs, byte arrays, or streams
- **Automatic Image Resizing**: Large images are automatically downscaled for optimal processing
- **Database Caching**: SQLite or PostgreSQL caching to avoid regenerating alt text
- **Easy Integration**: Simple dependency injection setup with .NET
- **Configurable**: Control model paths, prompts, logging, database, and more
- **Production Ready**: Built-in error handling, OpenTelemetry support, and logging
- **Wide Format Support**: JPEG, PNG, GIF, BMP, TIFF, WebP, and more via ImageSharp

## Important: Model Downloads

**This package automatically downloads AI models (~800MB) on first use.**

- Models are downloaded from Microsoft's Florence-2 repository
- Download happens once and models are cached locally
- Requires ~800MB disk space and internet connectivity on first run
- Subsequent runs use cached models (no download required)
- Default model path: `./models` (configurable)

## Installation

```bash
dotnet add package Mostlylucid.LlmAltText
```

## Quick Start

### 1. Register Services

```csharp
using Mostlylucid.LlmAltText.Extensions;

// In Program.cs
builder.Services.AddAltTextGeneration();

// Or with custom configuration
builder.Services.AddAltTextGeneration(options =>
{
    options.ModelPath = "./my-models";          // Where to store models
    options.EnableDiagnosticLogging = true;     // Detailed logging
    options.MaxWords = 90;                      // Recommended max words
    options.DefaultTaskType = "MORE_DETAILED_CAPTION";
});
```

### 2. Use the Service

```csharp
using Mostlylucid.LlmAltText.Services;

public class ImageController : ControllerBase
{
    private readonly IImageAnalysisService _imageAnalysis;

    public ImageController(IImageAnalysisService imageAnalysis)
    {
        _imageAnalysis = imageAnalysis;
    }

    [HttpPost("analyze")]
    public async Task<IActionResult> AnalyzeImage(IFormFile image)
    {
        using var stream = image.OpenReadStream();

        // Get complete analysis with content classification
        var result = await _imageAnalysis.AnalyzeWithClassificationAsync(stream);

        return Ok(new
        {
            AltText = result.AltText,
            ExtractedText = result.ExtractedText,
            ContentType = result.ContentType.ToString(),
            Confidence = result.ContentTypeConfidence,
            HasText = result.HasSignificantText
        });
    }
}
```

## API Reference

### IImageAnalysisService

The main service interface for image analysis.

#### Properties

| Property | Type | Description |
|----------|------|-------------|
| `IsReady` | `bool` | Indicates if the service is initialized and ready to process images |

#### Stream-Based Methods

```csharp
// Generate alt text from a stream
Task<string> GenerateAltTextAsync(Stream imageStream, string taskType = "MORE_DETAILED_CAPTION");

// Extract text via OCR
Task<string> ExtractTextAsync(Stream imageStream);

// Get both alt text and extracted text
Task<(string AltText, string ExtractedText)> AnalyzeImageAsync(Stream imageStream);

// Complete analysis with content type classification
Task<ImageAnalysisResult> AnalyzeWithClassificationAsync(Stream imageStream);

// Classify content type only
Task<(ImageContentType Type, double Confidence)> ClassifyContentTypeAsync(Stream imageStream);
```

#### File-Based Methods

```csharp
// Generate alt text from a file path
Task<string> GenerateAltTextFromFileAsync(string filePath,
    string taskType = "MORE_DETAILED_CAPTION",
    CancellationToken cancellationToken = default);

// Extract text from a file
Task<string> ExtractTextFromFileAsync(string filePath,
    CancellationToken cancellationToken = default);

// Analyze a file
Task<(string AltText, string ExtractedText)> AnalyzeImageFromFileAsync(string filePath,
    CancellationToken cancellationToken = default);

// Complete analysis of a file
Task<ImageAnalysisResult> AnalyzeWithClassificationFromFileAsync(string filePath,
    CancellationToken cancellationToken = default);
```

#### URL-Based Methods

```csharp
// Generate alt text from a URL (string or Uri overloads)
Task<string> GenerateAltTextFromUrlAsync(string imageUrl,
    string taskType = "MORE_DETAILED_CAPTION",
    CancellationToken cancellationToken = default);

Task<string> GenerateAltTextFromUrlAsync(Uri imageUrl,
    string taskType = "MORE_DETAILED_CAPTION",
    CancellationToken cancellationToken = default);

// Extract text from URL
Task<string> ExtractTextFromUrlAsync(string imageUrl,
    CancellationToken cancellationToken = default);

// Analyze image from URL
Task<(string AltText, string ExtractedText)> AnalyzeImageFromUrlAsync(string imageUrl,
    CancellationToken cancellationToken = default);

// Complete analysis from URL
Task<ImageAnalysisResult> AnalyzeWithClassificationFromUrlAsync(string imageUrl,
    CancellationToken cancellationToken = default);
```

#### Byte Array Methods

```csharp
// Generate alt text from byte array
Task<string> GenerateAltTextAsync(byte[] imageData, string taskType = "MORE_DETAILED_CAPTION");

// Extract text from byte array
Task<string> ExtractTextAsync(byte[] imageData);

// Analyze byte array
Task<(string AltText, string ExtractedText)> AnalyzeImageAsync(byte[] imageData);

// Complete analysis of byte array
Task<ImageAnalysisResult> AnalyzeWithClassificationAsync(byte[] imageData);
```

### Task Types

| Task Type | Description |
|-----------|-------------|
| `"CAPTION"` | Brief, simple caption |
| `"DETAILED_CAPTION"` | More detailed description |
| `"MORE_DETAILED_CAPTION"` | Full descriptive alt text (default, recommended) |

### ImageAnalysisResult

```csharp
public class ImageAnalysisResult
{
    public required string AltText { get; set; }
    public required string ExtractedText { get; set; }
    public ImageContentType ContentType { get; set; }
    public double ContentTypeConfidence { get; set; }  // 0-1
    public bool HasSignificantText { get; set; }
}
```

### Content Types

| Type | Description |
|------|-------------|
| `Photograph` | Photos of real-world scenes, people, objects |
| `Document` | Text-heavy documents, forms, PDFs |
| `Screenshot` | Screenshots of software, websites, UIs |
| `Chart` | Charts, graphs, data visualizations |
| `Illustration` | Drawings, artwork, cartoons |
| `Diagram` | Flowcharts, schematics, UML diagrams |
| `Unknown` | Unable to classify |

## Usage Examples

### Basic Usage with Streams

```csharp
public async Task<string> GetAltTextFromFormFile(IFormFile file)
{
    using var stream = file.OpenReadStream();
    return await _imageAnalysis.GenerateAltTextAsync(stream);
}
```

### Processing Local Files

```csharp
// Single file
var altText = await _imageAnalysis.GenerateAltTextFromFileAsync("/path/to/image.jpg");

// Batch processing
var files = Directory.GetFiles("/images", "*.jpg");
foreach (var file in files)
{
    var result = await _imageAnalysis.AnalyzeWithClassificationFromFileAsync(file);
    Console.WriteLine($"{Path.GetFileName(file)}: {result.AltText}");
}
```

### Processing Images from URLs

```csharp
// From string URL
var altText = await _imageAnalysis.GenerateAltTextFromUrlAsync(
    "https://example.com/photo.jpg");

// From Uri with cancellation
var cts = new CancellationTokenSource(TimeSpan.FromSeconds(30));
var result = await _imageAnalysis.AnalyzeWithClassificationFromUrlAsync(
    new Uri("https://example.com/photo.jpg"),
    cts.Token);
```

### Processing Byte Arrays

```csharp
// From HttpClient response
var httpClient = new HttpClient();
var imageBytes = await httpClient.GetByteArrayAsync("https://example.com/image.png");
var altText = await _imageAnalysis.GenerateAltTextAsync(imageBytes);

// From base64
var base64 = "iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAYAAAAfFcSJ...";
var bytes = Convert.FromBase64String(base64);
var result = await _imageAnalysis.AnalyzeWithClassificationAsync(bytes);
```

### Different Task Types

```csharp
using var stream = file.OpenReadStream();

// Brief caption
var brief = await _imageAnalysis.GenerateAltTextAsync(stream, "CAPTION");
// Result: "A dog sitting on grass"

stream.Position = 0;

// Detailed caption
var detailed = await _imageAnalysis.GenerateAltTextAsync(stream, "DETAILED_CAPTION");
// Result: "A golden retriever sitting on green grass in a park"

stream.Position = 0;

// Most detailed (default, best for accessibility)
var full = await _imageAnalysis.GenerateAltTextAsync(stream, "MORE_DETAILED_CAPTION");
// Result: "A happy golden retriever with light fur sitting on lush green grass
//          in a sunny park, with trees visible in the background."
```

### Content Type Classification

```csharp
var result = await _imageAnalysis.AnalyzeWithClassificationAsync(stream);

switch (result.ContentType)
{
    case ImageContentType.Document:
        // Prioritize extracted text for screen readers
        Console.WriteLine($"Document text: {result.ExtractedText}");
        break;

    case ImageContentType.Screenshot:
        // Include both alt text and extracted UI text
        Console.WriteLine($"Screenshot: {result.AltText}");
        if (result.HasSignificantText)
            Console.WriteLine($"UI Text: {result.ExtractedText}");
        break;

    case ImageContentType.Photograph:
        // Focus on descriptive alt text
        Console.WriteLine($"Photo: {result.AltText}");
        break;

    case ImageContentType.Chart:
        // Include data if extracted
        Console.WriteLine($"Chart: {result.AltText}");
        Console.WriteLine($"Data: {result.ExtractedText}");
        break;
}
```

## Auto Alt Text TagHelper

The TagHelper automatically generates alt text for `<img>` tags that don't have one.

### Enable the TagHelper

```csharp
// In Program.cs
builder.Services.AddAltTextGeneration(options =>
{
    options.EnableTagHelper = true;
    options.EnableDatabase = true;              // Enable caching (recommended)
    options.DbProvider = AltTextDbProvider.Sqlite;
    options.SqliteDbPath = "./alttext.db";
    options.CacheDurationMinutes = 60;
});

// After building the app, migrate the database
var app = builder.Build();
await app.Services.MigrateAltTextDatabaseAsync();
```

### Register TagHelpers in Views

In your `_ViewImports.cshtml`:

```cshtml
@addTagHelper *, Mostlylucid.LlmAltText
```

### TagHelper Behavior

```html
<!-- Alt text will be auto-generated for this image -->
<img src="https://example.com/photo.jpg" />

<!-- Has alt text - SKIPPED (not modified) -->
<img src="https://example.com/photo.jpg" alt="My custom alt text" />

<!-- Empty alt - SKIPPED (intentionally empty for decorative images) -->
<img src="https://example.com/decorative.jpg" alt="" />

<!-- Explicitly skip - SKIPPED -->
<img src="https://example.com/photo.jpg" data-skip-alt="true" />

<!-- Data URI - SKIPPED -->
<img src="data:image/png;base64,iVBORw0KGgo..." />
```

### TagHelper Rules

| Condition | Action |
|-----------|--------|
| `alt` attribute exists (any value) | **Skipped** - respects existing alt |
| `alt=""` (empty) | **Skipped** - decorative image per a11y standards |
| `data-skip-alt="true"` | **Skipped** - explicitly opted out |
| `src` starts with `data:` or `blob:` | **Skipped** - data URIs not fetched |
| `src` is relative path | **Skipped** - cannot fetch without base URL |
| Domain not in `AllowedImageDomains` | **Skipped** - security restriction |
| No `alt` attribute | **Generates** - fetches image and creates alt text |

### Domain Restrictions

```csharp
options.AllowedImageDomains = new List<string>
{
    "mycdn.example.com",
    "images.example.org"
};
// Only images from these domains will be processed
```

### PostgreSQL Configuration

```csharp
builder.Services.AddAltTextGeneration(options =>
{
    options.EnableTagHelper = true;
    options.EnableDatabase = true;
    options.DbProvider = AltTextDbProvider.PostgreSql;
    options.ConnectionString = "Host=localhost;Database=alttext;Username=user;Password=pass";
});
```

## Configuration Options

### AltTextOptions

```csharp
public class AltTextOptions
{
    // Model storage location
    public string ModelPath { get; set; } = "./models";

    // Custom prompt for alt text generation
    public string AltTextPrompt { get; set; } = "Provide 2-3 complete...";

    // Default task type: CAPTION, DETAILED_CAPTION, MORE_DETAILED_CAPTION
    public string DefaultTaskType { get; set; } = "MORE_DETAILED_CAPTION";

    // Enable detailed diagnostic logging
    public bool EnableDiagnosticLogging { get; set; } = true;

    // Maximum recommended word count for alt text
    public int MaxWords { get; set; } = 90;

    // TagHelper settings
    public bool EnableTagHelper { get; set; } = false;
    public bool EnableDatabase { get; set; } = false;
    public bool AutoMigrateDatabase { get; set; } = true;
    public AltTextDbProvider DbProvider { get; set; } = AltTextDbProvider.Sqlite;
    public string SqliteDbPath { get; set; } = "alttext.db";
    public string? ConnectionString { get; set; }

    // Security
    public List<string> AllowedImageDomains { get; set; } = new();
    public List<string> SkipSrcPrefixes { get; set; } = new() { "data:", "blob:" };

    // Caching
    public int CacheDurationMinutes { get; set; } = 60;
}
```

### Configuration Examples

```csharp
// Production configuration
builder.Services.AddAltTextGeneration(options =>
{
    options.ModelPath = "/var/app/ai-models";
    options.EnableDiagnosticLogging = false;
    options.MaxWords = 100;
    options.EnableTagHelper = true;
    options.EnableDatabase = true;
    options.DbProvider = AltTextDbProvider.PostgreSql;
    options.ConnectionString = Environment.GetEnvironmentVariable("ALTTEXT_DB");
});

// Development configuration
builder.Services.AddAltTextGeneration(options =>
{
    options.ModelPath = "./models";
    options.EnableDiagnosticLogging = true;
    options.EnableTagHelper = true;
    options.EnableDatabase = true;
    options.DbProvider = AltTextDbProvider.Sqlite;
    options.SqliteDbPath = "./dev-alttext.db";
});

// Minimal (no database, no TagHelper)
builder.Services.AddAltTextGeneration();
```

## Image Processing

### Automatic Resizing

Large images are automatically downscaled before processing:

- **Max dimension**: 2048px (width or height)
- **Max file size trigger**: 20MB
- **Resize algorithm**: Lanczos3 (high quality)
- **Output format**: PNG (lossless)

This ensures consistent processing times and prevents memory issues with very large images.

### Supported Formats

All formats supported by SixLabors.ImageSharp:

- JPEG/JPG
- PNG
- GIF (first frame)
- BMP
- TIFF
- WebP
- PBM
- TGA
- QOI

## OpenTelemetry Support

The package includes built-in OpenTelemetry instrumentation:

```csharp
// Add tracing
builder.Services.AddOpenTelemetry()
    .WithTracing(tracing =>
    {
        tracing.AddSource("Mostlylucid.LlmAltText");
    });
```

### Traced Activities

- `llmalttext.generate_alt_text`
- `llmalttext.extract_text`
- `llmalttext.analyze_image`
- `llmalttext.analyze_with_classification`
- `llmalttext.classify_content_type`

### Recorded Metrics

- Image size (bytes)
- Processing duration (ms)
- Alt text length
- Extracted text length
- Content type classification
- Confidence scores
- Success/failure status

## Health Checks

```csharp
public class ImageAnalysisHealthCheck : IHealthCheck
{
    private readonly IImageAnalysisService _service;

    public ImageAnalysisHealthCheck(IImageAnalysisService service)
    {
        _service = service;
    }

    public Task<HealthCheckResult> CheckHealthAsync(
        HealthCheckContext context,
        CancellationToken cancellationToken = default)
    {
        if (_service.IsReady)
            return Task.FromResult(HealthCheckResult.Healthy("AI model ready"));

        return Task.FromResult(HealthCheckResult.Unhealthy("AI model not initialized"));
    }
}
```

## Performance

| Metric | Typical Value |
|--------|--------------|
| First run | Slower (800MB model download) |
| Initialization | 1-3 seconds (model load) |
| Per-image processing | 500-2000ms |
| Memory usage | 2GB+ recommended |
| Model size | ~800MB on disk |

### Tips

- Register as **Singleton** (model initialization is expensive)
- Enable database caching for repeated images
- Use cancellation tokens for timeout handling
- Process images in parallel with caution (memory usage)

## Troubleshooting

### Models Not Downloading

**Problem:** Model download fails or times out

**Solutions:**
- Check internet connectivity
- Ensure firewall allows downloads from Hugging Face
- Verify sufficient disk space (~800MB)
- Check write permissions on model directory
- Set a custom `ModelPath` to a writable location

### Service Not Ready

**Problem:** `IsReady` returns false

**Solutions:**
- Check logs for initialization errors
- Verify model files exist in `ModelPath`
- Ensure sufficient memory available (2GB+)
- Check for exceptions during startup

### Poor Quality Alt Text

**Problem:** Generated alt text is not descriptive enough

**Solutions:**
- Use `"MORE_DETAILED_CAPTION"` task type (default)
- Customize `AltTextPrompt` in options
- Ensure input images are clear and well-lit
- Check if image is too small or blurry

### TagHelper Not Working

**Problem:** Alt text not generated for images

**Solutions:**
- Verify `EnableTagHelper = true` in options
- Check `@addTagHelper` in `_ViewImports.cshtml`
- Ensure images have absolute URLs (relative not supported)
- Check `AllowedImageDomains` if configured
- Look for `data-skip-alt` attribute
- Verify database migration ran

## Accessibility Best Practices

When using generated alt text:

1. **Review AI Output**: Always review generated alt text for accuracy
2. **Keep It Concise**: Aim for 90-100 words maximum
3. **Be Descriptive**: Include context, subjects, actions, and relationships
4. **Avoid Redundancy**: Don't start with "Image of..." or "Picture of..."
5. **Consider Context**: Alt text should serve the image's purpose on the page
6. **Use Empty Alt for Decorative**: Set `alt=""` for purely decorative images
7. **Include Text in Images**: If image contains text, include it in alt or nearby

## Requirements

- **.NET 8.0** or **.NET 9.0**
- **~800MB disk space** for AI models (one-time download)
- **Internet connectivity** for initial model download
- **Memory**: Recommended 2GB+ RAM for model operations
- **Optional**: SQLite or PostgreSQL for TagHelper caching

## License

Unlicense - Public Domain

## Contributing

Contributions welcome! Please see the main repository:
https://github.com/scottgal/mostlylucidweb

## Credits

Built on:

- **Florence-2** - Microsoft's Vision Language Model
- **SixLabors.ImageSharp** - Cross-platform image processing
- **Entity Framework Core** - Database access
- **Microsoft.Extensions.*** - Dependency injection and configuration

## Support

For issues, questions, or contributions:
https://github.com/scottgal/mostlylucidweb/issues
