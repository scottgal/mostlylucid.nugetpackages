# Mostlylucid.YarpGateway

A lightweight, Docker-first YARP reverse proxy gateway.

## Quick Start

### Zero-Config Mode

Just point to an upstream:

```bash
docker run -p 8080:8080 -e DEFAULT_UPSTREAM=http://your-backend:3000 mostlylucid/yarp-gateway
```

### File-Based Configuration

Mount your YARP config:

```bash
docker run -p 8080:8080 \
  -v ./config:/app/config:ro \
  -e ADMIN_SECRET=your-secret \
  mostlylucid/yarp-gateway
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
docker build -t mostlylucid/yarp-gateway .

# Or use .NET SDK
dotnet publish -c Release
```

## Image Size

The Alpine-based image is optimized for size:
- Base: ~80MB (ASP.NET 9.0 Alpine runtime)
- App: ~10MB
- Total: ~90MB

## Supported Architectures

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
    image: mostlylucid/yarp-gateway:latest
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
  mostlylucid/yarp-gateway
```

### Pi Network Gateway Example

Use the Pi as a home network reverse proxy:

```yaml
# docker-compose.yml
services:
  gateway:
    image: mostlylucid/yarp-gateway:latest
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
   git tag yarpgateway-v0.1.0
   git push origin yarpgateway-v0.1.0
   ```

2. The workflow will:
   - Build the .NET project
   - Build multi-arch Docker images (amd64, arm64, arm/v7)
   - Push to Docker Hub as `mostlylucid/yarp-gateway:0.1.0` and `:latest`
   - Update Docker Hub description from README

### Manual Trigger

You can also trigger the workflow manually from GitHub Actions with a custom version.

### Required Secrets

Configure these in your GitHub repository settings:
- `DOCKERHUB_USERNAME` - Docker Hub username
- `DOCKERHUB_TOKEN` - Docker Hub access token

## License

[Unlicense](https://unlicense.org/) - Public Domain
