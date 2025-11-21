# Mostlylucid.LlmAltText

> **Note**: These packages are provided as-is. I'll get them working good enough to release but I can't commit to support. However they are Unlicense so have at it!

AI-powered alt text generation and OCR using Microsoft's Florence-2 Vision Language Model. Automatically generates
descriptive, accessible alt text for images and extracts text content.

## Features

- **Automatic Alt Text Generation**: Creates descriptive, accessibility-friendly alt text for any image
- **OCR Text Extraction**: Extracts text content from images with high accuracy
- **Complete Image Analysis**: Get both alt text and extracted text in one call
- **Image Content Classification**: Detects if image is a photograph, document, screenshot, chart, etc.
- **Auto Alt Text TagHelper**: Automatically populates missing alt text on `<img>` tags
- **Database Caching**: SQLite or PostgreSQL caching to avoid regenerating alt text
- **Easy Integration**: Simple dependency injection setup with .NET
- **Configurable**: Control model paths, prompts, logging, database, and more
- **Production Ready**: Built-in error handling, diagnostics, and logging

## ⚠️ Important: Model Downloads

**This package automatically downloads AI models (~800MB) on first use.**

- Models are downloaded from Microsoft's Florence-2 repository
- Download happens once and models are cached locally
- Requires ~800MB disk space and internet connectivity on first run
- Subsequent runs use cached models (no download required)

Default model path: `./models` (configurable)

## Installation

```bash
dotnet add package Mostlylucid.LlmAltText
```

## Quick Start

### 1. Register Services

```csharp
using Mostlylucid.LlmAltText.Extensions;

// In Program.cs or Startup.cs
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

        // Generate alt text only
        var altText = await _imageAnalysis.GenerateAltTextAsync(stream);

        // Extract text only
        stream.Position = 0;
        var extractedText = await _imageAnalysis.ExtractTextAsync(stream);

        // Or get both at once
        stream.Position = 0;
        var (alt, text) = await _imageAnalysis.AnalyzeImageAsync(stream);

        return Ok(new { AltText = alt, ExtractedText = text });
    }
}
```

## API Reference

### IImageAnalysisService

#### GenerateAltTextAsync

```csharp
Task<string> GenerateAltTextAsync(Stream imageStream, string taskType = "MORE_DETAILED_CAPTION")
```

Generates descriptive alt text for an image.

**Parameters:**

- `imageStream`: Image data (Stream will NOT be disposed)
- `taskType`: Vision task level
    - `"CAPTION"` - Brief caption
    - `"DETAILED_CAPTION"` - More detailed description
    - `"MORE_DETAILED_CAPTION"` - Full descriptive alt text (default)

**Returns:** Generated alt text string

#### ExtractTextAsync

```csharp
Task<string> ExtractTextAsync(Stream imageStream)
```

Extracts text from an image using OCR.

**Parameters:**

- `imageStream`: Image data (Stream will NOT be disposed)

**Returns:** Extracted text content

#### AnalyzeImageAsync

```csharp
Task<(string AltText, string ExtractedText)> AnalyzeImageAsync(Stream imageStream)
```

Performs complete analysis: generates alt text and extracts text.

**Parameters:**

- `imageStream`: Image data (Stream will NOT be disposed)

**Returns:** Tuple of (alt text, extracted text)

#### IsReady

```csharp
bool IsReady { get; }
```

Indicates if the service is initialized and ready to process images.

## Configuration Options

### AltTextOptions

```csharp
public class AltTextOptions
{
    // Model storage location (default: "./models")
    public string ModelPath { get; set; } = "./models";

    // Custom prompt for alt text generation
    public string AltTextPrompt { get; set; } = "Provide 2-3 complete...";

    // Default task type: CAPTION, DETAILED_CAPTION, MORE_DETAILED_CAPTION
    public string DefaultTaskType { get; set; } = "MORE_DETAILED_CAPTION";

    // Enable detailed diagnostic logging
    public bool EnableDiagnosticLogging { get; set; } = true;

    // Maximum recommended word count for alt text
    public int MaxWords { get; set; } = 90;
}
```

## Auto Alt Text TagHelper

The TagHelper automatically generates alt text for `<img>` tags that don't have one, with database caching for
efficiency.

