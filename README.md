# Mostlylucid NuGet Packages

A collection *highly experimental* ASP.NET Core middleware and services for accessibility, security, compliance, and internationalization. All packages leverage local AI models for privacy-focused solutions.

## Packages

| Package | Description |
|---------|-------------|
| **Mostlylucid.LlmAltText** | AI-powered alt text generation and OCR using Florence-2 |
| **Mostlylucid.BotDetection** | Multi-strategy bot detection middleware |
| **Mostlylucid.GeoDetection** | IP-based geo-location and country routing |
| **Mostlylucid.LlmSlideTranslator** | RAG-assisted document translation |

## Target Frameworks

All packages support:
- .NET 8.0
- .NET 9.0

## License

All packages are released under the [Unlicense](https://unlicense.org/) (Public Domain).

---

## Mostlylucid.LlmAltText

AI-powered alt text generation and OCR for images using Microsoft's Florence-2 Vision Language Model.

### Features

- **Automatic alt text generation** for accessibility-friendly image descriptions
- **OCR text extraction** from images
- **Image content classification** (photograph, document, screenshot, chart, illustration, diagram)
- **ASP.NET Core TagHelper** to automatically populate missing alt text on `<img>` tags
- **Database caching** (SQLite or PostgreSQL) to avoid regenerating alt text
- **Multiple caption types**: CAPTION, DETAILED_CAPTION, MORE_DETAILED_CAPTION

### Quick Start

```csharp
// Program.cs
builder.Services.AddImageAnalysisServices(options =>
{
    options.ModelPath = "path/to/florence2/model";
});

// Usage
public class MyService
{
    private readonly IImageAnalysisService _imageService;

    public async Task<string> GetAltText(Stream imageStream)
    {
        var result = await _imageService.AnalyzeImageAsync(imageStream, ImageTask.Caption);
        return result.AltText;
    }
}
```

### TagHelper Usage

```html
<!-- Automatically generates alt text for images without one -->
<img src="/images/photo.jpg" alt-text-auto />
```

### Notes

- First run downloads ~800MB of model files (cached locally)
- All processing is done locally - no cloud API calls

---

## Mostlylucid.BotDetection

Multi-strategy bot detection middleware for ASP.NET Core with behavioral analysis and optional LLM classification.

### Features

- **Multi-Strategy Detection**:
  - User-Agent analysis (500+ known bot signatures)
  - HTTP header inspection for suspicious patterns
  - IP-based detection (datacenter/cloud provider ranges)
  - Behavioral analysis (request frequency, timing patterns)
  - Optional LLM-based classification via Ollama

- **Automatic bot list updates** (every 24 hours)
- **Flexible response options**: block, rate-limit, or detect-only
- **Results caching** with configurable duration
- **Confidence scoring** (0.0-1.0) with detailed reasons

### Bot Types

```csharp
public enum BotType
{
    Unknown,
    SearchEngine,
    SocialMediaBot,
    MonitoringBot,
    Scraper,
    MaliciousBot,
    GoodBot,
    VerifiedBot
}
```

### Quick Start

```csharp
// Program.cs
builder.Services.AddBotDetection(options =>
{
    options.EnableLlmDetection = false; // Set true to use Ollama
    options.CacheDuration = TimeSpan.FromMinutes(30);
});

app.UseBotDetection();

// Usage in controller
public class MyController : Controller
{
    private readonly IBotDetectionService _botService;

    public async Task<IActionResult> Index()
    {
        var result = await _botService.DetectAsync(HttpContext);
        if (result.IsBot && result.BotType == BotType.MaliciousBot)
        {
            return StatusCode(403);
        }
        return View();
    }
}
```

### Performance

- 1-5ms detection without LLM
- 50-200ms with LLM enabled (optional)

---

## Mostlylucid.GeoDetection

Geographic location detection and country-based routing middleware for ASP.NET Core.

### Features

- **IP-to-location lookup** with caching
- **Country-based access control** (allow/block by country code)
- **GeoRoute attribute** for endpoint-level restrictions
- **Reverse proxy support** (X-Forwarded-For, CF-Connecting-IP headers)
- **ISO 3166-1 alpha-2** country codes

### GeoLocation Data

```csharp
public class GeoLocation
{
    public string CountryCode { get; set; }      // "US", "GB", etc.
    public string CountryName { get; set; }
    public string ContinentCode { get; set; }
    public string RegionCode { get; set; }
    public string City { get; set; }
    public double Latitude { get; set; }
    public double Longitude { get; set; }
    public string Timezone { get; set; }
    public bool IsVpn { get; set; }
    public bool IsProxy { get; set; }
    public bool IsHosting { get; set; }
}
```

### Quick Start

```csharp
// Program.cs
builder.Services.AddGeoDetection(options =>
{
    options.AllowedCountries = new[] { "US", "CA", "GB" };
    options.BlockedCountries = new[] { "XX" };
});

app.UseGeoDetection();

// Attribute-based routing
[GeoRoute(AllowedCountries = new[] { "US", "CA" })]
public IActionResult UsOnlyContent() => View();
```

---

## Mostlylucid.LlmSlideTranslator

RAG-assisted translation for long documents using small local LLMs with sliding-window context and vector similarity search.

### Features

- **RAG-Enhanced Translation**: Retrieves similar earlier blocks to maintain terminology consistency
- **Sliding Window Context**: Includes previous translated block for continuity
- **Multiple Translation Methods**:
  - RAG + LLM (recommended for consistency)
  - LLM only (creative but may drift)
  - NMT baseline + LLM post-editing
  - NMT only (baseline quality, fastest)

- **Markdown-Aware Chunking**: Intelligently splits content while preserving structure
- **Vector Store Support**: File-based (default) or Qdrant database
- **Real-time Progress**: SignalR support for streaming updates

### How It Works

1. Chunk markdown into translatable blocks
2. Generate vector embeddings for each block
3. Store blocks and embeddings in vector database
4. For each block: retrieve similar blocks (RAG) + include previous block
5. Translate with LLM using retrieved context
6. Output aligned block-by-block translation

### Quick Start

```csharp
// Program.cs
builder.Services.AddLlmSlideTranslator(options =>
{
    options.SourceLanguage = "en";
    options.TargetLanguage = "fr";
    options.EmbeddingProvider = EmbeddingProvider.Ollama;
    options.VectorStoreProvider = VectorStoreProvider.File;
});

// Usage
public class TranslationService
{
    private readonly ILlmSlideTranslator _translator;

    public async Task<string> TranslateDocument(string markdown)
    {
        var result = await _translator.TranslateAsync(markdown);
        return result.TranslatedContent;
    }
}
```

### Embedding Providers

- **Ollama** - Via local Ollama API
- **LlamaSharp** - Direct GGUF model inference

---

## Demo Applications

### Mostlylucid.AltText.Demo

Web UI demonstration with drag-and-drop image upload, live alt text generation, and OCR capabilities.

### Mostlylucid.BotDetection.Demo

Interactive demo to test different User-Agent strings and view detection statistics.

### mostlylucid.llmslidetranslator.Demo

Minimal API demonstration with SignalR real-time updates and translation comparison endpoints.

---

## Project Structure

```
mostlylucid.nugetpackages/
├── Mostlylucid.LlmAltText/           # Alt text generation package
├── Mostlylucid.LlmAltText.Test/      # Unit tests
├── Mostlylucid.AltText.Demo/         # Demo application
│
├── Mostlylucid.BotDetection/         # Bot detection package
├── Mostlylucid.BotDetection.Test/    # Unit tests
├── Mostlylucid.BotDetection.Demo/    # Demo application
│
├── Mostlylucid.GeoDetection/         # Geo detection package
├── Mostlylucid.GeoDetection.Test/    # Unit tests
│
├── mostlylucid.llmslidetranslator/   # Translation package
└── mostlylucid.llmslidetranslator.Demo/ # Demo application
```

---

## Contributing

Contributions are welcome! Please feel free to submit issues and pull requests.

## Author

Scott Galloway - [mostlylucid.net](https://mostlylucid.net)
