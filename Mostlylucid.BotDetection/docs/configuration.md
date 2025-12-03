# Configuration Reference

Complete configuration options for Mostlylucid.BotDetection.

## Core Settings

| Option | Type | Default | Description |
|--------|------|---------|-------------|
| `BotThreshold` | double | `0.7` | Confidence threshold to classify as bot (0.0-1.0) |
| `EnableUserAgentDetection` | bool | `true` | Enable User-Agent pattern matching |
| `EnableHeaderAnalysis` | bool | `true` | Enable HTTP header inspection |
| `EnableIpDetection` | bool | `true` | Enable IP-based detection |
| `EnableBehavioralAnalysis` | bool | `true` | Enable behavioral rate analysis |
| `EnableAiDetection` | bool | `false` | Enable AI-based classification |
| `EnableTestMode` | bool | `false` | Enable test mode headers (dev only!) |
| `MaxRequestsPerMinute` | int | `60` | Rate limit threshold (1-10000) |
| `CacheDurationSeconds` | int | `300` | Cache duration for results (0-86400) |

## Blocking Settings

| Option | Type | Default | Description |
|--------|------|---------|-------------|
| `BlockDetectedBots` | bool | `false` | Enable automatic blocking |
| `BlockStatusCode` | int | `403` | HTTP status when blocking |
| `BlockMessage` | string | `"Access denied"` | Response message |
| `MinConfidenceToBlock` | double | `0.8` | Confidence required to block |
| `AllowVerifiedSearchEngines` | bool | `true` | Let Googlebot, Bingbot through |
| `AllowSocialMediaBots` | bool | `true` | Let Facebook, Twitter through |
| `AllowMonitoringBots` | bool | `true` | Let UptimeRobot, Pingdom through |

## AI Detection Settings

```json
{
  "BotDetection": {
    "EnableAiDetection": true,
    "AiDetection": {
      "Provider": "Onnx",
      "TimeoutMs": 2000,
      "MaxConcurrentRequests": 5,
      "Ollama": {
        "Endpoint": "http://localhost:11434",
        "Model": "gemma3:1b",
        "UseJsonMode": true,
        "CustomPrompt": null
      },
      "Onnx": {
        "ModelPath": null,
        "ModelDownloadUrl": "",
        "AutoDownloadModel": true,
        "UseGpu": false,
        "EnableHeuristicFallback": true
      }
    }
  }
}
```

See [ai-detection.md](ai-detection.md) for details.

## Behavioral Settings

```json
{
  "BotDetection": {
    "Behavioral": {
      "ApiKeyHeader": "X-Api-Key",
      "ApiKeyRateLimit": 120,
      "UserIdClaim": "sub",
      "UserIdHeader": "X-User-Id",
      "UserRateLimit": 180,
      "EnableAnomalyDetection": true,
      "SpikeThresholdMultiplier": 5.0,
      "NewPathAnomalyThreshold": 0.8
    }
  }
}
```

See [behavioral-analysis.md](behavioral-analysis.md) for details.

## Client-Side Settings

```json
{
  "BotDetection": {
    "ClientSide": {
      "Enabled": true,
      "TokenSecret": "your-secret-key",
      "TokenLifetimeSeconds": 300,
      "FingerprintCacheDurationSeconds": 1800,
      "CollectWebGL": true,
      "CollectCanvas": true,
      "CollectAudio": false,
      "MinIntegrityScore": 70,
      "HeadlessThreshold": 0.5
    }
  }
}
```

See [client-side-fingerprinting.md](client-side-fingerprinting.md) for details.

## Background Update Settings

| Option | Type | Default | Description |
|--------|------|---------|-------------|
| `EnableBackgroundUpdates` | bool | `true` | Enable automatic list updates |
| `UpdateIntervalHours` | int | `24` | Hours between updates (1-168) |
| `UpdateCheckIntervalMinutes` | int | `60` | Minutes between update checks (5-1440) |
| `StartupDelaySeconds` | int | `5` | Delay before first update |
| `ListDownloadTimeoutSeconds` | int | `30` | Timeout per download |
| `MaxDownloadRetries` | int | `3` | Retries before giving up |

## Logging Settings

| Option | Type | Default | Description |
|--------|------|---------|-------------|
| `LogAllRequests` | bool | `false` | Log all requests (verbose) |
| `LogDetailedReasons` | bool | `true` | Log detection reasons |
| `LogPerformanceMetrics` | bool | `false` | Log timing/cache stats |
| `LogIpAddresses` | bool | `true` | Include IPs (disable for privacy) |
| `LogUserAgents` | bool | `true` | Include UAs (disable for privacy) |

## Whitelists and Blocklists

```json
{
  "BotDetection": {
    "WhitelistedBotPatterns": ["Googlebot", "Bingbot", "Slackbot"],
    "WhitelistedIps": ["192.168.1.0/24"],
    "BlacklistedIps": ["10.0.0.1"],
    "DatacenterIpPrefixes": ["3.0.0.0/8", "13.0.0.0/8"]
  }
}
```

### Default Whitelisted Bots

```
Googlebot, Bingbot, Slackbot, DuckDuckBot, Baiduspider,
YandexBot, Sogou, Exabot, facebot, ia_archiver
```

### Default Datacenter IP Prefixes

```
3.0.0.0/8, 13.0.0.0/8, 18.0.0.0/8, 52.0.0.0/8    (AWS)
20.0.0.0/8, 40.0.0.0/8, 104.0.0.0/8              (Azure)
34.0.0.0/8, 35.0.0.0/8                            (GCP)
138.0.0.0/8, 139.0.0.0/8, 140.0.0.0/8            (Oracle)
```

## Legacy Configuration

These properties are deprecated and will be removed in v2.0:

| Legacy | Replacement |
|--------|-------------|
| `EnableLlmDetection` | `EnableAiDetection` |
| `OllamaEndpoint` | `AiDetection.Ollama.Endpoint` |
| `OllamaModel` | `AiDetection.Ollama.Model` |
| `LlmTimeoutMs` | `AiDetection.TimeoutMs` |

## Example Configurations

See the `docs/` folder for example configurations:
- `appsettings.minimal.json` - Minimal setup
- `appsettings.typical.json` - Typical production
- `appsettings.full.json` - All options documented
