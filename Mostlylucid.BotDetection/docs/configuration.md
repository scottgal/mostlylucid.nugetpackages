# Configuration Reference

Complete configuration options for Mostlylucid.BotDetection.

## Quick Start Configurations

### Minimal Configuration

Basic detection without blocking - good for getting started:

```json
{
  "BotDetection": {
    "BotThreshold": 0.7

    // Uncomment to enable AI-powered detection with learning (RECOMMENDED):
    // "EnableAiDetection": true,
    // "AiDetection": {
    //   "Provider": "Onnx",
    //   "Onnx": { "AutoDownloadModel": true, "EnableHeuristicFallback": true }
    // },
    // "Learning": { "Enabled": true }
  }
}
```

### Typical Production Configuration (RECOMMENDED)

Full detection with ONNX AI, learning enabled, and stealth throttling:

```json
{
  "BotDetection": {
    // === Core Detection ===
    "BotThreshold": 0.7,
    "BlockDetectedBots": true,
    "DefaultActionPolicyName": "throttle-stealth",

    // === AI Detection with Learning (KEY FEATURE) ===
    "EnableAiDetection": true,
    "AiDetection": {
      "Provider": "Onnx",
      "TimeoutMs": 1000,
      "Onnx": {
        "AutoDownloadModel": true,
        "EnableHeuristicFallback": true,
        "UseGpu": false
      }
    },

    // === Learning System (Continuous Improvement) ===
    "Learning": {
      "Enabled": true,
      "LearningRate": 0.1,
      "EnableDriftDetection": true
    },

    // === Path-Based Policies ===
    "PathPolicies": {
      "/api/login": "strict",
      "/api/checkout/*": "strict",
      "/api/admin/**": "strict",
      "/sitemap.xml": "allowVerifiedBots",
      "/robots.txt": "allowVerifiedBots"
    },

    // === Action Policies ===
    "ActionPolicies": {
      "api-throttle": {
        "Type": "Throttle",
        "BaseDelayMs": 500,
        "MaxDelayMs": 10000,
        "ScaleByRisk": true,
        "JitterPercent": 0.5,
        "IncludeHeaders": false
      }
    }
  }
}
```

### Full Configuration (All Options)

Complete reference with all available options:

