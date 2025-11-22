# mostlylucid's playground lab


> **IMPORTANT: Despite the name of the repo this is likely SUPER broken. It's the lab where I experiement with features before deciding if I break them out to actual packages. YOU HAVE BEEN WARNED**



A collection of *highly experimental* ASP.NET Core middleware and services for accessibility, security, compliance, and internationalization. All packages leverage local AI models (primarily via [Ollama](https://ollama.ai/)) for privacy-focused solutions - **no data ever leaves your infrastructure**.

---

## Table of Contents

- [Target Frameworks](#target-frameworks)
- [License](#license)
- [Prerequisites](#prerequisites)
- **Packages:**
  - [Mostlylucid.Common](#mostlylucidcommon) - Shared utilities
  - [Mostlylucid.BotDetection](#mostlylucidbotdetection) - Bot detection
  - [Mostlylucid.GeoDetection](#mostlylucidgeodetection) - Geo-location routing
  - [Mostlylucid.LlmAltText](#mostlylucidllmalttext) - Alt text generation
  - [Mostlylucid.LlmPiiRedactor](#mostlylucidllmpiiredactor) - PII redaction
  - [Mostlylucid.LLMContentModeration](#mostlylucidllmcontentmoderation) - Content moderation
  - [Mostlylucid.LlmAccessibilityAuditor](#mostlylucidllmaccessibilityauditor) - Accessibility auditing
  - [Mostlylucid.LlmLogSummarizer](#mostlylucidllmlogsummarizer) - Log summarization
  - [Mostlylucid.LlmSeoMetadata](#mostlylucidllmseometadata) - SEO metadata
  - [Mostlylucid.LlmSlideTranslator](#mostlylucidllmslidetranslator) - Document translation
  - [Mostlylucid.LlmI18nAssistant](#mostlylucidllmi18nassistant) - Resource localization
  - [Mostlylucid.RagLlmSearch](#mostlylucidragllmsearch) - RAG-enabled chat with web search
- [Project Structure](#project-structure)
- [Contributing](#contributing)

---

## Target Frameworks

All packages support:
- .NET 8.0
- .NET 9.0

## License

All packages are released under the [Unlicense](https://unlicense.org/) (Public Domain).

---

## Packages Overview

| Package | Description | Source |
|---------|-------------|--------|
| **Mostlylucid.Common** | Shared abstractions, caching, and utilities | [Source](./Mostlylucid.Common) |
| **Mostlylucid.BotDetection** | Multi-strategy bot detection middleware | [Source](./Mostlylucid.BotDetection) |
| **Mostlylucid.GeoDetection** | IP-based geo-location and country routing | [Source](./Mostlylucid.GeoDetection) |
| **Mostlylucid.LlmAltText** | AI-powered alt text generation and OCR | [Source](./Mostlylucid.LlmAltText) |
| **Mostlylucid.LlmPiiRedactor** | PII detection and redaction | [Source](./Mostlylucid.LlmPiiRedactor) |
| **Mostlylucid.LLMContentModeration** | LLM-powered content moderation | [Source](./Mostlylucid.LLMContentModeration) |
| **Mostlylucid.LlmAccessibilityAuditor** | HTML accessibility auditing | [Source](./Mostlylucid.LlmAccessibilityAuditor) |
| **Mostlylucid.LlmLogSummarizer** | AI-powered log analysis and summarization | [Source](./Mostlylucid.LlmLogSummarizer) |
| **Mostlylucid.LlmSeoMetadata** | SEO metadata generation | [Source](./Mostlylucid.LlmSeoMetadata) |
| **Mostlylucid.LlmSlideTranslator** | RAG-assisted document translation | [Source](./mostlylucid.llmslidetranslator) |
| **Mostlylucid.LlmI18nAssistant** | Short-string localization helper | [Source](./mostlylucid.llmi18nassistant) |
| **Mostlylucid.RagLlmSearch** | RAG-enabled chat with multi-provider search | [Source](./Mostlylucid.RagLlmSearch) |

---

## Mostlylucid.Common

Shared abstractions, base classes, and utilities for all Mostlylucid packages.

### Features

- Generic caching service with memory cache implementation
- Statistics tracking interfaces
- Periodic update service for background operations
- Middleware base classes with test mode support
- IP address extraction helpers (proxy/CDN aware)

### Quick Start

```csharp
// Register caching service
services.AddMemoryCachingService<MyData>(options =>
{
    options.DefaultExpiration = TimeSpan.FromMinutes(30);
    options.MaxEntries = 5000;
});

// Use in your service
public class MyService
{
    private readonly ICachingService<MyData> _cache;

    public async Task<MyData?> GetDataAsync(string key)
    {
        return await _cache.GetOrAddAsync(key, async () =>
        {
            return await FetchFromSourceAsync(key);
        });
    }
}
```

[Full documentation](Mostlylucid.Common/README.md)

---

## Mostlylucid.BotDetection

Multi-strategy bot detection middleware with behavioral analysis and optional LLM classification.

### Features

- **User-Agent Analysis**: 500+ known bot signatures
- **Header Inspection**: Suspicious pattern detection
- **IP Detection**: Datacenter/cloud provider ranges
- **Behavioral Analysis**: Request frequency and timing
- **LLM Classification**: Optional Ollama-based detection
- **Auto-updating Blocklists**: 24-hour refresh cycle
- **Confidence Scoring**: 0.0-1.0 with detailed reasons

### Quick Start

```csharp
// Program.cs
builder.Services.AddBotDetection(options =>
{
    options.EnableBehavioralAnalysis = true;
    options.EnableLlmDetection = false; // Optional: requires Ollama
    options.BlockBots = false;          // Just detect, don't block
});

app.UseBotDetection();

// Usage in controller
public async Task<IActionResult> Index()
{
    var result = await _botService.DetectAsync(HttpContext);
    if (result.IsBot && result.BotType == BotType.MaliciousBot)
    {
        return StatusCode(403);
    }
    return View();
}
```

**Performance**: 1-5ms without LLM, 50-200ms with LLM

[Full documentation](Mostlylucid.BotDetection/README.md)

---

## Mostlylucid.GeoDetection

Geographic location detection and country-based routing. **Works out of the box** with free ip-api.com - no account required!

### Features

- **Multiple Providers**: ip-api.com (free), DataHub CSV (local), MaxMind GeoLite2
- **Country-based Access Control**: Allow/block by country code
- **GeoRoute Attribute**: Endpoint-level restrictions
- **Reverse Proxy Support**: X-Forwarded-For, CF-Connecting-IP
- **Memory + Database Caching**: Optional SQLite/EF Core persistence

### Quick Start

```csharp
// Zero configuration - uses ip-api.com, no account needed
builder.Services.AddGeoRoutingWithIpApi();

app.UseForwardedHeaders();
app.UseGeoRouting();

// Country-restricted endpoint
app.MapGet("/us-only", () => "US Content").RequireCountry("US");

// MVC attribute
[GeoRoute(AllowedCountries = new[] { "US", "CA" })]
public class NorthAmericaController : Controller { }
```

| Provider | Setup | Best For |
|----------|-------|----------|
| **IpApi** | None | Quick start, development |
| **DataHubCsv** | None | Production, local, country-level |
| **MaxMindLocal** | Free account | Production, city-level precision |

[Full documentation](Mostlylucid.GeoDetection/README.md)

---

## Mostlylucid.LlmAltText

AI-powered alt text generation and OCR using Microsoft's Florence-2 Vision Language Model.

### Features

- **Automatic Alt Text Generation**: Accessibility-friendly descriptions
- **OCR Text Extraction**: High-accuracy text extraction
- **Image Classification**: Photograph, document, screenshot, chart, etc.
- **ASP.NET Core TagHelper**: Auto-populates missing `<img>` alt text
- **Database Caching**: SQLite or PostgreSQL

### Quick Start

```csharp
// Program.cs
builder.Services.AddAltTextGeneration(options =>
{
    options.EnableTagHelper = true;
    options.EnableDatabase = true;
    options.DbProvider = AltTextDbProvider.Sqlite;
});

// Migrate database
await app.Services.MigrateAltTextDatabaseAsync();

// Enable TagHelper in _ViewImports.cshtml
@addTagHelper *, Mostlylucid.LlmAltText
```

```html
<!-- Alt text auto-generated -->
<img src="/images/photo.jpg" />

<!-- Skip with data attribute -->
<img src="/images/decorative.jpg" data-skip-alt="true" />
```

**Note**: First run downloads ~800MB of model files (cached locally).

[Full documentation](Mostlylucid.LlmAltText/README.md)

---

## Mostlylucid.LlmPiiRedactor

Comprehensive PII detection and redaction for ASP.NET Core applications.

### Features

- **Multi-Type Detection**: Emails, phones, credit cards, SSNs, IPs, names, addresses, postcodes, bank accounts, API keys
- **Redaction Styles**: Full mask, partial mask, tokenized, type labels, hashed, removal
- **ASP.NET Integration**: Request/response body, headers, query strings
- **Logging Integration**: ILogger wrapper, Serilog enricher
- **Compliance Presets**: GDPR and PCI-DSS configurations

### Quick Start

```csharp
// Program.cs
builder.Services.AddPiiRedaction(
    configureRedaction: options =>
    {
        options.DefaultStyle = RedactionStyle.PartialMask;
        options.DetectionTypes = PiiType.Email | PiiType.CreditCard | PiiType.PhoneNumber;
    },
    configureMiddleware: options =>
    {
        options.RedactRequestBody = true;
        options.RedactResponseBody = true;
    }
);

app.UsePiiRedaction();

// Direct usage
var result = _redactionService.Redact("Contact john@example.com");
// Result: "Contact jo****@example.com"
```

```
Original: Contact john.doe@example.com or call +1-555-123-4567

Full Mask:     Contact ********************* or call **************
Partial Mask:  Contact jo****@example.com or call ****-****-****-4567
Tokenized:     Contact [EMAIL_001] or call [PHONE_001]
```

[Full documentation](Mostlylucid.LlmPiiRedactor/README.md)

---

## Mostlylucid.LLMContentModeration

Local LLM-powered content moderation. All processing happens on your server - no data ever leaves your infrastructure.

### Features

- **Content Classification**: Toxicity, abuse, spam, self-harm, NSFW
- **PII Detection**: Email, phone, address, IBAN, credit cards
- **Three Modes**: DetectOnly, Block, MaskAndAllow
- **Per-Route Policies**: Configure different levels per controller/action
- **Privacy-First**: All processing via local Ollama

### Quick Start

```csharp
// Program.cs
builder.Services.AddLLMContentModeration(options =>
{
    options.Ollama.Endpoint = "http://localhost:11434";
    options.Ollama.Model = "llama3.2:3b";
    options.DefaultMode = ModerationMode.Block;
});

app.UseContentModeration();

// Per-route policy
[ModerationPolicy(ModerationMode.Block, EnablePii = true, EnableToxicity = true)]
public class CommentsController : Controller { }

// Direct usage
var result = await _moderation.ModerateAsync(content);
if (result.IsFlagged)
{
    foreach (var flag in result.Flags)
        Console.WriteLine($"{flag.Category}: {flag.Confidence:P}");
}
```

[Full documentation](Mostlylucid.LLMContentModeration/README.md)

---

## Mostlylucid.LlmAccessibilityAuditor

LLM-powered HTML accessibility auditor with rule-based and AI analysis.

### Features

- **Rule-based Analysis**: Fast HTML parsing for common issues
- **LLM Analysis**: Ollama-powered detection of subtle issues
- **Detected Issues**: Missing ARIA labels, heading hierarchy, contrast, click targets, forms, tables
- **ASP.NET Middleware**: Auto-audit HTML responses in development
- **Diagnostic Dashboard**: Web UI and JSON API
- **Inline Widget**: Floating issues display on pages
- **WCAG References**: Guideline mappings included

### Quick Start

```csharp
// Program.cs
builder.Services.AddAccessibilityAuditor(options =>
{
    options.Enabled = true;
    options.OnlyInDevelopment = true;
    options.EnableLlmAnalysis = true;
    options.EnableInlineReport = true;

    options.Ollama.Endpoint = "http://localhost:11434";
    options.Ollama.Model = "llama3.2:3b";
});

app.UseAccessibilityAudit();
app.MapAccessibilityDiagnostics(); // /_accessibility dashboard
```

```html
@addTagHelper *, Mostlylucid.LlmAccessibilityAuditor

<!-- Inline display -->
<accessibility-warnings inline="true" min-severity="Serious" />
```

[Full documentation](Mostlylucid.LlmAccessibilityAuditor/README.md)

---

## Mostlylucid.LlmLogSummarizer

AI-powered log summarization using local LLMs. Clusters similar exceptions, identifies error patterns, and generates human-readable digests.

### Features

- **Background Service**: Nightly/periodic summarization
- **Multiple Sources**: Serilog JSON, plain text, Azure Application Insights
- **Exception Clustering**: Fingerprinting and Levenshtein distance
- **LLM Summarization**: Local Ollama for privacy
- **Multiple Outputs**: Markdown, Email, Slack webhooks, custom webhooks
- **Trend Analysis**: New error types, increasing/decreasing rates

### Quick Start

```csharp
// Minimal setup
builder.Services.AddLlmLogSummarizer(
    serilogPath: "logs/*.json",
    outputDirectory: "./logs/summaries");

// Full configuration
builder.Services.AddLlmLogSummarizer(options =>
{
    options.SummarizationInterval = TimeSpan.FromHours(24);
    options.DailyRunTime = TimeSpan.FromHours(2); // 2 AM

    options.Sources.SerilogFiles.Add(new SerilogSourceConfig
    {
        Name = "Application",
        Path = "logs/*.json"
    });

    options.Ollama.Endpoint = "http://localhost:11434";
    options.Ollama.Model = "llama3.2:3b";

    options.Output.Slack = new SlackOutputConfig
    {
        Enabled = true,
        WebhookUrl = "https://hooks.slack.com/..."
    };
});
```

[Full documentation](Mostlylucid.LlmLogSummarizer/README.md)

---

## Mostlylucid.LlmSeoMetadata

AI-powered SEO metadata generation using local LLMs via Ollama.

### Features

- **Meta Description Generation**: SEO-optimized descriptions
- **OpenGraph Tags**: og:title, og:description, og:type, Twitter Cards
- **JSON-LD Structured Data**: Article, BlogPosting, Product, Service, Event, Recipe, FAQPage, HowTo
- **Two Modes**: Design-time templates or runtime generation
- **Database Caching**: SQLite or PostgreSQL
- **TagHelper**: Easy Razor integration

### Quick Start

```csharp
// Program.cs
builder.Services.AddSeoMetadata(options =>
{
    options.OllamaEndpoint = "http://localhost:11434";
    options.Model = "llama3.2:3b";
    options.SiteName = "My Blog";
    options.TwitterSite = "@myblog";
});

// Generate metadata
var request = new GenerationRequest
{
    Content = new ContentInput
    {
        Title = post.Title,
        Content = post.Body,
        ContentType = SeoContentType.BlogPosting,
        Url = $"https://myblog.com/posts/{slug}"
    }
};

var result = await _seoService.GenerateMetadataAsync(request);
```

```html
@addTagHelper *, Mostlylucid.LlmSeoMetadata

<head>
    <seo-metadata
        title="@Model.Title"
        content="@Model.Content"
        content-type="BlogPosting"
        url="@Model.Url" />
</head>
```

[Full documentation](Mostlylucid.LlmSeoMetadata/README.md)

---

## Mostlylucid.LlmSlideTranslator

RAG-assisted translation for long documents using small local LLMs with sliding-window context.

### Features

- **RAG-Enhanced Translation**: Maintains terminology consistency across documents
- **Sliding Window Context**: Previous translated block always included
- **Multiple Methods**: RAG+LLM, LLM only, NMT+LLM, NMT only
- **Vector Store Support**: File-based or Qdrant database
- **Markdown-Aware Chunking**: Preserves document structure
- **Real-time Progress**: SignalR streaming updates
- **Cross-Document Learning**: Experimental mode for book series/documentation sets

### Quick Start

```csharp
// Program.cs
builder.Services.AddLlmSlideTranslator(builder.Configuration);

// Translate document
var result = await translator.TranslateAsync(
    markdown: "# My Document\n\nThis is content to translate...",
    documentId: "doc_001",
    sourceLanguage: "en",
    targetLanguage: "de",
    method: TranslationMethod.RagLlm
);

Console.WriteLine(result.GetTranslatedText());
```

**Why Context Preservation Matters:**
```
Without context:
- Block 1: "The mayor announced..." -> "Der Bürgermeister..."
- Block 15: "...the mayor spoke..." -> "...der Oberbürgermeister..."  // Different!

With RAG + sliding window:
- All blocks consistently use "Bürgermeister"
```

[Full documentation](mostlylucid.llmslidetranslator/README.md)

---

## Mostlylucid.LlmI18nAssistant

LLM-assisted localization helper for short-string app resources. Complements LlmSlideTranslator for UI copy.

### Features

- **Resource File Support**: `.resx` (XML) and JSON files
- **Key Stability**: Only values transformed, keys unchanged
- **LLM + NMT Combo**: Local LLM with optional NMT baseline
- **Consistency Mode**: RAG over translations/glossary
- **Format Preservation**: Placeholders (`{0}`), HTML tags maintained
- **CLI Tool**: `dotnet tool` for offline generation
- **API Endpoint**: Minimal API for on-demand translation

### Quick Start

```csharp
// Program.cs
builder.Services.AddLlmI18nAssistant(builder.Configuration);

// Translate resource file
var result = await i18nAssistant.TranslateResourceFileAsync(
    filePath: "Resources/Strings.resx",
    sourceLanguage: "en",
    targetLanguages: ["de", "fr", "es"],
    options: new TranslationOptions
    {
        UseConsistencyMode = true,
        PreserveFormatStrings = true
    });

// Save translated files
foreach (var translation in result.Translations)
{
    await translation.SaveAsync($"Resources/Strings.{translation.Language}.resx");
}
```

```bash
# CLI usage
llm-i18n translate Resources/Strings.resx --source en --target de,fr,es
```

[Full documentation](mostlylucid.llmi18nassistant/README.md)

---

## Mostlylucid.RagLlmSearch

RAG-enabled AI chat with multiple search provider support, conversation history, and real-time streaming via SignalR.

### Features

- **Multiple Search Providers**: DuckDuckGo (free), Brave, Tavily, SerpApi
- **Ollama LLM Integration**: Local chat and embeddings
- **RAG (Retrieval Augmented Generation)**: SQLite vector storage with cosine similarity
- **Conversation History**: Persistent storage with full message history
- **SignalR Streaming**: Real-time response streaming
- **Fact Checking**: Automatic web search for current information
- **Source Citations**: All responses include source references

### Quick Start

```csharp
// Program.cs
builder.Services.AddRagLlmSearch(
    options =>
    {
        options.OllamaEndpoint = "http://localhost:11434";
        options.ChatModel = "llama3.2";
        options.EmbeddingModel = "nomic-embed-text";
    },
    searchProviders =>
    {
        searchProviders.DefaultProvider = SearchProviderType.DuckDuckGo;
        // Optional: configure other providers with API keys
        searchProviders.Brave.ApiKey = "your-brave-api-key";
    });

await app.InitializeRagLlmSearchAsync();
app.MapChatHub("/chathub");

// Direct service usage
var response = await chatService.ChatAsync(new ChatRequest
{
    Message = "What's the latest news on AI?",
    EnableWebSearch = true,
    EnableRag = true
});
```

```javascript
// SignalR client
const connection = new signalR.HubConnectionBuilder()
    .withUrl('/chathub')
    .build();

connection.on('ReceiveChunk', (chunk) => {
    console.log(chunk.content);
    if (chunk.isFinal) console.log('Sources:', chunk.sources);
});

await connection.start();
await connection.invoke('SendMessage', { message: "Hello!" });
```

| Provider | Free Tier | API Key |
|----------|-----------|---------|
| **DuckDuckGo** | Unlimited | None |
| **Brave** | 2000/month | Required |
| **Tavily** | Limited | Required |
| **SerpApi** | 100/month | Required |

[Full documentation](Mostlylucid.RagLlmSearch/README.md)

---

## Prerequisites

Most LLM-powered packages require [Ollama](https://ollama.ai/) running locally:

```bash
# Install Ollama
curl -fsSL https://ollama.com/install.sh | sh

# Pull recommended models
ollama pull llama3.2:3b          # For text analysis
ollama pull nomic-embed-text     # For embeddings (translation packages)

# Start the server
ollama serve
```

---

## Project Structure

```
mostlylucid.nugetpackages/
├── Mostlylucid.Common/                    # Shared utilities
│
├── Mostlylucid.BotDetection/              # Bot detection
├── Mostlylucid.BotDetection.Test/
├── Mostlylucid.BotDetection.Demo/
│
├── Mostlylucid.GeoDetection/              # Geo detection
├── Mostlylucid.GeoDetection.Test/
├── Mostlylucid.GeoDetection.Demo/
│
├── Mostlylucid.LlmAltText/                # Alt text generation
├── Mostlylucid.LlmAltText.Test/
├── Mostlylucid.AltText.Demo/
│
├── Mostlylucid.LlmPiiRedactor/            # PII redaction
├── Mostlylucid.LlmPiiRedactor.Tests/
├── Mostlylucid.LlmPiiRedactor.Demo/
│
├── Mostlylucid.LLMContentModeration/      # Content moderation
├── Mostlylucid.LLMContentModeration.Test/
├── Mostlylucid.LLMContentModeration.Demo/
│
├── Mostlylucid.LlmAccessibilityAuditor/   # Accessibility auditing
├── Mostlylucid.LlmAccessibilityAuditor.Test/
├── Mostlylucid.LlmAccessibilityAuditor.Demo/
│
├── Mostlylucid.LlmLogSummarizer/          # Log summarization
├── Mostlylucid.LlmLogSummarizer.Test/
├── Mostlylucid.LlmLogSummarizer.Demo/
│
├── Mostlylucid.LlmSeoMetadata/            # SEO metadata
├── Mostlylucid.LlmSeoMetadata.Test/
├── Mostlylucid.LlmSeoMetadata.Demo/
│
├── mostlylucid.llmslidetranslator/        # Document translation
├── mostlylucid.llmslidetranslator.Demo/
│
├── mostlylucid.llmi18nassistant/          # Resource localization
├── mostlylucid.llmi18nassistant.cli/      # CLI tool
├── mostlylucid.llmi18nassistant.demo/
│
├── Mostlylucid.RagLlmSearch/              # RAG-enabled chat
└── Mostlylucid.RagLlmSearch.Demo/
```

---

## Contributing

Contributions are welcome! Please feel free to submit issues and pull requests.

## Author

Scott Galloway - [mostlylucid.net](https://mostlylucid.net)
