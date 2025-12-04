# Mostlylucid.YarpGateway

A **behavioral router** - a reverse proxy that makes intelligent routing decisions based on request analysis, not just URL patterns. Built on Microsoft's YARP with integrated bot detection and more detection modules coming soon.

## Concept: Behavioral Routing

Traditional reverse proxies route based on paths and headers. This gateway adds **behavioral intelligence**:

```
Request → Analyze Behavior → Route to Different Backend → Response
                ↓
         Bot detected? → Honeypot / Cached / Throttled cluster
         Human?        → Real backend
```

**Route bots differently than humans:**
- Send scrapers to a **honeypot** with fake data
- Serve bots a **cached/static** version (save server resources)
- Route suspicious traffic to a **rate-limited** cluster
- Send verified good bots (Googlebot) to the **real backend**
- Block malicious bots entirely

Currently supports **Bot Detection** with additional behavioral modules planned (rate limiting, geo-blocking, anomaly detection, and more).

### Example: Bot-Aware Routing

```
/api/products
  ├── Human (confidence < 0.3)      → api-cluster (real backend)
  ├── Good Bot (Googlebot, etc.)    → api-cluster (real backend)
  ├── Scraper (confidence > 0.7)    → honeypot-cluster (fake data)
  └── Malicious (confidence > 0.9)  → BLOCKED
```

## Bot Detection (Mostlylucid.BotDetection)