```json
{
  "BotDetection": {
    // ==========================================
    // CORE SETTINGS
    // ==========================================
    "BotThreshold": 0.7,
    "EnableUserAgentDetection": true,
    "EnableHeaderAnalysis": true,
    "EnableIpDetection": true,
    "EnableBehavioralAnalysis": true,
    "EnableTestMode": false,
    "MaxRequestsPerMinute": 60,
    "CacheDurationSeconds": 300,

    // ==========================================
    // BLOCKING SETTINGS
    // ==========================================
    "BlockDetectedBots": true,
    "BlockStatusCode": 403,
    "BlockMessage": "Access denied",
    "MinConfidenceToBlock": 0.8,
    "AllowVerifiedSearchEngines": true,
    "AllowSocialMediaBots": true,
    "AllowMonitoringBots": true,
    "DefaultActionPolicyName": "throttle-stealth",

    // ==========================================
    // AI DETECTION (KEY FEATURE)
    // ==========================================
    "EnableAiDetection": true,
    "AiDetection": {
      "Provider": "Onnx",
      "TimeoutMs": 1000,
      "MaxConcurrentRequests": 10,

      // ONNX Settings (recommended)
      "Onnx": {
        "ModelPath": "models/bot_classifier.onnx",
        "AutoDownloadModel": true,
        "ModelDownloadUrl": "",
        "UseGpu": false,
        "GpuDeviceId": 0,
        "EnableHeuristicFallback": true,
        "InferenceThreads": 4
      },

      // Ollama Settings (for LLM escalation)
      "Ollama": {
        "Endpoint": "http://localhost:11434",
        "Model": "gemma3:4b",
        "UseJsonMode": true,
        "Temperature": 0.1,
        "MaxTokens": 256
      }
    },

    // ==========================================
    // LEARNING SYSTEM (KEY FEATURE)
    // ==========================================
    "Learning": {
      "Enabled": true,
      "LearningRate": 0.1,
      "MaxSupport": 1000,
      "ScoreDecayTauHours": 168,
      "SupportDecayTauHours": 336,
      "Prior": 0.5,
      "PromoteToBadScore": 0.9,
      "PromoteToBadSupport": 50,
      "DemoteFromBadScore": 0.7,
      "DemoteFromBadSupport": 100,
      "GcEligibleDays": 90,
      "EnableDriftDetection": true,
      "DriftThreshold": 0.05
    },

    // ==========================================
    // DETECTION POLICIES
    // ==========================================
    "DefaultPolicyName": "default",
    "PathPolicies": {
      "/api/login": "strict",
      "/api/checkout/*": "strict",
      "/api/admin/**": "strict",
      "/static/*": "relaxed",
      "/sitemap.xml": "allowVerifiedBots"
    },
    "Policies": {
      "custom": {
        "Description": "Custom detection policy",
        "FastPath": ["UserAgent", "Header", "Ip"],
        "SlowPath": ["Behavioral", "Inconsistency"],
        "AiPath": ["Onnx"],
        "UseFastPath": true,
        "ForceSlowPath": false,
        "EscalateToAi": true,
        "AiEscalationThreshold": 0.6,
        "EarlyExitThreshold": 0.3,
        "ImmediateBlockThreshold": 0.95,
        "TimeoutMs": 15000,
        "ActionPolicyName": "throttle-stealth",
        "Weights": {
          "Behavioral": 2.0,
          "Inconsistency": 1.5
        },
        "Transitions": [
          { "WhenRiskExceeds": 0.9, "ActionPolicyName": "block-hard" },
          { "WhenSignal": "VerifiedGoodBot", "Action": "Allow" }
        ]
      }
    },

    // ==========================================
    // ACTION POLICIES
    // ==========================================
    "ActionPolicies": {
      "custom-block": {
        "Type": "Block",
        "StatusCode": 403,
        "Message": "Access denied",
        "IncludeRiskScore": false,
        "Headers": { "X-Reason": "bot-detected" }
      },
      "custom-throttle": {
        "Type": "Throttle",
        "BaseDelayMs": 500,
        "MaxDelayMs": 10000,
        "JitterPercent": 0.5,
        "ScaleByRisk": true,
        "IncludeHeaders": false
      }
    },

    // ==========================================
    // FAST PATH SETTINGS
    // ==========================================
    "FastPath": {
      "Enabled": true,
      "MaxParallelDetectors": 4,
      "EnableWaveParallelism": true,
      "WaveTimeoutMs": 50,
      "AbortThreshold": 0.95,
      "EarlyExitThreshold": 0.85,
      "SkipSlowPathThreshold": 0.2,
      "SlowPathTriggerThreshold": 0.5,
      "FastPathTimeoutMs": 100,
      "SlowPathQueueCapacity": 10000,
      "AlwaysRunFullOnPaths": ["/api/checkout", "/api/login"],
      "EnableDriftDetection": true,
      "DriftThreshold": 0.005,
      "EnableFeedbackLoop": true,
      "FeedbackMinConfidence": 0.9,
      "FeedbackMinOccurrences": 5
    },

    // ==========================================
    // BEHAVIORAL SETTINGS
    // ==========================================
    "Behavioral": {
      "ApiKeyHeader": "X-Api-Key",
      "ApiKeyRateLimit": 120,
      "UserIdClaim": "sub",
      "UserIdHeader": "X-User-Id",
      "UserRateLimit": 180,
      "EnableAnomalyDetection": true,
      "SpikeThresholdMultiplier": 5.0,
      "NewPathAnomalyThreshold": 0.8
    },

    // ==========================================
    // CLIENT-SIDE SETTINGS
    // ==========================================
    "ClientSide": {
      "Enabled": true,
      "TokenSecret": "your-secret-key-min-32-chars-long",
      "TokenLifetimeSeconds": 300,
      "FingerprintCacheDurationSeconds": 1800,
      "CollectWebGL": true,
      "CollectCanvas": true,
      "CollectAudio": false,
      "MinIntegrityScore": 70,
      "HeadlessThreshold": 0.5
    },

    // ==========================================
    // BACKGROUND UPDATES
    // ==========================================
    "EnableBackgroundUpdates": true,
    "UpdateIntervalHours": 24,
    "UpdateCheckIntervalMinutes": 60,
    "StartupDelaySeconds": 5,
    "ListDownloadTimeoutSeconds": 30,
    "MaxDownloadRetries": 3,

    // ==========================================
    // LOGGING
    // ==========================================
    "LogAllRequests": false,
    "LogDetailedReasons": true,
    "LogPerformanceMetrics": false,
    "LogIpAddresses": true,
    "LogUserAgents": true,

    // ==========================================
    // WHITELISTS / BLOCKLISTS
    // ==========================================
    "WhitelistedBotPatterns": ["Googlebot", "Bingbot", "Slackbot"],
    "WhitelistedIps": ["192.168.1.0/24"],
    "BlacklistedIps": [],
    "DatacenterIpPrefixes": []
  }
}
```

