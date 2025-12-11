# Mostlylucid.BotDetection.Console

Minimal, single-file YARP gateway with bot detection. Optimized for size and performance with AOT compilation and trimming.

## Features

- **Single-file executable** - AOT compiled and trimmed
- **Cross-platform** - Windows, Linux (x64, arm64 including Raspberry Pi 4/5)
- **Signature tracking** - Stores high-confidence detections as HMAC-SHA256 hashed signatures (core feature)
- **Two modes**:
  - **Demo Mode** (default) - Full verbose logging, all detectors, no blocking
  - **Production Mode** - Blocking, background learning, AI escalation
- **Tiny footprint** - ~15-30MB single executable
- **Zero dependencies** - No runtime required
- **Console logging** - All detections logged with full details
- **Zero-PII architecture** - Raw IPs never stored, only privacy-safe HMAC signatures

## Quick Start

### Demo Mode (Default)

```bash
# Windows
minigw.exe --upstream http://localhost:8080 --port 5000

# Linux
./minigw --upstream http://localhost:8080 --port 5000
```

### Production Mode

```bash
# Windows
minigw.exe --mode production --upstream http://backend:8080 --port 80

# Linux
./minigw --mode production --upstream http://backend:8080 --port 80
```

### Using Environment Variables

```bash
export UPSTREAM=http://backend:8080
export PORT=5000
export MODE=production

./minigw
```

## Modes

### Demo Mode

- **Purpose**: Testing, development, observability
- **Detectors**: Fast-path only (UserAgent, Header, IP, SecurityTool, etc.)
- **Blocking**: Disabled - all traffic allowed
- **Learning**: Disabled
- **Logging**: Full verbose output with all signals and detector contributions
- **Action**: Log only

Example output:
```
━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
🔍 Bot Detection Result
━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
  Request:     GET /api/data
  IP:          192.168.1.100
  User-Agent:  curl/8.4.0

  IsBot:       ✗ YES
  Probability: 0.95
  Confidence:  0.95
  Risk Band:   VeryHigh
  Bot Name:    curl
  Policy:      demo
  Action:      Allow

  Detectors:   3 ran in 2.3ms
  ┌─────────────────────────────────────────────────────
  │ UserAgent                   0.95 × 1.0 =   0.95
  │ Header                      0.80 × 0.8 =   0.64
  │ SecurityTool                0.00 × 1.0 =   0.00
  └─────────────────────────────────────────────────────

  Signals: 12
    ua.bot_probability             = 0.95
    ua.pattern_match               = curl/
    ua.is_headless                 = False
    header.missing_accept          = True
    ...
━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
```

### Production Mode

- **Purpose**: Real-world deployment with bot blocking
- **Detectors**: Fast-path + slow-path + AI escalation
  - Fast: FastPathReputation, UserAgent, Header, IP, SecurityTool
  - Slow: Behavioral, Fingerprinting (HTTP/2, TLS, TCP/IP), MultiLayerCorrelation, Waveform
  - AI: Heuristic learning
- **Blocking**: Enabled with adaptive policies
  - >0.95 risk: Immediate block (403)
  - >0.70 risk: Block with logging
  - >0.50 risk: Throttle (rate limit)
  - <0.30 risk: Allow with minimal logging
- **Learning**: Background learning enabled
- **Logging**: Concise one-line format
- **Response Path**: Honeypot tracking, error pattern detection

Example output:
```
✓ HUMAN  0.12 VeryLow      1.2ms GET  /                   [192.168.1.100] -
✗ BOT    0.95 VeryHigh     2.3ms GET  /api/data           [192.168.1.101] curl
✓ HUMAN  0.25 Low          1.5ms POST /submit             [192.168.1.102] -
✗ BOT    0.87 High         8.7ms GET  /admin              [192.168.1.103] Acunetix
```

## Command-Line Options

| Option | Environment Variable | Default | Description |
|--------|---------------------|---------|-------------|
| `--upstream` | `UPSTREAM` | `http://localhost:8080` | Upstream server URL |
| `--port` | `PORT` | `5000` | Port to listen on |
| `--mode` | `MODE` | `demo` | Mode: `demo` or `production` |

## Configuration

Configuration is in `appsettings.json` (demo) and `appsettings.production.json` (production).

