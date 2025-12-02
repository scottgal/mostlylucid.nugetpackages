# Mostlylucid.BotDetection

> **Note**: This package is provided as-is under the [Unlicense](https://unlicense.org/) license.
> Feel free to use, modify, and distribute without restriction.

Bot detection middleware for ASP.NET Core applications with behavioral analysis, header inspection, IP-based detection, and optional LLM-based classification.

[![NuGet](https://img.shields.io/nuget/v/mostlylucid.botdetection.svg)](https://www.nuget.org/packages/mostlylucid.botdetection)

## Features

- **Multi-Strategy Detection**: Combines multiple detection methods for accuracy
- **User-Agent Detection**: Matches against known bot signatures and patterns
- **Header Analysis**: Inspects HTTP headers for suspicious patterns
- **IP Detection**: Checks against known bot IP ranges and datacenter blocklists
- **Behavioral Analysis**: Monitors request patterns for bot-like behavior
- **LLM Detection** (Optional): Uses Ollama for advanced AI-based classification
- **Auto-Updating Blocklists**: Background service updates bot signatures from authoritative sources
- **Configurable Responses**: Block, rate-limit, or just detect and log
- **Caching**: Results cached for performance
- **OpenTelemetry**: Built-in observability and tracing support

## Installation

```bash
dotnet add package Mostlylucid.BotDetection
```

## Quick Start

### 1. Configure Services

```csharp
using Mostlylucid.BotDetection.Extensions;

var builder = WebApplication.CreateBuilder(args);

// Add bot detection services (all detection methods enabled by default)
builder.Services.AddBotDetection();

var app = builder.Build();

// Use bot detection middleware
app.UseBotDetection();

app.Run();
```

### 2. Configuration via appsettings.json

```json
{
  "BotDetection": {
    "EnableUserAgentDetection": true,
    "EnableHeaderAnalysis": true,
    "EnableIpDetection": true,
    "EnableBehavioralAnalysis": true,
    "EnableLlmDetection": false,
    "EnableTestMode": false,
    "BotThreshold": 0.7,
    "CacheDurationSeconds": 300,
    "MaxRequestsPerMinute": 60,
    "OllamaEndpoint": "http://localhost:11434",
    "OllamaModel": "qwen2.5:1.5b",
    "LlmTimeoutMs": 2000
  }
}
```

## Detection Strategies

### 1. User-Agent Detection

Matches User-Agent strings against known bot patterns from multiple sources:

- Search engine bots (Googlebot, Bingbot, DuckDuckBot, etc.)
- Social media crawlers (FacebookBot, Twitterbot, LinkedInBot)
- SEO tools (AhrefsBot, SEMrushBot, MajesticBot)
- Scrapers and automation tools
- Known malicious bots

### 2. Header Detection

Analyzes HTTP headers for suspicious patterns:

- Missing standard browser headers
- Inconsistent Accept headers
- Missing or invalid Accept-Language
- Suspicious Connection headers
- Known bot header signatures

### 3. IP Detection

Checks client IP against:

- Known datacenter IP ranges (AWS, Azure, GCP, Oracle Cloud)
- Cloud provider ranges (auto-updated)
- Cloudflare IP ranges

### 4. Behavioral Analysis

Monitors request patterns:

- Request frequency (requests per minute per IP)
- Threshold-based detection (configurable via `MaxRequestsPerMinute`)

### 5. LLM Detection (Optional)

Uses Ollama with a small LLM to analyze request patterns for advanced classification:

```csharp
builder.Services.AddAdvancedBotDetection(
    ollamaEndpoint: "http://localhost:11434",
    model: "qwen2.5:1.5b"
);
```

## Service Registration Methods

The package provides several convenience methods for different use cases:

```csharp
// Full detection (default) - all heuristics, no LLM
builder.Services.AddBotDetection();

// Simple detection - user-agent only (fastest)
builder.Services.AddSimpleBotDetection();

// Comprehensive detection - all heuristics, no LLM
builder.Services.AddComprehensiveBotDetection();

// Advanced detection - all heuristics + LLM (requires Ollama)
builder.Services.AddAdvancedBotDetection("http://localhost:11434", "qwen2.5:1.5b");
```

## Configuration Options

| Option                     | Type       | Default                    | Valid Range     | Description                                     |
|----------------------------|------------|----------------------------|-----------------|------------------------------------------------|
| `EnableUserAgentDetection` | bool       | `true`                     | -               | Enable user-agent pattern matching              |
| `EnableHeaderAnalysis`     | bool       | `true`                     | -               | Enable HTTP header inspection                   |
| `EnableIpDetection`        | bool       | `true`                     | -               | Enable IP-based detection                       |
| `EnableBehavioralAnalysis` | bool       | `true`                     | -               | Enable behavioral rate analysis                 |
| `EnableLlmDetection`       | bool       | `false`                    | -               | Enable LLM-based classification (needs Ollama)  |
| `EnableTestMode`           | bool       | `false`                    | -               | Enable test mode header processing              |
| `BotThreshold`             | double     | `0.7`                      | 0.0 - 1.0       | Minimum confidence to classify as bot           |
| `CacheDurationSeconds`     | int        | `300`                      | 0 - 86400       | Cache duration for detection results            |
| `MaxRequestsPerMinute`     | int        | `60`                       | 1 - 10000       | Threshold for behavioral analysis               |
| `OllamaEndpoint`           | string     | `"http://localhost:11434"` | -               | Ollama API endpoint                             |
| `OllamaModel`              | string     | `"qwen2.5:1.5b"`           | -               | Ollama model for LLM detection                  |
| `LlmTimeoutMs`             | int        | `2000`                     | 100 - 30000     | LLM request timeout in milliseconds             |
| `WhitelistedBotPatterns`   | List       | Common good bots           | -               | Bot patterns to allow (Googlebot, etc.)         |
| `DatacenterIpPrefixes`     | List       | AWS/Azure/GCP/Oracle       | -               | Known datacenter IP CIDR ranges                 |

### Default Whitelisted Bots

```csharp
"Googlebot", "Bingbot", "Slackbot", "DuckDuckBot", "Baiduspider",
"YandexBot", "Sogou", "Exabot", "facebot", "ia_archiver"
```

### Default Datacenter IP Prefixes

```csharp
"3.0.0.0/8", "13.0.0.0/8", "18.0.0.0/8", "52.0.0.0/8",   // AWS
"20.0.0.0/8", "40.0.0.0/8", "104.0.0.0/8",               // Azure
"34.0.0.0/8", "35.0.0.0/8",                               // GCP
"138.0.0.0/8", "139.0.0.0/8", "140.0.0.0/8"              // Oracle Cloud
```

## Usage

### Getting Detection Results in Controllers

```csharp
using Mostlylucid.BotDetection.Extensions;

public class MyController : Controller
{
    public IActionResult Index()
    {
        var result = HttpContext.GetBotDetectionResult();

        if (result?.IsBot == true)
        {
            _logger.LogWarning("Bot detected: {BotType}, Confidence: {Confidence:F2}",
                result.BotType, result.ConfidenceScore);
        }

        return View();
    }
}
```

### Using the Service Directly

```csharp
public class MyService
{
    private readonly IBotDetectionService _botDetection;

    public MyService(IBotDetectionService botDetection)
    {
        _botDetection = botDetection;
    }

    public async Task<bool> IsBotAsync(HttpContext context)
    {
        var result = await _botDetection.DetectAsync(context);
        return result.IsBot;
    }
}
```

### Access Detection Result from Middleware

```csharp
using Mostlylucid.BotDetection.Middleware;

public class MyMiddleware
{
    private readonly RequestDelegate _next;

    public async Task InvokeAsync(HttpContext context)
    {
        // Detection result is stored in HttpContext.Items after middleware runs
        var result = context.Items[BotDetectionMiddleware.BotDetectionResultKey]
            as BotDetectionResult;

        if (result?.IsBot == true)
        {
            context.Response.Headers.Append("X-Custom-Bot-Header", "true");
        }

        await _next(context);
    }
}
```

## Blocking Bots

### Using Attributes (MVC Controllers)

```csharp
// Block all bots
[BlockBots]
public IActionResult SensitiveData() { ... }

// Block bots except verified ones (Googlebot, etc.)
[BlockBots(AllowVerifiedBots = true)]
public IActionResult PublicData() { ... }

// Block bots except search engines
[BlockBots(AllowSearchEngines = true)]
public IActionResult Indexable() { ... }

// Only block high-confidence detections
[BlockBots(MinConfidence = 0.9)]
public IActionResult ModerateProtection() { ... }

// Custom status code and message
[BlockBots(StatusCode = 429, Message = "Too many requests")]
public IActionResult RateLimited() { ... }
```

### Using Attributes (Allow Bots)

```csharp
// Explicitly allow all bots on this endpoint
[AllowBots]
public IActionResult RobotsFile() { ... }

// Only allow verified bots
[AllowBots(OnlyVerified = true)]
public IActionResult Sitemap() { ... }
```

### Requiring Human Visitors

```csharp
// Blocks ALL bots including verified ones
[RequireHuman]
public IActionResult SubmitForm() { ... }

[RequireHuman(StatusCode = 403, Message = "Human verification required")]
public IActionResult SecureAction() { ... }
```

### Minimal API Endpoint Filters

```csharp
// Block all bots
app.MapGet("/api/data", () => "sensitive data")
   .BlockBots();

// Block bots with options
app.MapGet("/api/protected", () => "protected")
   .BlockBots(allowVerifiedBots: true, allowSearchEngines: false, minConfidence: 0.8);

// Require human visitors
app.MapPost("/api/submit", () => "submitted")
   .RequireHuman();
```

## Diagnostic Endpoints

Map built-in diagnostic endpoints for monitoring:

```csharp
app.MapBotDetectionEndpoints("/bot-detection");

// Creates:
//   GET /bot-detection/check   - Check current request
//   GET /bot-detection/stats   - Get detection statistics
//   GET /bot-detection/health  - Health check
```

### Statistics Response Example

```json
{
  "totalRequests": 1000,
  "botsDetected": 150,
  "botPercentage": 15.0,
  "verifiedBots": 50,
  "maliciousBots": 10,
  "averageProcessingTimeMs": 2.5,
  "botTypeBreakdown": {
    "SearchEngine": 50,
    "SocialMediaBot": 30,
    "Scraper": 60,
    "MaliciousBot": 10
  }
}
```

## Detection Result Model

```csharp
public class BotDetectionResult
{
    // Whether classified as a bot (confidence > threshold)
    public bool IsBot { get; set; }

    // Confidence score (0.0 to 1.0)
    public double ConfidenceScore { get; set; }

    // Type of bot detected
    public BotType? BotType { get; set; }

    // Identified bot name (e.g., "Googlebot", "AhrefsBot")
    public string? BotName { get; set; }

    // Detailed reasons for detection
    public List<DetectionReason> Reasons { get; set; }

    // Processing time in milliseconds
    public long ProcessingTimeMs { get; set; }
}

public enum BotType
{
    Unknown,
    SearchEngine,      // Google, Bing, DuckDuckGo, etc.
    SocialMediaBot,    // Facebook, Twitter, LinkedIn, etc.
    MonitoringBot,     // Uptime monitors, health checks
    Scraper,           // Web scrapers and crawlers
    MaliciousBot,      // Known bad actors
    GoodBot,           // Generally beneficial bots
    VerifiedBot        // Verified legitimate bots
}
```

## Test Mode

For development and testing, enable test mode to simulate bot detection:

```csharp
builder.Services.AddBotDetection(options =>
{
    options.EnableTestMode = true; // WARNING: Only enable in development!
});
```

Use the `ml-bot-test-mode` header to simulate detection results:

| Header Value  | Result                                    |
|---------------|-------------------------------------------|
| `disable`     | Bypasses all detection (returns human)    |
| `human`       | Simulates human traffic                   |
| `bot`         | Simulates generic bot detection           |
| `googlebot`   | Simulates Googlebot (SearchEngine type)   |
| `bingbot`     | Simulates Bingbot (SearchEngine type)     |
| `scraper`     | Simulates scraper bot                     |
| `malicious`   | Simulates malicious bot                   |
| `social`      | Simulates social media bot                |
| `monitor`     | Simulates monitoring bot                  |
| `<any-other>` | Creates generic bot with given name       |

```bash
# Test with curl
curl -H "ml-bot-test-mode: googlebot" https://localhost:5001/api/data
```

> **Security Note**: Test mode headers are only processed when `EnableTestMode` is `true`.
> In production, the header is completely ignored to prevent information leakage.

## Auto-Updating Bot Lists

The package includes a background service that automatically updates bot signatures every 24 hours from authoritative sources.

### External Data Sources

The package fetches bot detection data from these sources:

| Source | URL | Description |
|--------|-----|-------------|
| Matomo Device Detector | [bots.yml](https://raw.githubusercontent.com/matomo-org/device-detector/master/regexes/bots.yml) | 1000+ bot patterns with categories |
| Crawler User Agents | [crawler-user-agents](https://raw.githubusercontent.com/monperrus/crawler-user-agents/master/crawler-user-agents.json) | Community-maintained crawler list |
| AWS IP Ranges | [ip-ranges.json](https://ip-ranges.amazonaws.com/ip-ranges.json) | Official AWS IP ranges |
| Google Cloud IP Ranges | [cloud.json](https://www.gstatic.com/ipranges/cloud.json) | Official GCP IP ranges |
| Cloudflare IP Ranges | [ips-v4](https://www.cloudflare.com/ips-v4) / [ips-v6](https://www.cloudflare.com/ips-v6) | Cloudflare CDN ranges |
| ISBot Patterns | [list.json](https://unpkg.com/isbot@latest/src/list.json) | From the popular isbot npm package |

If external sources are unavailable, the package falls back to embedded static lists.

## OpenTelemetry Integration

The middleware automatically emits telemetry for monitoring:

```csharp
builder.Services.AddOpenTelemetry()
    .WithTracing(tracing =>
    {
        tracing.AddSource("Mostlylucid.BotDetection");
    });
```

### Activity Tags

| Tag | Description |
|-----|-------------|
| `http.client_ip` | Client IP address |
| `http.user_agent` | User-Agent header |
| `mostlylucid.botdetection.is_bot` | Whether detected as bot |
| `mostlylucid.botdetection.confidence` | Confidence score |
| `mostlylucid.botdetection.bot_type` | Type of bot |
| `mostlylucid.botdetection.bot_name` | Identified bot name |
| `mostlylucid.botdetection.processing_time_ms` | Processing time |
| `mostlylucid.botdetection.reason_count` | Number of detection reasons |

## Troubleshooting

### Bot Detection Not Running

Ensure the middleware is added to the pipeline:

```csharp
app.UseBotDetection();  // Must be called before UseRouting()
```

### LLM Detection Not Working

1. Ensure Ollama is running: `ollama serve`
2. Check the model is available: `ollama list`
3. Verify endpoint and model settings:

```csharp
builder.Services.AddBotDetection(options =>
{
    options.EnableLlmDetection = true;
    options.OllamaEndpoint = "http://localhost:11434";
    options.OllamaModel = "qwen2.5:1.5b";
    options.LlmTimeoutMs = 5000;  // Increase if timing out
});
```

### False Positives

Adjust the threshold or whitelist patterns:

```csharp
builder.Services.AddBotDetection(options =>
{
    options.BotThreshold = 0.9;  // Higher threshold = fewer false positives
    options.WhitelistedBotPatterns.Add("MyTrustedBot");
});
```

### Options Validation Errors

The package validates configuration on startup. Common errors:

- `BotThreshold must be between 0.0 and 1.0`
- `LlmTimeoutMs must be between 100 and 30000`
- `MaxRequestsPerMinute must be between 1 and 10000`
- `CacheDurationSeconds must be between 0 and 86400`
- `Invalid CIDR notation in DatacenterIpPrefixes`
- `OllamaEndpoint must be specified when LLM detection is enabled`

### Performance Issues

- Use caching (default 5 minutes)
- Disable LLM detection for high-traffic endpoints
- Use `AddSimpleBotDetection()` for fastest detection

## Requirements

- **.NET 8.0** or **.NET 9.0**
- **Optional**: [Ollama](https://ollama.ai/) for LLM-based detection

## License

[The Unlicense](https://unlicense.org/) - Public Domain. Free and unencumbered software released into the public domain.

## Links

- [GitHub Repository](https://github.com/scottgal/mostlylucid.nugetpackages/tree/main/Mostlylucid.BotDetection)
- [NuGet Package](https://www.nuget.org/packages/mostlylucid.botdetection/)
- [Ollama](https://ollama.ai/) - Local LLM runtime

## Related Packages

- [Mostlylucid.SentimentAnalysis](https://www.nuget.org/packages/mostlylucid.sentimentanalysis) - ONNX-based sentiment analysis

## External Resources

- [Matomo Device Detector](https://github.com/matomo-org/device-detector) - Bot pattern source
- [crawler-user-agents](https://github.com/monperrus/crawler-user-agents) - Crawler patterns
- [isbot](https://github.com/omrilotan/isbot) - JavaScript bot detection library