---

## Core Settings

| Option | Type | Default | Description |
|--------|------|---------|-------------|
| `BotThreshold` | double | `0.7` | Confidence threshold to classify as bot (0.0-1.0) |
| `EnableUserAgentDetection` | bool | `true` | Enable User-Agent pattern matching |
| `EnableHeaderAnalysis` | bool | `true` | Enable HTTP header inspection |
| `EnableIpDetection` | bool | `true` | Enable IP-based detection |
| `EnableBehavioralAnalysis` | bool | `true` | Enable behavioral rate analysis |
| `EnableAiDetection` | bool | `true` | **Enable AI-based classification (RECOMMENDED)** |
| `EnableTestMode` | bool | `false` | Enable test mode headers (dev only!) |
| `MaxRequestsPerMinute` | int | `60` | Rate limit threshold (1-10000) |
| `CacheDurationSeconds` | int | `300` | Cache duration for results (0-86400) |

---

## AI Detection Settings (KEY FEATURE)

AI detection provides machine learning-based classification with continuous learning. **This is a key differentiator** - the system improves over time.

```json
{
  "BotDetection": {
    "EnableAiDetection": true,
    "AiDetection": {
      "Provider": "Onnx",
      "TimeoutMs": 1000,
      "MaxConcurrentRequests": 10,
      "Onnx": {
        "ModelPath": "models/bot_classifier.onnx",
        "AutoDownloadModel": true,
        "ModelDownloadUrl": "",
        "UseGpu": false,
        "GpuDeviceId": 0,
        "EnableHeuristicFallback": true,
        "InferenceThreads": 4
      },
      "Ollama": {
        "Endpoint": "http://localhost:11434",
        "Model": "gemma3:4b",
        "UseJsonMode": true,
        "Temperature": 0.1,
        "MaxTokens": 256
      }
    }
  }
}
```

| Option | Type | Default | Description |
|--------|------|---------|-------------|
| `Provider` | string | `"Onnx"` | AI provider: `Onnx` (fast) or `Ollama` (LLM) |
| `TimeoutMs` | int | `1000` | Timeout for AI inference |
| `MaxConcurrentRequests` | int | `10` | Max parallel AI requests |

### ONNX Options

| Option | Type | Default | Description |
|--------|------|---------|-------------|
| `ModelPath` | string | `models/bot_classifier.onnx` | Path to ONNX model |
| `AutoDownloadModel` | bool | `true` | Auto-download if missing |
| `UseGpu` | bool | `false` | Enable CUDA GPU acceleration |
| `GpuDeviceId` | int | `0` | CUDA device ID |
| `EnableHeuristicFallback` | bool | `true` | Use heuristics when no model |
| `InferenceThreads` | int | `4` | CPU threads for inference |

See [ai-detection.md](ai-detection.md) for full details on GPU setup and model training.

---

## Learning System Settings (KEY FEATURE)

The learning system enables continuous improvement. Patterns are tracked, reputations evolve, and the model improves over time.

```json
{
  "BotDetection": {
    "Learning": {
      "Enabled": true,
      "LearningRate": 0.1,
      "MaxSupport": 1000,
      "ScoreDecayTauHours": 168,
      "SupportDecayTauHours": 336,
      "Prior": 0.5,
      "PromoteToBadScore": 0.9,
      "PromoteToBadSupport": 50,
      "DemoteFromBadScore": 0.7,
      "DemoteFromBadSupport": 100,
      "GcEligibleDays": 90,
      "EnableDriftDetection": true,
      "DriftThreshold": 0.05
    }
  }
}
```