All bot detection settings are discoverable in these files:
- Detection policies (which detectors to run)
- Action policies (what to do when bots are detected)
- Thresholds (risk levels for different actions)
- Learning settings
- Transitions (dynamic policy switching)

## Testing

Use the included `test.http` file with Visual Studio Code REST Client extension or similar tools.

```bash
# Install VS Code REST Client extension
code --install-extension humao.rest-client

# Open test.http and click "Send Request" links
code test.http
```

Test cases included:
- Normal human requests (Chrome, Firefox)
- Bot requests (curl, Python requests, Selenium)
- Legitimate bots (Googlebot)
- Security scanners (Acunetix)
- Edge cases (missing User-Agent, old browsers)
- Load testing

## Building

### Prerequisites

- .NET 9 SDK
- Native AOT compiler toolchain:
  - **Windows**: Visual Studio 2022 with C++ workload
  - **Linux**: `build-essential`, `zlib1g-dev`
  - **macOS**: Xcode command-line tools

### Build for Current Platform

```bash
dotnet publish -c Release
```

Output: `bin/Release/net9.0/{platform}/publish/minigw` or `minigw.exe`

### Cross-Platform Builds

```bash
# Linux x64
dotnet publish -c Release -r linux-x64

# Linux ARM64 (Raspberry Pi 4/5)
dotnet publish -c Release -r linux-arm64

# Windows x64
dotnet publish -c Release -r win-x64

# macOS ARM64 (M1/M2)
dotnet publish -c Release -r osx-arm64
```

### Size Optimization

The project is already configured for maximum size optimization:
- AOT compilation (`PublishAot=true`)
- Full trimming (`TrimMode=full`)
- Symbol stripping (`StripSymbols=true`)
- Request delegate generation
- Configuration binding generation

Typical sizes:
- Linux x64: ~20MB
- Linux ARM64: ~18MB
- Windows x64: ~15MB

## Deployment

### Standalone Executable

```bash
# Copy single executable to target system
scp bin/Release/net9.0/linux-x64/publish/minigw user@server:/usr/local/bin/

# Copy configuration
scp appsettings*.json user@server:/etc/minigw/

# Run
ssh user@server
cd /etc/minigw
/usr/local/bin/minigw --mode production --upstream http://backend:8080 --port 80
```

### Docker

```dockerfile
FROM mcr.microsoft.com/dotnet/runtime-deps:9.0-alpine

COPY bin/Release/net9.0/linux-x64/publish/minigw /app/minigw
COPY appsettings*.json /app/

WORKDIR /app

ENTRYPOINT ["./minigw"]
```

```bash
docker build -t minigw:latest .
docker run -p 5000:5000 -e UPSTREAM=http://backend:8080 -e MODE=production minigw:latest
```

### Systemd Service (Linux)

```ini
[Unit]
Description=Mostlylucid Bot Detection Gateway
After=network.target

[Service]
Type=simple
User=gateway
WorkingDirectory=/etc/minigw
ExecStart=/usr/local/bin/minigw --mode production --upstream http://backend:8080 --port 80
Restart=on-failure
RestartSec=5
Environment="DOTNET_SYSTEM_GLOBALIZATION_INVARIANT=false"

[Install]
WantedBy=multi-user.target
```

```bash
sudo systemctl enable minigw
sudo systemctl start minigw
sudo systemctl status minigw
```

## Performance

Typical latency (measured on a Raspberry Pi 5):
- Fast-path only (demo): ~1-3ms
- Fast + slow path: ~5-15ms
- With AI escalation: ~20-50ms

Memory usage:
- Idle: ~30MB
- Under load (1000 req/s): ~80MB

## Raspberry Pi Notes

Tested on:
- Raspberry Pi 4 (1GB, 2GB, 4GB, 8GB)
- Raspberry Pi 5 (4GB, 8GB)

**Not supported**:
- Raspberry Pi 3 and older (ARM v7)
- Raspberry Pi Zero

Build for Pi:
```bash
dotnet publish -c Release -r linux-arm64
```

Deploy:
```bash
scp bin/Release/net9.0/linux-arm64/publish/minigw pi@raspberrypi.local:~/
scp appsettings*.json pi@raspberrypi.local:~/

ssh pi@raspberrypi.local
chmod +x minigw
./minigw --upstream http://localhost:8080
```

