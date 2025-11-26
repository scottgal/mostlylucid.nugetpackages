# Mostlylucid.BotDetection

> **Note**: These packages are provided as-is. I'll get them working good enough to release but I can't commit to
> support. However they are Unlicense so have at it!

Bot detection middleware for ASP.NET Core applications with behavioral analysis, header inspection, IP-based detection,
and optional LLM-based classification.

## Features

- **Multi-Strategy Detection**: Combines multiple detection methods for accuracy
- **User-Agent Detection**: Matches against known bot signatures and patterns
- **Header Analysis**: Inspects HTTP headers for suspicious patterns
- **IP Detection**: Checks against known bot IP ranges and blocklists
- **Behavioral Analysis**: Monitors request patterns for bot-like behavior
- **LLM Detection** (Optional): Uses Ollama for advanced AI-based classification
- **Auto-Updating Blocklists**: Background service updates bot signatures automatically
- **Configurable Responses**: Block, rate-limit, or just detect and log
- **Caching**: Results cached for performance

## Installation

```bash
dotnet add package Mostlylucid.BotDetection
```

## Quick Start

### 1. Configure Services

```csharp
using Mostlylucid.BotDetection.Extensions;

var builder = WebApplication.CreateBuilder(args);

// Add bot detection services
builder.Services.AddBotDetection(options =>
{
    options.EnableBehavioralAnalysis = true;
    options.EnableHeaderDetection = true;
    options.EnableUserAgentDetection = true;
    options.EnableIpDetection = true;
    options.EnableLlmDetection = false; // Optional: requires Ollama
    options.BlockBots = false;          // Just detect, don't block
});

var app = builder.Build();

// Use bot detection middleware
app.UseBotDetection();

app.Run();
```

### 2. Configuration via appsettings.json

```json
{
  "BotDetection": {
    "EnableBehavioralAnalysis": true,
    "EnableHeaderDetection": true,
    "EnableUserAgentDetection": true,
    "EnableIpDetection": true,
    "EnableLlmDetection": false,
    "EnableTestMode": false,
    "BotThreshold": 0.7,
    "CacheDurationSeconds": 300,
    "MaxRequestsPerMinute": 60,
    "OllamaEndpoint": "http://localhost:11434",
    "OllamaModel": "llama3.2:3b",
    "LlmTimeoutMs": 5000
  }
}
```

## Detection Strategies

### 1. User-Agent Detection

Matches User-Agent strings against known bot patterns:

- Search engine bots (Googlebot, Bingbot, etc.)
- Social media crawlers (FacebookBot, Twitterbot)
- SEO tools (AhrefsBot, SEMrushBot)
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

- Known bot IP ranges
- Data center IP blocks
- Cloud provider ranges
- Blocklist services

### 4. Behavioral Analysis

Monitors request patterns:

- Request frequency (requests per minute)
- Request timing patterns
- Resource access patterns
- Session behavior

### 5. LLM Detection (Optional)

Uses Ollama with a small LLM to analyze request patterns:

```csharp
builder.Services.AddBotDetection(options =>
{
    options.EnableLlmDetection = true;
    options.OllamaEndpoint = "http://localhost:11434";
    options.OllamaModel = "qwen2.5:1.5b"; // Small, fast model
    options.LlmTimeoutMs = 5000;
});
```

## Usage

### Getting Detection Results

```csharp
public class MyController : Controller
{
    private readonly IBotDetectionService _botDetection;
    private readonly ILogger<MyController> _logger;

    public MyController(IBotDetectionService botDetection, ILogger<MyController> logger)
    {
        _botDetection = botDetection;
        _logger = logger;
    }

    public async Task<IActionResult> Index()
    {
        var result = await _botDetection.DetectAsync(HttpContext);

        if (result.IsBot)
        {
            _logger.LogWarning("Bot detected: {BotType}, Confidence: {Confidence:F2}",
                result.BotType, result.ConfidenceScore);

            // Handle bot traffic (optional - middleware handles detection automatically)
            return StatusCode(403);
        }

        return View();
    }
}
```

### Access Detection Result from HttpContext

```csharp
using Mostlylucid.BotDetection.Middleware;
using Mostlylucid.BotDetection.Models;

public class MyMiddleware
{
    private readonly RequestDelegate _next;

    public MyMiddleware(RequestDelegate next)
    {
        _next = next;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        // Detection result is stored in HttpContext.Items after middleware runs
        var result = context.Items[BotDetectionMiddleware.BotDetectionResultKey] as BotDetectionResult;

        if (result?.IsBot == true)
        {
            // Log or handle bot traffic
            context.Response.Headers.Append("X-Custom-Bot-Header", "true");
        }

        await _next(context);
    }
}
```

### Automatic Blocking

```csharp
builder.Services.AddBotDetection(options =>
{
    options.BlockBots = true;
    options.BotBlockStatusCode = 403;
    options.MinConfidenceToBlock = 0.8; // Only block high-confidence detections
});
```

## Configuration Options