| Option | Type | Default | Description |
|--------|------|---------|-------------|
| `Enabled` | bool | `true` | **Enable learning (RECOMMENDED)** |
| `LearningRate` | double | `0.1` | EMA learning rate (0.01-0.5) |
| `MaxSupport` | int | `1000` | Max effective sample count |
| `ScoreDecayTauHours` | int | `168` | Score decay time constant (7 days) |
| `SupportDecayTauHours` | int | `336` | Support decay time constant (14 days) |
| `Prior` | double | `0.5` | Neutral prior for new patterns |
| `PromoteToBadScore` | double | `0.9` | Score to promote to ConfirmedBad |
| `PromoteToBadSupport` | int | `50` | Support to promote to ConfirmedBad |
| `DemoteFromBadScore` | double | `0.7` | Score to demote from ConfirmedBad |
| `DemoteFromBadSupport` | int | `100` | Support to demote (hysteresis) |
| `GcEligibleDays` | int | `90` | Days before pattern GC eligible |
| `EnableDriftDetection` | bool | `true` | Detect concept drift |
| `DriftThreshold` | double | `0.05` | Drift alert threshold |

See [learning-and-reputation.md](learning-and-reputation.md) for full details.

---

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
| `DefaultActionPolicyName` | string | `"block"` | Default action policy |

---

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

---

## Client-Side Settings

```json
{
  "BotDetection": {
    "ClientSide": {
      "Enabled": true,
      "TokenSecret": "your-secret-key-min-32-chars-long",
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

---

## Background Update Settings

| Option | Type | Default | Description |
|--------|------|---------|-------------|
| `EnableBackgroundUpdates` | bool | `true` | Enable automatic list updates |
| `UpdateIntervalHours` | int | `24` | Hours between updates (1-168) |
| `UpdateCheckIntervalMinutes` | int | `60` | Minutes between update checks |
| `StartupDelaySeconds` | int | `5` | Delay before first update |
| `ListDownloadTimeoutSeconds` | int | `30` | Timeout per download |
| `MaxDownloadRetries` | int | `3` | Retries before giving up |

---

## Logging Settings

| Option | Type | Default | Description |
|--------|------|---------|-------------|
| `LogAllRequests` | bool | `false` | Log all requests (verbose) |
| `LogDetailedReasons` | bool | `true` | Log detection reasons |
| `LogPerformanceMetrics` | bool | `false` | Log timing/cache stats |
| `LogIpAddresses` | bool | `true` | Include IPs (disable for privacy) |
| `LogUserAgents` | bool | `true` | Include UAs (disable for privacy) |

---

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

---

## Legacy Configuration

These properties are deprecated and will be removed in v2.0:

| Legacy | Replacement |
|--------|-------------|
| `EnableLlmDetection` | `EnableAiDetection` |
| `OllamaEndpoint` | `AiDetection.Ollama.Endpoint` |
| `OllamaModel` | `AiDetection.Ollama.Model` |
| `LlmTimeoutMs` | `AiDetection.TimeoutMs` |

---

## Environment-Specific Examples

### Development

```json
{
  "BotDetection": {
    "BotThreshold": 0.5,
    "BlockDetectedBots": false,
    "EnableTestMode": true,
    "DefaultActionPolicyName": "debug",
    "EnableAiDetection": true,
    "Learning": { "Enabled": true },
    "LogAllRequests": true,
    "LogPerformanceMetrics": true
  }
}
```

### Staging

```json
{
  "BotDetection": {
    "BotThreshold": 0.7,
    "BlockDetectedBots": false,
    "DefaultActionPolicyName": "shadow",
    "EnableAiDetection": true,
    "Learning": { "Enabled": true, "EnableDriftDetection": true }
  }
}
```

### Production

```json
{
  "BotDetection": {
    "BotThreshold": 0.7,
    "BlockDetectedBots": true,
    "DefaultActionPolicyName": "throttle-stealth",
    "EnableAiDetection": true,
    "AiDetection": {
      "Provider": "Onnx",
      "Onnx": { "AutoDownloadModel": true, "UseGpu": false }
    },
    "Learning": {
      "Enabled": true,
      "EnableDriftDetection": true
    },
    "LogAllRequests": false,
    "LogPerformanceMetrics": false
  }
}
```