### Enable the TagHelper

```csharp
// In Program.cs
builder.Services.AddAltTextGeneration(options =>
{
    options.EnableTagHelper = true;    // Enable the TagHelper
    options.EnableDatabase = true;     // Enable caching (recommended)
    options.DbProvider = AltTextDbProvider.Sqlite;  // or PostgreSql
    options.SqliteDbPath = "./alttext.db";
});

// After building the app, migrate the database
var app = builder.Build();
await app.Services.MigrateAltTextDatabaseAsync();
```

### With PostgreSQL

```csharp
builder.Services.AddAltTextGeneration(options =>
{
    options.EnableTagHelper = true;
    options.EnableDatabase = true;
    options.DbProvider = AltTextDbProvider.PostgreSql;
    options.ConnectionString = "Host=localhost;Database=alttext;Username=user;Password=pass";
});
```

### Register TagHelpers in Views

In your `_ViewImports.cshtml`:

```cshtml
@addTagHelper *, Mostlylucid.LlmAltText
```

### Usage

The TagHelper works automatically on any `<img>` without an `alt` attribute:

```html
<!-- Alt text will be auto-generated for this image -->
<img src="https://example.com/photo.jpg" />

<!-- This image already has alt text - SKIPPED (not modified) -->
<img src="https://example.com/photo.jpg" alt="My custom alt text" />

<!-- Decorative image with empty alt - SKIPPED (intentionally empty per a11y standards) -->
<img src="https://example.com/decorative.jpg" alt="" />

<!-- Explicitly skip auto alt text generation -->
<img src="https://example.com/photo.jpg" data-skip-alt="true" />
```

**Behavior:**

- If `alt` attribute exists (even if empty) → **skipped**
- If `data-skip-alt="true"` is set → **skipped**
- If `alt` attribute is missing → **auto-generates alt text**

### Configuration Options

```csharp
options.EnableTagHelper = true;        // Enable/disable the TagHelper
options.EnableDatabase = true;         // Enable database caching
options.DbProvider = AltTextDbProvider.Sqlite;  // Sqlite or PostgreSql
options.SqliteDbPath = "./alttext.db"; // SQLite file path
options.ConnectionString = null;       // Or provide connection string
options.AutoMigrateDatabase = true;    // Auto-create database schema
options.AllowedImageDomains = new();   // Restrict to specific domains
options.SkipSrcPrefixes = new() { "data:", "blob:" };  // Skip data URIs
options.CacheDurationMinutes = 60;     // Memory cache duration
```

## Image Content Classification

Detect whether an image is a photograph, document, screenshot, chart, etc.:

```csharp
var result = await _imageAnalysis.AnalyzeWithClassificationAsync(stream);

Console.WriteLine($"Content Type: {result.ContentType}");        // e.g., "Photograph"
Console.WriteLine($"Confidence: {result.ContentTypeConfidence}"); // e.g., 0.85
Console.WriteLine($"Has Text: {result.HasSignificantText}");     // true/false
Console.WriteLine($"Alt Text: {result.AltText}");
Console.WriteLine($"OCR Text: {result.ExtractedText}");
```

### Content Types

- `Photograph` - Photos of real-world scenes, people, objects
- `Document` - Text-heavy documents, forms, PDFs
- `Screenshot` - Screenshots of software, websites, UIs
- `Chart` - Charts, graphs, data visualizations
- `Illustration` - Drawings, artwork, cartoons
- `Diagram` - Flowcharts, schematics, UML diagrams
- `Unknown` - Unable to classify

## Advanced Usage

### Custom Configuration

```csharp
services.AddAltTextGeneration(options =>
{
    // Store models in a specific location
    options.ModelPath = "/var/app/ai-models";

    // Custom alt text prompt
    options.AltTextPrompt = "Describe this image concisely for screen readers.";

    // Use simpler captions
    options.DefaultTaskType = "DETAILED_CAPTION";

    // Disable verbose logging in production
    options.EnableDiagnosticLogging = false;

    // Warn if alt text exceeds 100 words
    options.MaxWords = 100;
});
```

### Checking Service Status