| Option                      | Type   | Default                    | Description                              |
|-----------------------------|--------|----------------------------|------------------------------------------|
| `EnableBehavioralAnalysis`  | bool   | `true`                     | Enable behavioral analysis               |
| `EnableHeaderDetection`     | bool   | `true`                     | Enable header inspection                 |
| `EnableUserAgentDetection`  | bool   | `true`                     | Enable user-agent matching               |
| `EnableIpDetection`         | bool   | `true`                     | Enable IP-based detection                |
| `EnableLlmDetection`        | bool   | `false`                    | Enable LLM-based classification          |
| `EnableTestMode`            | bool   | `false`                    | Enable test mode header processing       |
| `BotThreshold`              | double | `0.7`                      | Minimum confidence to classify as bot    |
| `CacheDurationSeconds`      | int    | `300`                      | Cache duration for detection results     |
| `MaxRequestsPerMinute`      | int    | `60`                       | Threshold for behavioral analysis        |
| `OllamaEndpoint`            | string | `"http://localhost:11434"` | Ollama API endpoint                      |
| `OllamaModel`               | string | `"llama3.2:3b"`            | Ollama model for LLM detection           |
| `LlmTimeoutMs`              | int    | `5000`                     | LLM request timeout in milliseconds      |
| `WhitelistedBotPatterns`    | list   | Common good bots           | Bot patterns to allow (Googlebot, etc.)  |
| `DatacenterIpPrefixes`      | list   | AWS/Azure/GCP ranges       | Known datacenter IP CIDR ranges          |

## Detection Result

```csharp
public class BotDetectionResult
{
    // Overall detection result
    public bool IsBot { get; set; }

    // Confidence score (0.0 - 1.0)
    public double ConfidenceScore { get; set; }

    // Type of bot detected (nullable)
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
    GoodBot,           // Generally beneficial bots
    VerifiedBot,       // Verified legitimate bots (e.g., verified Googlebot)
    MaliciousBot       // Known bad actors and attackers
}
```

## Background Services

The package includes a background service that automatically updates bot signatures:

```csharp
// Signatures are updated automatically every 24 hours
// Sources include:
// - Known bot user-agents
// - IP blocklists
// - Crawler databases
```

### Manual Update

```csharp
public class AdminController : Controller
{
    private readonly IBotListUpdateService _updateService;

    public AdminController(IBotListUpdateService updateService)
    {
        _updateService = updateService;
    }

    public async Task<IActionResult> UpdateBotLists()
    {
        await _updateService.UpdateAsync();
        return Ok("Bot lists updated");
    }
}
```

## Test Mode

For development and testing, you can enable test mode to simulate bot detection without actual analysis:

```csharp
builder.Services.AddBotDetection(options =>
{
    options.EnableTestMode = true; // Only enable in development!
});
```

When test mode is enabled, use the `ml-bot-test-mode` header to simulate detection results:

| Header Value      | Result                                           |
|-------------------|--------------------------------------------------|
| `disable`         | Bypasses all detection (returns human)           |
| `human`           | Simulates human traffic                          |
| `bot`             | Simulates generic bot detection                  |
| `googlebot`       | Simulates Googlebot (SearchEngine type)          |
| `bingbot`         | Simulates Bingbot (SearchEngine type)            |
| `scraper`         | Simulates scraper bot                            |
| `malicious`       | Simulates malicious bot                          |
| `social`          | Simulates social media bot                       |
| `monitor`         | Simulates monitoring bot                         |
| `<any-other>`     | Creates generic bot with given name              |

Example:
```bash
# Test with curl
curl -H "ml-bot-test-mode: googlebot" https://localhost:5001/api/data
```

> **Security Note**: Test mode headers are only processed when `EnableTestMode` is `true`.
> In production, the header is completely ignored to prevent information leakage.

## OpenTelemetry Integration

The middleware automatically emits telemetry for monitoring:

```csharp
// Activities are created under "Mostlylucid.BotDetection" source
// Tags include:
// - mostlylucid.botdetection.is_bot
// - mostlylucid.botdetection.confidence
// - mostlylucid.botdetection.bot_type
// - mostlylucid.botdetection.bot_name
// - mostlylucid.botdetection.cache_hit
// - mostlylucid.botdetection.detector_count
```

Configure your OpenTelemetry setup to capture these traces:

```csharp
builder.Services.AddOpenTelemetry()
    .WithTracing(tracing =>
    {
        tracing.AddSource("Mostlylucid.BotDetection");
    });
```

## Advanced Usage

### Custom Detection Logic

```csharp
builder.Services.AddBotDetection(options =>
{
    // Add custom user-agent patterns
    options.CustomBotPatterns.Add(@"MyCustomBot\/\d+");

    // Add custom IP ranges to blocklist
    options.BlockedIpRanges.Add("192.168.0.0/16");

    // Whitelist specific bots
    options.WhitelistedUserAgents.Add("MyTrustedBot");
});
```

### Endpoint-Specific Detection

```csharp
app.MapGet("/api/data", async (HttpContext context, IBotDetectionService botDetection) =>
{
    var result = await botDetection.DetectAsync(context);

    if (result.IsBot && result.BotType != BotType.SearchEngine)
    {
        return Results.StatusCode(403);
    }

    return Results.Ok(new { data = "sensitive data" });
});
```

## Requirements

- **.NET 8.0** or **.NET 9.0**
- **Optional**: Ollama for LLM-based detection

## License

Unlicense - Public Domain

## Links

- [GitHub Repository](https://github.com/scottgal/mostlylucidweb/tree/main/Mostlylucid.BotDetection)
- [NuGet Package](https://www.nuget.org/packages/Mostlylucid.BotDetection/)