The gateway integrates [Mostlylucid.BotDetection](https://www.nuget.org/packages/Mostlylucid.BotDetection) v1.0.0:

- **Multi-Signal Detection** - User-Agent, headers, IP reputation, behavioral patterns, client-side fingerprinting
- **AI-Powered Classification** - Sub-millisecond heuristic model with optional LLM escalation (Ollama/gemma3:4b)
- **Configurable Actions** - Block, throttle, challenge, redirect, honeypot, or log-only
- **Stealth Mode** - Respond to bots without revealing detection
- **Real-time Learning** - Adapts to new bot patterns automatically
- **Path-Based Policies** - Different detection/response rules per route
- **Full Observability** - OpenTelemetry traces and metrics

## Gateway Features

- **Zero-Config Mode** - Just set `DEFAULT_UPSTREAM` and go
- **File-Based Config** - Mount standard YARP JSON configuration
- **Database Backends** - Optional PostgreSQL or SQL Server for dynamic config
- **Admin API** - Health checks, metrics, route inspection, bot detection stats
- **Multi-Architecture** - Runs on x86-64, ARM64 (Raspberry Pi 4/5, Apple Silicon, AWS Graviton)
- **Alpine-Based** - Tiny ~90MB image
- **Built on YARP** - Microsoft's production-grade reverse proxy

## Quick Start

### Zero-Config (Simplest)

```bash
docker run -d -p 8080:8080 \
  -e DEFAULT_UPSTREAM=http://your-backend:3000 \
  scottgal/mostlylucid.yarpgateway
```

### With File Configuration

```bash
docker run -d -p 8080:8080 \
  -v ./config:/app/config:ro \
  -e ADMIN_SECRET=your-secret \
  scottgal/mostlylucid.yarpgateway
```

### Docker Compose

```yaml
services:
  gateway:
    image: scottgal/mostlylucid.yarpgateway:latest
    ports:
      - "80:8080"
    environment:
      - DEFAULT_UPSTREAM=http://backend:3000
      - ADMIN_SECRET=changeme
    volumes:
      - ./config:/app/config:ro
    restart: unless-stopped
```

## Environment Variables

| Variable | Default | Description |
|----------|---------|-------------|
| `GATEWAY_HTTP_PORT` | `8080` | HTTP port |
| `DEFAULT_UPSTREAM` | - | Catch-all upstream URL (zero-config mode) |
| `ADMIN_BASE_PATH` | `/admin` | Admin API path prefix |
| `ADMIN_SECRET` | - | Required header value for admin API access |
| `DB_PROVIDER` | `none` | Database provider: `none`, `postgres`, `sqlserver` |
| `DB_CONNECTION_STRING` | - | Database connection string |
| `DB_MIGRATE_ON_STARTUP` | `true` | Auto-run database migrations |
| `LOG_LEVEL` | `Information` | Logging level |
| `YARP_CONFIG_FILE` | `/app/config/yarp.json` | Path to YARP config file |
| `GATEWAY_CONFIG_PATH` | `/app/config` | Override config directory |
| `GATEWAY_DATA_PATH` | `/app/data` | Override data directory |
| `GATEWAY_LOGS_PATH` | `/app/logs` | Override logs directory |
| `GATEWAY_PLUGINS_PATH` | `/app/plugins` | Override plugins directory |

## Directory Mappings

The gateway uses four logical directories that can be customized via environment variables:

| Logical Name | Default Path | Environment Variable | Purpose |
|--------------|--------------|---------------------|---------|
| `config` | `/app/config` | `GATEWAY_CONFIG_PATH` | Configuration files (appsettings.json, yarp.json) |
| `data` | `/app/data` | `GATEWAY_DATA_PATH` | Persistent data storage |
| `logs` | `/app/logs` | `GATEWAY_LOGS_PATH` | Log files |
| `plugins` | `/app/plugins` | `GATEWAY_PLUGINS_PATH` | Plugin assemblies |

### Volume Mount Examples

```bash
# Mount all directories
docker run -d -p 8080:8080 \
  -v ./config:/app/config:ro \
  -v ./data:/app/data \
  -v ./logs:/app/logs \
  -v ./plugins:/app/plugins:ro \
  scottgal/mostlylucid.yarpgateway
```

```bash
# Custom paths via environment variables
docker run -d -p 8080:8080 \
  -v /etc/gateway:/etc/gateway:ro \
  -v /var/lib/gateway:/var/lib/gateway \
  -e GATEWAY_CONFIG_PATH=/etc/gateway \
  -e GATEWAY_DATA_PATH=/var/lib/gateway \
  scottgal/mostlylucid.yarpgateway
```

### Config Directory Structure

```
/app/config/
├── appsettings.json      # Application settings (optional)
├── yarp.json             # YARP routes and clusters
└── appsettings.*.json    # Environment-specific overrides
```

### Browse Directories via Admin API

```bash
# List all logical directories
curl http://localhost:8080/admin/fs
# {"config":"/app/config","data":"/app/data","logs":"/app/logs","plugins":"/app/plugins"}

# Browse a specific directory
curl http://localhost:8080/admin/fs/config
# {"path":"/app/config","files":["yarp.json","appsettings.json"],"directories":[]}
```

## Admin API

Access at `/admin` (or custom `ADMIN_BASE_PATH`). If `ADMIN_SECRET` is set, include `X-Admin-Secret` header.

| Endpoint | Description |
|----------|-------------|
| `GET /admin/health` | Health check with uptime, route count, DB status |
| `GET /admin/config/effective` | Current merged configuration |
| `GET /admin/routes` | Current YARP routes |
| `GET /admin/clusters` | Current YARP clusters |
| `GET /admin/metrics` | Gateway metrics |

```bash
curl http://localhost:8080/admin/health
# {"status":"ok","uptimeSeconds":3600,"routesConfigured":2,"clustersConfigured":2}
```

## Bot Detection Configuration

Add bot detection settings to `config/appsettings.json`:

```json
{
  "BotDetection": {
    "Enabled": true,
    "BotThreshold": 0.7,
    "EnableUserAgentDetection": true,
    "EnableHeaderAnalysis": true,
    "EnableIpDetection": true,
    "EnableBehavioralAnalysis": true,
    "EnableClientSideFingerprinting": true,

    "AiDetection": {
      "Provider": "Heuristic",
      "TimeoutMs": 15000,
      "Ollama": {
        "Enabled": false,
        "Endpoint": "http://ollama:11434",
        "Model": "gemma3:4b"
      }
    },

    "DetectionPolicies": {
      "default": {
        "Description": "Standard protection",
        "UseFastPath": true,
        "EarlyExitThreshold": 0.3,
        "ImmediateBlockThreshold": 0.95,
        "ActionPolicyName": "block"
      },
      "api": {
        "Description": "Strict API protection",
        "UseFastPath": true,
        "ImmediateBlockThreshold": 0.8,
        "ActionPolicyName": "block"
      },
      "relaxed": {
        "Description": "Allow search engines",
        "UseFastPath": true,
        "ActionPolicyName": "logonly"
      }
    },

    "PathMappings": {
      "/api/*": "api",
      "/health": "relaxed",
      "/*": "default"
    },

    "ActionPolicies": {
      "block": {
        "Type": "Block",
        "StatusCode": 403,
        "Message": "Access Denied"
      },
      "throttle": {
        "Type": "Throttle",
        "BaseDelayMs": 2000,
        "MaxDelayMs": 10000
      },
      "logonly": {
        "Type": "LogOnly"
      }
    }
  }
}
```

### Bot Detection with LLM (Ollama)

For advanced AI-powered detection, run Ollama alongside the gateway:

```yaml
services:
  gateway:
    image: scottgal/mostlylucid.yarpgateway:latest
    ports:
      - "80:8080"
    volumes:
      - ./config:/app/config:ro
    depends_on:
      - ollama

  ollama:
    image: ollama/ollama:latest
    volumes:
      - ollama_data:/root/.ollama
    # Pull model on first run: docker exec ollama ollama pull gemma3:4b

volumes:
  ollama_data:
```

## YARP Configuration with Bot-Aware Routing

Create `config/yarp.json` with multiple clusters for different traffic types:

```json
{
  "ReverseProxy": {
    "Routes": {
      "api-route": {
        "ClusterId": "api-cluster",
        "Match": { "Path": "/api/{**catch-all}" }
      }
    },
    "Clusters": {
      "api-cluster": {
        "Destinations": {
          "primary": { "Address": "http://api-server:3000" }
        }
      },
      "honeypot-cluster": {
        "Destinations": {
          "honeypot": { "Address": "http://honeypot:8080" }
        }
      },
      "cached-cluster": {
        "Destinations": {
          "cache": { "Address": "http://cache-server:80" }
        }
      },
      "throttled-cluster": {
        "Destinations": {
          "slow": { "Address": "http://api-server:3000" }
        },
        "HttpRequest": {
          "Timeout": "00:00:30"
        }
      }
    }
  }
}
```

Then configure bot detection to route to different clusters in `config/appsettings.json`:

```json
{
  "BotDetection": {
    "Enabled": true,
    "BotThreshold": 0.7,

    "ActionPolicies": {
      "allow": {
        "Type": "Allow"
      },
      "block": {
        "Type": "Block",
        "StatusCode": 403
      },
      "honeypot": {
        "Type": "Redirect",
        "RedirectCluster": "honeypot-cluster",
        "PreserveOriginalPath": true
      },
      "serve-cached": {
        "Type": "Redirect",
        "RedirectCluster": "cached-cluster"
      },
      "throttle": {
        "Type": "Throttle",
        "BaseDelayMs": 3000,
        "MaxDelayMs": 15000,
        "RedirectCluster": "throttled-cluster"
      }
    },

    "DetectionPolicies": {
      "default": {
        "Description": "Standard API protection",
        "UseFastPath": true,
        "EarlyExitThreshold": 0.3,
        "ImmediateBlockThreshold": 0.95,
        "ActionPolicyName": "allow",
        "Transitions": [
          { "WhenRiskExceeds": 0.9, "ActionPolicyName": "block" },
          { "WhenRiskExceeds": 0.7, "ActionPolicyName": "honeypot" },
          { "WhenRiskExceeds": 0.5, "ActionPolicyName": "throttle" },
          { "WhenSignal": "VerifiedGoodBot", "ActionPolicyName": "allow" }
        ]
      },
      "static-content": {
        "Description": "Serve bots cached content",
        "ActionPolicyName": "allow",
        "Transitions": [
          { "WhenRiskExceeds": 0.5, "ActionPolicyName": "serve-cached" }
        ]
      }
    },

    "PathMappings": {
      "/api/*": "default",
      "/images/*": "static-content",
      "/css/*": "static-content",
      "/*": "default"
    }
  }
}
```

### What This Configuration Does

| Traffic Type | Risk Score | Action |
|--------------|------------|--------|
| Normal humans | < 0.3 | Allow → Real backend |
| Verified bots (Googlebot, Bingbot) | Any | Allow → Real backend |
| Suspicious (grey zone) | 0.5 - 0.7 | Throttle → Slow response |
| Likely scrapers | 0.7 - 0.9 | Redirect → Honeypot (fake data) |
| Malicious bots | > 0.9 | Block (403) |
| Bots on static content | > 0.5 | Redirect → Cached version |

### Complete Docker Compose Example

```yaml
services:
  # The behavioral gateway
  gateway:
    image: scottgal/mostlylucid.yarpgateway:latest
    ports:
      - "80:8080"
    volumes:
      - ./config:/app/config:ro
      - ./data:/app/data
    environment:
      - ADMIN_SECRET=your-secret
    depends_on:
      - api
      - honeypot
      - cache
    restart: unless-stopped

  # Real API backend (humans and good bots)
  api:
    image: your-api:latest
    expose:
      - "3000"

  # Honeypot backend (scrapers get fake data)
  honeypot:
    image: nginx:alpine
    volumes:
      - ./honeypot-data:/usr/share/nginx/html:ro
    expose:
      - "80"

  # Cache backend (bots get cached/static content)
  cache:
    image: nginx:alpine
    volumes:
      - ./cached-content:/usr/share/nginx/html:ro
    expose:
      - "80"

  # Optional: Ollama for AI-powered detection
  ollama:
    image: ollama/ollama:latest
    volumes:
      - ollama_data:/root/.ollama
    profiles:
      - ai

volumes:
  ollama_data:
```

## Supported Architectures

| Architecture | Platforms |
|--------------|-----------|
| `linux/amd64` | x86-64 servers, desktops, cloud VMs |
| `linux/arm64` | Raspberry Pi 4/5, Apple Silicon, AWS Graviton |

## Tags

- `latest` - Latest stable release
- `X.Y.Z` - Specific version (e.g., `1.0.0`)
- `YYYYMMDD` - Date-based builds

## Raspberry Pi Deployment

```bash
# Install Docker
curl -fsSL https://get.docker.com | sh
sudo usermod -aG docker $USER

# Run gateway
docker run -d --name gateway \
  -p 80:8080 \
  --restart unless-stopped \
  -e DEFAULT_UPSTREAM=http://192.168.1.100:3000 \
  scottgal/mostlylucid.yarpgateway
```

### Pi-Optimized Compose

```yaml
services:
  gateway:
    image: scottgal/mostlylucid.yarpgateway:latest
    ports:
      - "80:8080"
    environment:
      - DEFAULT_UPSTREAM=http://your-backend:3000
      - LOG_LEVEL=Warning
    deploy:
      resources:
        limits:
          memory: 256M
    restart: unless-stopped
```

## Source & Documentation

- **GitHub**: [github.com/scottgal/mostlylucid.nugetpackages](https://github.com/scottgal/mostlylucid.nugetpackages)
- **Full Documentation**: See repository README

## License

[Unlicense](https://unlicense.org/) - Public Domain
