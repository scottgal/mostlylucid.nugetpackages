# YARP Gateway

A lightweight, Docker-first YARP reverse proxy gateway - the companion project to Mostlylucid.BotDetection.

> **Full Documentation**: See the [Mostlylucid.YarpGateway README](../../Mostlylucid.YarpGateway/README.md) for complete documentation.

## Overview

Mostlylucid.YarpGateway is a standalone Docker image that provides:
- Zero-config reverse proxy (just set `DEFAULT_UPSTREAM`)
- YARP-based routing with hot-reload configuration
- Admin API for health checks, config inspection, and metrics
- Optional database persistence (Postgres, SQL Server)
- Multi-architecture support (amd64, arm64, arm/v7 for Raspberry Pi)

## Quick Start

```bash
# Zero-config mode
docker run -p 8080:8080 -e DEFAULT_UPSTREAM=http://your-backend:3000 mostlylucid/yarp-gateway

# With file configuration
docker run -p 8080:8080 -v ./config:/app/config:ro mostlylucid/yarp-gateway
```

## Using with BotDetection

The YarpGateway pairs naturally with Mostlylucid.BotDetection for edge protection:

### Architecture

```
Internet → YarpGateway → Your App (with BotDetection)
              ↓
        Load balancing
        Health checks
        TLS termination
```

### Deployment Pattern

1. **YarpGateway** handles:
   - Edge routing and load balancing
   - TLS termination
   - Request forwarding
   - Health monitoring

2. **BotDetection** handles:
   - Bot classification
   - Rate limiting
   - Challenge/response
   - Learning and adaptation

### Docker Compose Example

```yaml
services:
  gateway:
    image: mostlylucid/yarp-gateway:latest
    ports:
      - "80:8080"
    environment:
      - ADMIN_SECRET=gateway-secret
    volumes:
      - ./yarp.json:/app/config/yarp.json:ro

  webapp:
    build: .
    environment:
      - BotDetection__EnableAiDetection=true
      - BotDetection__Learning__Enabled=true
    # Not exposed - only accessible via gateway
```

With `yarp.json`:
```json
{
  "ReverseProxy": {
    "Routes": {
      "webapp": {
        "ClusterId": "webapp",
        "Match": { "Path": "/{**catch-all}" }
      }
    },
    "Clusters": {
      "webapp": {
        "Destinations": {
          "primary": { "Address": "http://webapp:8080" }
        },
        "HealthCheck": {
          "Passive": { "Enabled": true }
        }
      }
    }
  }
}
```

## Raspberry Pi Deployment

YarpGateway is optimized for Raspberry Pi as a home network gateway:

```bash
# On Raspberry Pi (64-bit OS recommended)
docker run -d --name gateway \
  -p 80:8080 \
  --restart unless-stopped \
  -e DEFAULT_UPSTREAM=http://192.168.1.100:3000 \
  mostlylucid/yarp-gateway
```

See the [full Pi documentation](../../Mostlylucid.YarpGateway/README.md#raspberry-pi-deployment) for:
- Memory-optimized settings
- Performance tips
- Home network routing examples

## Key Features

| Feature | Description |
|---------|-------------|
| **Zero-config** | Single env var to get started |
| **Hot reload** | Config changes without restart |
| **Admin API** | `/admin/health`, `/admin/routes`, `/admin/metrics` |
| **Multi-arch** | amd64, arm64, arm/v7 |
| **Lightweight** | ~90MB Alpine-based image |

## Links

- [Docker Hub](https://hub.docker.com/r/mostlylucid/yarp-gateway)
- [Full Documentation](../../Mostlylucid.YarpGateway/README.md)
- [Source Code](../../Mostlylucid.YarpGateway/)
