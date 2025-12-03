# Mostlylucid.BotDetection

**DESTROY ALL ROBOTS!** (politely, with HTTP 403s)

Bot detection middleware for ASP.NET Core with multi-signal detection (User-Agent, headers, IP, behavior, optional AI), auto-updated blocklists, YARP integration, and full observability.

[![NuGet](https://img.shields.io/nuget/v/mostlylucid.botdetection.svg)](https://www.nuget.org/packages/mostlylucid.botdetection)

## Why Use This?

**When commercial WAF (Web Application Firewall) isn't an option:**
- Self-hosted apps without Cloudflare/AWS/Azure
- Internal tools behind corporate firewalls
- Compliance requirements that prohibit third-party request inspection
- Cost-sensitive projects where $3K+/month WAF isn't justified

**When you need more than User-Agent matching:**
- Bots spoofing browser User-Agents
- Scripts that forget other signals (Accept-Language, cookies, timing)
- API abuse from datacenter IPs
- Web design scrapers cloning your site's CSS/HTML

**When you want app-level control:**
- Different policies per endpoint (block scrapers, allow Googlebot)
- Custom rate limits by API key or authenticated user
- Integration with your existing auth/routing (YARP, custom middleware)

**When you're learning or prototyping:**
- Understand bot detection techniques hands-on
- Build custom rules before committing to a vendor
- Augment existing protection with application-specific logic

## Positioning

A self-hosted, app-level bot detection layer for ASP.NET Core, sitting between "regex-only NuGet packages" and full CDN/WAF products like Cloudflare Bot Management.

**Highlights:**
- Multi-signal detection: User-Agent + headers + IP ranges + behavioral analysis
- Optional AI detection: local ONNX (fast) or Ollama LLM (accurate)
- Auto-updated threat intel: pulls isbot patterns and cloud IP ranges
- First-class YARP support: bot-aware routing and headers
- Observability: OpenTelemetry traces and metrics baked in

> **Note**: For enterprise applications with stringent security requirements, consider commercial services like [Cloudflare Bot Management](https://www.cloudflare.com/products/bot-management/), [AWS WAF Bot Control](https://aws.amazon.com/waf/features/bot-control/), or [DataDome](https://datadome.co/).

## Quick Start

### 1. Install

```bash
dotnet add package Mostlylucid.BotDetection
```

### 2. Configure Services

```csharp
using Mostlylucid.BotDetection.Extensions;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddBotDetection();

var app = builder.Build();

app.UseBotDetection();
app.Run();
```

### 3. Configuration (appsettings.json)

```json
{
  "BotDetection": {
    "BotThreshold": 0.7
  }
}
```

That's it. All detection methods are enabled by default with sensible settings.

## Basic Usage

### HttpContext Extensions

```csharp
if (context.IsBot())
    return Results.StatusCode(403);

var confidence = context.GetBotConfidence();
var botType = context.GetBotType();
```

### Endpoint Filters

```csharp
app.MapGet("/api/data", () => "sensitive")
   .BlockBots();

app.MapPost("/api/submit", () => "ok")
   .RequireHuman();
```

### MVC Attributes

```csharp
[BlockBots(AllowVerifiedBots = true)]
public IActionResult Index() => View();
```

## Detection Methods

| Method | What it does |
|--------|--------------|
| **User-Agent** | Matches against known bot patterns |
| **Headers** | Inspects for suspicious/missing headers |
| **IP** | Checks datacenter IP ranges (AWS, GCP, Azure) |
| **Behavioral** | Rate limiting + pattern analysis |
| **Inconsistency** | Catches bots that spoof one signal but miss others |

## Advanced Features

For detailed documentation on advanced features:

| Feature | Description | Docs |
|---------|-------------|------|
| **Behavioral Analysis** | Per-IP, per-API-key, per-user rate limiting and anomaly detection | [behavioral-analysis.md](docs/behavioral-analysis.md) |
| **Client-Side Fingerprinting** | JavaScript-based headless browser detection | [client-side-fingerprinting.md](docs/client-side-fingerprinting.md) |
| **AI Detection** | ONNX (1-10ms) or Ollama LLM (50-500ms) classification | [ai-detection.md](docs/ai-detection.md) |
| **YARP Integration** | Bot-aware reverse proxy with header injection | [yarp-integration.md](docs/yarp-integration.md) |
| **Blocking & Filters** | Attributes, endpoint filters, risk bands | [blocking-and-filters.md](docs/blocking-and-filters.md) |
| **Configuration** | Full options reference | [configuration.md](docs/configuration.md) |
| **Data Sources** | Auto-updating bot lists and IP ranges | [data-sources.md](docs/data-sources.md) |
| **Telemetry** | OpenTelemetry traces and metrics | [telemetry-and-metrics.md](docs/telemetry-and-metrics.md) |

## Diagnostic Endpoints

```csharp
app.MapBotDetectionEndpoints("/bot-detection");

// GET /bot-detection/check   - Current request analysis
// GET /bot-detection/stats   - Detection statistics
// GET /bot-detection/health  - Health check
```

## Test Mode

For development only:

```json
{
  "BotDetection": {
    "EnableTestMode": true
  }
}
```

```bash
curl -H "ml-bot-test-mode: googlebot" https://localhost:5001/
```

## Service Registration Options

```csharp
// Default: all heuristics, no AI
builder.Services.AddBotDetection();

// User-agent only (fastest)
builder.Services.AddSimpleBotDetection();

// All heuristics + AI (requires Ollama)
builder.Services.AddAdvancedBotDetection("http://localhost:11434", "gemma3:1b");
```

## Requirements

- .NET 8.0 or .NET 9.0
- Optional: [Ollama](https://ollama.ai/) for LLM-based detection

## License

[The Unlicense](https://unlicense.org/) - Public Domain

## Links

- [GitHub](https://github.com/scottgal/mostlylucid.nugetpackages/tree/main/Mostlylucid.BotDetection)
- [NuGet](https://www.nuget.org/packages/mostlylucid.botdetection/)
- [Full Documentation](docs/)
