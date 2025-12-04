# Mostlylucid.YarpGateway

**Not just another reverse proxy.** This is a **behavioral router** with production-ready bot detection built-in.

## What Makes This Different

| Traditional Proxy | Behavioral Router |
|-------------------|-------------------|
| Routes by URL path | Routes by **who's asking** |
| Same backend for everyone | Different backends for bots vs humans |
| Block or allow | Block, throttle, honeypot, challenge, redirect |
| Static rules | **Learns and adapts** in real-time |

```
Scraper → Honeypot (fake data)
Suspicious → Throttled backend
Malicious → Blocked
Googlebot → Real backend (verified)
Human → Real backend
```

## Core Feature: Bot Detection

This isn't a wrapper around YARP with bot detection bolted on. **Bot detection IS the core feature.** YARP provides the routing infrastructure.

Integrates [Mostlylucid.BotDetection](https://www.nuget.org/packages/Mostlylucid.BotDetection) v1.0.0:

- **8-Layer Detection Pipeline** - User-Agent, headers, IP reputation, behavioral analysis, client-side fingerprinting, version age, inconsistency detection, AI classification
- **Sub-Millisecond Fast Path** - Most requests classified in <1ms
- **AI Escalation** - Uncertain cases escalate to heuristic model or LLM (Ollama)
- **Stealth Responses** - Bots don't know they're detected
- **Continuous Learning** - Adapts to new patterns automatically
- **Full Observability** - OpenTelemetry metrics and traces

### Detection → Action → Route

```
Request
   ↓
┌─────────────────────────────────────┐
│  Fast Path (<1ms)                   │
│  UA + Headers + IP + Fingerprint    │
└─────────────────────────────────────┘
   ↓
┌─────────────────────────────────────┐
│  Slow Path (if uncertain)           │
│  Behavioral + Inconsistency + AI    │
└─────────────────────────────────────┘
   ↓
   Risk Score (0.0 - 1.0)
   ↓
┌─────────────────────────────────────┐
│  Action Policy                      │
│  → Allow (real backend)             │
│  → Redirect (honeypot/cache)        │
│  → Throttle (slow response)         │
│  → Challenge (CAPTCHA)              │
│  → Block (403/custom)               │
└─────────────────────────────────────┘
```

## Future: More Behavioral Modules

Bot detection is v1.0. Additional detection modules coming:

- **Geo-blocking** - Route by country/region
- **Rate limiting** - Behavioral rate limits (not just IP-based)
- **Anomaly detection** - Unusual request patterns
- **API abuse detection** - Credential stuffing, enumeration attacks
- **Custom detectors** - Plugin architecture for your own logic

Each module feeds into the same behavioral routing decision.

---

## Quick Start

### Zero-Config

```bash
docker run -d -p 8080:8080 \
  -e DEFAULT_UPSTREAM=http://your-backend:3000 \
  scottgal/mostlylucid.yarpgateway
```

### With Bot Detection Config

```bash
docker run -d -p 8080:8080 \
  -v ./config:/app/config:ro \
  scottgal/mostlylucid.yarpgateway
```

## Bot-Aware Routing Example

**config/yarp.json** - Define your clusters:

```json
{
  "ReverseProxy": {
    "Routes": {
      "catch-all": {
        "ClusterId": "backend",
        "Match": { "Path": "/{**catch-all}" }
      }
    },
    "Clusters": {
      "backend": {
        "Destinations": { "main": { "Address": "http://api:3000" } }
      },
      "honeypot": {
        "Destinations": { "trap": { "Address": "http://honeypot:8080" } }
      },
      "cached": {
        "Destinations": { "static": { "Address": "http://cache:80" } }
      }
    }
  }
}
```

**config/appsettings.json** - Route by behavior:

```json
{
  "BotDetection": {
    "Enabled": true,

    "ActionPolicies": {
      "allow": { "Type": "Allow" },
      "block": { "Type": "Block", "StatusCode": 403 },
      "honeypot": { "Type": "Redirect", "RedirectCluster": "honeypot" },
      "throttle": { "Type": "Throttle", "BaseDelayMs": 3000 }
    },

    "DetectionPolicies": {
      "default": {
        "ActionPolicyName": "allow",
        "Transitions": [
          { "WhenRiskExceeds": 0.9, "ActionPolicyName": "block" },
          { "WhenRiskExceeds": 0.7, "ActionPolicyName": "honeypot" },
          { "WhenRiskExceeds": 0.5, "ActionPolicyName": "throttle" },
          { "WhenSignal": "VerifiedGoodBot", "ActionPolicyName": "allow" }
        ]
      }
    }
  }
}
```

### Result

| Who | Risk | Action |
|-----|------|--------|
| Human | < 0.3 | Real backend |
| Googlebot (verified) | Any | Real backend |
| Suspicious | 0.5-0.7 | Throttled (3s delay) |
| Scraper | 0.7-0.9 | Honeypot (fake data) |
| Malicious | > 0.9 | Blocked |

## Full Docker Compose

```yaml
services:
  gateway:
    image: scottgal/mostlylucid.yarpgateway:latest
    ports:
      - "80:8080"
    volumes:
      - ./config:/app/config:ro
    depends_on:
      - api
      - honeypot

  api:
    image: your-api:latest
    expose:
      - "3000"

  honeypot:
    image: nginx:alpine
    volumes:
      - ./fake-data:/usr/share/nginx/html:ro
    expose:
      - "80"
```

## Environment Variables

| Variable | Default | Description |
|----------|---------|-------------|
| `DEFAULT_UPSTREAM` | - | Catch-all upstream (zero-config mode) |
| `ADMIN_SECRET` | - | Protect admin API with X-Admin-Secret header |
| `GATEWAY_HTTP_PORT` | `8080` | HTTP port |
| `LOG_LEVEL` | `Information` | Logging level |
| `DB_PROVIDER` | `none` | Optional: `postgres`, `sqlserver` |

## Directory Mounts

| Path | Purpose |
|------|---------|
| `/app/config` | Configuration (appsettings.json, yarp.json) |
| `/app/data` | Persistent data (learned patterns, weights) |
| `/app/logs` | Log files |

## Admin API

```bash
curl http://localhost:8080/admin/health
# {"status":"ok","routesConfigured":2,"clustersConfigured":3}

curl http://localhost:8080/admin/routes
curl http://localhost:8080/admin/clusters
curl http://localhost:8080/admin/metrics
```

## Architectures

- `linux/amd64` - x86-64 servers, cloud VMs
- `linux/arm64` - Raspberry Pi 4/5, Apple Silicon, AWS Graviton

## Tags

- `latest` - Current stable
- `1.0.0` - Specific version
- `YYYYMMDD` - Date builds

## Links

- **GitHub**: [github.com/scottgal/mostlylucid.nugetpackages](https://github.com/scottgal/mostlylucid.nugetpackages)
- **NuGet (BotDetection)**: [nuget.org/packages/Mostlylucid.BotDetection](https://www.nuget.org/packages/Mostlylucid.BotDetection)

## License

[Unlicense](https://unlicense.org/) - Public Domain