## Demo Loop: YARP → Backend with Detection Display

This demonstrates the complete flow from gateway through to backend with visual detection results.

### Setup

1. **Start Backend** (Demo app with YarpProxyDemo page):
```bash
cd ../Mostlylucid.BotDetection.Demo
dotnet run
# Runs on http://localhost:5000
```

2. **Start Gateway**:
```bash
# From Mostlylucid.BotDetection.Console directory
dotnet run -- --upstream http://localhost:5000 --port 5100 --mode demo
# Gateway runs on http://localhost:5100
```

3. **Access Demo Page**:
```bash
# Via gateway (bot detection runs)
open http://localhost:5100/YarpProxyDemo

# Or directly (fallback to inline middleware)
open http://localhost:5000/YarpProxyDemo
```

### What You'll See

The `/YarpProxyDemo` page displays:
- **Bot Detection Status** (Bot/Human with icon)
- **Detection Reasons** in plain English bullets
- **Detector Contributions** with visual contribution bars
- **YARP Routing Info** (when accessed via gateway)
- **Request Metadata** (ID, timestamp, processing time)
- **Architecture Explanation** (how the headers work)

### How It Works

```
┌──────┐     ┌─────────────────┐     ┌──────────────────┐
│Client│────▶│minigw (Gateway) │────▶│Demo App (Backend)│
└──────┘     │ Bot Detection   │     │ Display Results  │
             │ + Headers       │     └──────────────────┘
             └─────────────────┘
```

1. **Gateway** runs bot detection on request
2. **Headers** serialized to `X-Bot-Detection-*` headers:
   - `X-Bot-Detection-Result`: `true`/`false`
   - `X-Bot-Detection-Probability`: `0.85`
   - `X-Bot-Detection-Reasons`: `["Headless browser","Datacenter IP"]`
   - `X-Bot-Detection-Contributions`: JSON array of detector data
3. **Backend** reads headers via `BotDetectionDetailsViewComponent`
4. **Page** displays results in nice CSS with `<details>` tags

### Testing Different Bots

Try different user agents via the gateway:

**Googlebot:**
```bash
curl -A "Mozilla/5.0 (compatible; Googlebot/2.1)" http://localhost:5100/YarpProxyDemo
```

**Scraper:**
```bash
curl -A "Scrapy/2.5.0" http://localhost:5100/YarpProxyDemo
```

**Headless Chrome:**
```bash
curl -A "HeadlessChrome/120.0.0.0" http://localhost:5100/YarpProxyDemo
```

**Human (Browser):**
```bash
curl -A "Mozilla/5.0 (Windows NT 10.0) Chrome/120.0.0.0" \
     -H "Accept: text/html" \
     -H "Accept-Language: en-US" \
     http://localhost:5100/YarpProxyDemo
```

Watch the gateway console for colorful detection logs, then view the page HTML to see formatted results!

### Backend Integration

To add detection display to your own backend app:

1. **Install UI package:**
```bash
dotnet add package Mostlylucid.BotDetection.UI
```

2. **Add to _ViewImports.cshtml:**
```cshtml
@addTagHelper *, Mostlylucid.BotDetection.UI
```

3. **Use in your page:**
```cshtml
<link rel="stylesheet" href="~/_content/Mostlylucid.BotDetection.UI/bot-detection-details.css" />

<details open>
    <summary>Bot Detection</summary>
    <bot-detection-details />
</details>
```

The ViewComponent automatically handles both modes:
- **YARP Mode**: Reads `X-Bot-Detection-*` headers
- **Inline Mode**: Reads `HttpContext.Items["BotDetection.Evidence"]`

## Troubleshooting

### "Permission denied" on Linux

```bash
chmod +x minigw
```

### "Cannot execute binary file"

Wrong architecture. Rebuild for target platform:
```bash
dotnet publish -c Release -r linux-arm64  # For Pi
dotnet publish -c Release -r linux-x64    # For x86_64 servers
```

### High memory usage

If running on low-memory systems (1GB Pi 4):
1. Use demo mode (disables learning and slow-path)
2. Reduce detector count in appsettings.json
3. Increase `EarlyExitThreshold` to 0.5

### Slow startup

First run is slower due to JIT. Subsequent runs are fast (~100ms startup).

## License

Same as parent project (Mostlylucid.BotDetection).