```csharp
public class HealthCheckController : ControllerBase
{
    private readonly IImageAnalysisService _imageAnalysis;

    public HealthCheckController(IImageAnalysisService imageAnalysis)
    {
        _imageAnalysis = imageAnalysis;
    }

    [HttpGet("health")]
    public IActionResult CheckHealth()
    {
        if (!_imageAnalysis.IsReady)
        {
            return StatusCode(503, "AI model not initialized");
        }

        return Ok("Ready");
    }
}
```

### Handling Different Image Formats

```csharp
public async Task<string> ProcessAnyImage(byte[] imageBytes)
{
    using var stream = new MemoryStream(imageBytes);
    return await _imageAnalysis.GenerateAltTextAsync(stream);
}

public async Task<string> ProcessFromUrl(string imageUrl)
{
    using var client = new HttpClient();
    using var response = await client.GetAsync(imageUrl);
    using var stream = await response.Content.ReadAsStreamAsync();
    return await _imageAnalysis.GenerateAltTextAsync(stream);
}
```

## Logging and Diagnostics

The package provides comprehensive logging at different levels:

```csharp
// Enable diagnostic logging (verbose)
services.AddAltTextGeneration(options =>
{
    options.EnableDiagnosticLogging = true;
});
```

**Log output includes:**

- Model initialization status
- Model download progress
- Processing time for each operation
- Generated text previews
- Warnings for exceeding recommended limits
- Error details with full context

**Example logs:**

```
[Information] Initializing Florence-2 Vision Language Model...
[Information] Model path: ./models
[Information] Note: Models (~800MB) will be downloaded on first use if not present
[Information] Checking for model files...
[Information] Model download progress: 45.2% - Downloading encoder model
[Information] Florence-2 model initialized successfully
[Information] Generating alt text using task type: MORE_DETAILED_CAPTION
[Information] Alt text generated in 1247ms
[Information] Generated alt text: A person standing on a beach during sunset...
```

## Requirements

- **.NET 8.0** or **.NET 9.0**
- **~800MB disk space** for AI models (one-time download)
- **Internet connectivity** for initial model download
- **Memory**: Recommended 2GB+ RAM for model operations
- **Optional**: SQLite or PostgreSQL for TagHelper caching

## Supported Image Formats

All formats supported by SixLabors.ImageSharp:

- JPEG/JPG
- PNG
- GIF
- BMP
- TIFF
- WebP
- And more...

## Performance Considerations

- **First Run**: Slower due to model download (~800MB)
- **Initialization**: Models load into memory on service startup (1-3 seconds)
- **Per-Image Processing**: Typically 500-2000ms depending on image size and complexity
- **Singleton Service**: Model is loaded once and shared across requests
- **Thread Safety**: Service is thread-safe and can handle concurrent requests

## Troubleshooting

### Models Not Downloading

**Problem:** Model download fails or times out

**Solutions:**

- Check internet connectivity
- Ensure firewall allows downloads from Hugging Face
- Verify sufficient disk space (~800MB)
- Check write permissions on model directory

### Service Not Ready

**Problem:** `IsReady` returns false

**Solutions:**

- Check logs for initialization errors
- Verify model files exist in `ModelPath`
- Ensure sufficient memory available
- Try manual model download

### Poor Quality Alt Text

**Problem:** Generated alt text is not descriptive enough

**Solutions:**

- Use `"MORE_DETAILED_CAPTION"` task type
- Customize `AltTextPrompt` in options
- Ensure input images are clear and well-lit
- Try different image resolutions

## Accessibility Best Practices

When using generated alt text:

1. **Review AI Output**: Always review generated alt text for accuracy
2. **Keep It Concise**: Aim for 90-100 words maximum
3. **Be Descriptive**: Include context, subjects, actions, and relationships
4. **Avoid Redundancy**: Don't start with "Image of..." or "Picture of..."
5. **Consider Context**: Alt text should serve the image's purpose on the page

## License

Unlicense - Public Domain

## Contributing

Contributions welcome! Please see the main repository:
https://github.com/scottgal/mostlylucidweb

## Credits

Built on:

- **Florence-2** - Microsoft's Vision Language Model
- **SixLabors.ImageSharp** - Image processing
- **Microsoft.Extensions.*** - Dependency injection and configuration

## Support

For issues, questions, or contributions:
https://github.com/scottgal/mostlylucidweb/issues
