# Mostlylucid.YarpGateway

A lightweight, Docker-first YARP reverse proxy gateway.

[![Docker Hub](https://img.shields.io/docker/pulls/scottgal/mostlylucid.yarpgateway?label=Docker%20Hub)](https://hub.docker.com/r/scottgal/mostlylucid.yarpgateway)

## Quick Start

### Zero-Config Mode

Just point to an upstream:

```bash
docker run -p 8080:8080 -e DEFAULT_UPSTREAM=http://your-backend:3000 scottgal/mostlylucid.yarpgateway
```

### File-Based Configuration

Mount your YARP config:

```bash
docker run -p 8080:8080 \
  -v ./config:/app/config:ro \
  -e ADMIN_SECRET=your-secret \
  scottgal/mostlylucid.yarpgateway
```

## Configuration

### Environment Variables

| Variable | Default | Description |
|----------|---------|-------------|
| `GATEWAY_HTTP_PORT` | `8080` | HTTP port |
| `DEFAULT_UPSTREAM` | - | Catch-all upstream URL |
| `ADMIN_BASE_PATH` | `/admin` | Admin API path |
| `ADMIN_SECRET` | - | Required header for admin API |
| `DB_PROVIDER` | `none` | Database: `none`, `postgres`, `sqlserver` |
| `DB_CONNECTION_STRING` | - | Database connection string |
| `DB_MIGRATE_ON_STARTUP` | `true` | Auto-run migrations |
| `LOG_LEVEL` | `Information` | Log level |
| `YARP_CONFIG_FILE` | `/app/config/yarp.json` | YARP config path |

### Volume Mounts

| Path | Purpose |
|------|---------|
| `/app/config` | Configuration files (appsettings.json, yarp.json) |
| `/app/data` | Persistent data |
| `/app/logs` | Log files |
| `/app/plugins` | Plugin assemblies |

## Admin API

All endpoints under `/admin` (configurable via `ADMIN_BASE_PATH`).

If `ADMIN_SECRET` is set, include `X-Admin-Secret: your-secret` header.

### Endpoints

| Endpoint | Description |
|----------|-------------|
| `GET /admin/health` | Health check with uptime, route count, DB status |
| `GET /admin/config/effective` | Current merged configuration |
| `GET /admin/config/sources` | Active configuration sources |
| `GET /admin/fs` | List logical directories |
| `GET /admin/fs/{name}` | Browse directory contents |
| `GET /admin/routes` | Current YARP routes |
| `GET /admin/clusters` | Current YARP clusters |
| `GET /admin/metrics` | Gateway metrics |

### Example Response

```bash
curl http://localhost:8080/admin/health
```

```json
{
  "status": "ok",
  "uptimeSeconds": 3600,
  "routesConfigured": 2,
  "clustersConfigured": 2,
  "mode": "configured",
  "db": "disabled"
}
```

## Docker Compose Examples

### Simple (Zero-Config)

```bash
docker-compose --profile simple up -d
```

### With File Configuration

```bash
docker-compose --profile files up -d
```

### With Postgres

```bash
docker-compose --profile postgres up -d
```

### With SQL Server

```bash
docker-compose --profile sqlserver up -d
```

## YARP Configuration

Create `config/yarp.json`:

```json
{
  "ReverseProxy": {
    "Routes": {
      "api-route": {
        "ClusterId": "api-cluster",
        "Match": {
          "Path": "/api/{**catch-all}"
        }
      }
    },
    "Clusters": {
      "api-cluster": {
        "Destinations": {
          "primary": {
            "Address": "http://api-server:3000"
          }
        }
      }
    }
  }
}
```

## Configuration Precedence

1. Environment variables (highest priority)
2. Configuration files
3. Built-in defaults (lowest priority)

## Building

```bash
# Build image
docker build -t scottgal/mostlylucid.yarpgateway .

# Or use .NET SDK
dotnet publish -c Release
```

## Docker Image

### Available Tags

- `scottgal/mostlylucid.yarpgateway:latest` - Latest release
- `scottgal/mostlylucid.yarpgateway:X.Y.Z[-previewN]` - Specific version (e.g., `1.0.0-preview1`)
- `scottgal/mostlylucid.yarpgateway:YYYYMMDD` - Date-based (e.g., `20231203`)

### Image Size

The Alpine-based image is optimized for size:
- Base: ~80MB (ASP.NET 9.0 Alpine runtime)
- App: ~10MB
- Total: ~90MB

### Supported Architectures

Multi-architecture images are built automatically:

| Architecture | Platforms |
|--------------|-----------|
| `linux/amd64` | Standard x86-64 servers, desktops, cloud VMs |
| `linux/arm64` | Raspberry Pi 4/5 (64-bit), Apple Silicon, AWS Graviton, ARM servers |
| `linux/arm/v7` | Raspberry Pi 3/Zero 2 W (32-bit), older ARM devices |

Docker automatically selects the correct image for your platform.

## Raspberry Pi Deployment

### Recommended Hardware

- **Pi 5 (4GB+)**: Best performance, handles high traffic
- **Pi 4 (2GB+)**: Good for moderate traffic, home/small office use
- **Pi 3/Zero 2 W**: Light traffic only, basic routing

### Pi-Optimized Settings

Create a `docker-compose.pi.yml` for resource-constrained deployments:

```yaml
services:
  gateway:
    image: scottgal/mostlylucid.yarpgateway:latest
    ports:
      - "80:8080"
    environment:
      - DEFAULT_UPSTREAM=http://your-backend:3000
      - ADMIN_SECRET=your-secret
      - LOG_LEVEL=Warning
    volumes:
      - ./config:/app/config:ro
      - ./logs:/app/logs
    deploy:
      resources:
        limits:
          memory: 256M
        reservations:
          memory: 128M
    restart: unless-stopped
    healthcheck:
      test: ["CMD", "curl", "-f", "http://localhost:8080/admin/health"]
      interval: 60s
      timeout: 10s
      retries: 3
      start_period: 30s
```

### Pi Performance Tips

1. **Use file-based config** - Avoid database on limited RAM
2. **Set `LOG_LEVEL=Warning`** - Reduce disk I/O from logging
3. **Memory limit 256M** - Plenty for gateway, leaves room for OS
4. **Longer health check intervals** - Reduces CPU overhead
5. **Use USB SSD** - SD cards are slow for logs/data

### Quick Pi Setup

```bash
# Install Docker on Raspberry Pi OS (64-bit recommended)
curl -fsSL https://get.docker.com | sh
sudo usermod -aG docker $USER

# Pull and run (auto-selects arm64 or armv7)
docker run -d --name gateway \
  -p 80:8080 \
  --restart unless-stopped \
  -e DEFAULT_UPSTREAM=http://192.168.1.100:3000 \
  scottgal/mostlylucid.yarpgateway
```

### Pi Network Gateway Example

Use the Pi as a home network reverse proxy:

```yaml
# docker-compose.yml
services:
  gateway:
    image: scottgal/mostlylucid.yarpgateway:latest
    ports:
      - "80:8080"
      - "443:8443"
    volumes:
      - ./config:/app/config:ro
    environment:
      - ADMIN_SECRET=changeme
    restart: unless-stopped
```

With `config/yarp.json`:
```json
{
  "ReverseProxy": {
    "Routes": {
      "homeassistant": {
        "ClusterId": "ha",
        "Match": { "Hosts": ["ha.local"] }
      },
      "plex": {
        "ClusterId": "plex",
        "Match": { "Hosts": ["plex.local"] }
      },
      "pihole": {
        "ClusterId": "pihole",
        "Match": { "Hosts": ["pihole.local"] }
      }
    },
    "Clusters": {
      "ha": { "Destinations": { "d1": { "Address": "http://192.168.1.50:8123" } } },
      "plex": { "Destinations": { "d1": { "Address": "http://192.168.1.51:32400" } } },
      "pihole": { "Destinations": { "d1": { "Address": "http://192.168.1.1:80" } } }
    }
  }
}
```

## Publishing

Docker images are automatically built and pushed to Docker Hub via GitHub Actions.

### Release Process

1. Create and push a tag:
   ```bash
   git tag yarpgateway-v1.0.0-preview1
   git push origin yarpgateway-v1.0.0-preview1
   ```

2. The workflow will:
   - Build the .NET project
   - Build multi-arch Docker images (amd64, arm64, arm/v7)
   - Push to Docker Hub with multiple tags:
     - `scottgal/mostlylucid.yarpgateway:1.0.0-preview1` (version)
     - `scottgal/mostlylucid.yarpgateway:YYYYMMDD` (date)
     - `scottgal/mostlylucid.yarpgateway:latest`
   - Update Docker Hub description from README

### Manual Trigger

You can also trigger the workflow manually from GitHub Actions with a custom version.

### Required Secrets

Configure these in your GitHub repository settings:
- `DOCKERHUB_USERNAME` - Docker Hub username
- `DOCKERHUB_TOKEN` - Docker Hub access token

## Bot Detection Integration

YarpGateway uses [Mostlylucid.BotDetection](../Mostlylucid.BotDetection/) for intelligent bot filtering at the edge.

### Quick Setup

Add bot detection with a single line in your configuration:

```json
{
  "BotDetection": {
    "Enabled": true,
    "BotThreshold": 0.7,
    "BlockDetectedBots": true
  }
}
```

### Recommended Production Configuration

```json
{
  "BotDetection": {
    "BotThreshold": 0.7,
    "BlockDetectedBots": true,
    "DefaultActionPolicyName": "throttle-stealth",

    "EnableAiDetection": true,
    "AiDetection": {
      "Provider": "Heuristic",
      "Heuristic": {
        "Enabled": true,
        "EnableWeightLearning": true
      }
    },

    "Learning": {
      "Enabled": true
    },

    "PathPolicies": {
      "/api/auth/*": "strict",
      "/api/checkout/*": "strict",
      "/robots.txt": "allowAll",
      "/sitemap.xml": "allowVerifiedBots"
    }
  }
}
```

### Common Scenarios

#### API Protection

Block bots from API endpoints while allowing legitimate traffic:

```json
{
  "BotDetection": {
    "BotThreshold": 0.6,
    "BlockDetectedBots": true,
    "DefaultActionPolicyName": "block",
    "PathPolicies": {
      "/api/*": "strict",
      "/health": "allowAll"
    }
  }
}
```

#### Allow Verified Crawlers (Googlebot, Bingbot)

Let search engine bots through while blocking scrapers:

```json
{
  "BotDetection": {
    "BotThreshold": 0.7,
    "BlockDetectedBots": true,
    "PathPolicies": {
      "/": "allowVerifiedBots",
      "/api/*": "strict"
    }
  }
}
```

#### Stealth Throttling

Slow down bots without revealing detection:

```json
{
  "BotDetection": {
    "BotThreshold": 0.7,
    "BlockDetectedBots": true,
    "DefaultActionPolicyName": "throttle-stealth",
    "ActionPolicies": {
      "throttle-stealth": {
        "Type": "throttle",
        "DelayMs": 3000,
        "JitterMs": 1000
      }
    }
  }
}
```

#### Shadow Mode (Log Only)

Monitor bot traffic without blocking:

```json
{
  "BotDetection": {
    "BotThreshold": 0.5,
    "BlockDetectedBots": false,
    "DefaultActionPolicyName": "logonly"
  }
}
```

#### Project Honeypot IP Reputation

Add IP reputation checking (requires free API key from [projecthoneypot.org](https://www.projecthoneypot.org/)):

```json
{
  "BotDetection": {
    "ProjectHoneypot": {
      "Enabled": true,
      "AccessKey": "your-access-key"
    }
  }
}
```

#### LLM-Powered Detection

For complex cases, escalate to an LLM (requires [Ollama](https://ollama.ai/)):

```json
{
  "BotDetection": {
    "EnableAiDetection": true,
    "AiDetection": {
      "Provider": "HeuristicWithEscalation",
      "LlmEscalation": {
        "OllamaUrl": "http://localhost:11434",
        "Model": "gemma3:4b",
        "EscalationThreshold": 0.4
      }
    }
  }
}
```

### Environment Variables

| Variable | Description |
|----------|-------------|
| `BOTDETECTION__ENABLED` | Enable/disable bot detection |
| `BOTDETECTION__BOTTHRESHOLD` | Detection threshold (0.0-1.0) |
| `BOTDETECTION__BLOCKDETECTEDBOTS` | Block or just log detected bots |
| `BOTDETECTION__PROJECTHONEYPOT__ACCESSKEY` | Project Honeypot API key |
| `BOTDETECTION__AIDETECTION__LLMESCALATION__OLLAMAURL` | Ollama server URL |

### Detection Methods

| Method | Latency | Description |
|--------|---------|-------------|
| User-Agent | <1ms | Pattern matching against 1000+ known bots |
| Headers | <1ms | Suspicious/missing header detection |
| IP Ranges | <1ms | Datacenter IP identification (AWS, GCP, Azure) |
| Security Tools | <1ms | Detects Nikto, sqlmap, Burp Suite, etc. |
| Heuristic AI | <1ms | Weighted feature classification with learning |
| Project Honeypot | ~100ms | IP reputation via DNS lookup |
| LLM Escalation | 50-500ms | Full reasoning for edge cases |

### Full Documentation

For complete configuration options, custom detectors, and advanced scenarios, see the [BotDetection README](../Mostlylucid.BotDetection/README.md).

## License

[Unlicense](https://unlicense.org/) - Public Domain
