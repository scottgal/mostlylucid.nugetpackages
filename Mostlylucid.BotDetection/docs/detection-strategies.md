# Detection Strategies

Mostlylucid.BotDetection uses multiple detection strategies that work together to provide comprehensive bot detection:

1. **User-Agent Detection** – Known bot pattern matching
2. **Header Detection** – HTTP header anomaly analysis
3. **IP Detection** – Datacenter/cloud IP identification
4. **Behavioral Analysis** – Request pattern monitoring
5. **Client-Side Fingerprinting** – Browser integrity checking (optional)
6. **Inconsistency Detection** – Cross-signal contradiction detection
7. **Risk Assessment** – Signal aggregation into risk bands
8. **AI Detection** – ML-based classification (optional)

> **How it works:** Strategies 1–6 generate raw signals; strategies 7–8 combine those signals into risk bands and AI-assisted decisions.

## Architecture Overview

Detection runs on a **signal-driven, event-based architecture** with two execution paths:

### Fast Path (Synchronous)
Low-latency detectors that run inline with the request:
- User-Agent, Header, IP, Behavioral analysis
- Completes in <100ms
- Uses consensus-based finalisation (all detectors report before scoring)
- Can trigger early exit if confidence exceeds threshold

### Slow Path (Asynchronous)
Background processing for heavier analysis:
- ONNX/LLM AI classification
- Learning and pattern discovery
- Runs via inter-request event bus (non-blocking)

```
┌─────────────────────────────────────────────────────────────────────┐
│ Request arrives                                                      │
├─────────────────────────────────────────────────────────────────────┤
│ FAST PATH (sync, <100ms)                                            │
│   ├─ Stage 0: UA → Headers → IP → ClientSide (parallel-safe)        │
│   ├─ Stage 1: Behavioral (depends on Stage 0)                       │
│   ├─ Stage 2: Inconsistency (reads all prior signals)               │
│   └─ Consensus check → Early exit if high confidence                │
├─────────────────────────────────────────────────────────────────────┤
│ SLOW PATH (async, background)                                        │
│   ├─ ONNX/LLM inference (if enabled)                                │
│   ├─ Pattern learning                                                │
│   └─ Training data collection                                        │
└─────────────────────────────────────────────────────────────────────┘
```

### Configuration

```json
{
  "BotDetection": {
    "FastPath": {
      "Enabled": true,
      "EarlyExitThreshold": 0.85,
      "SkipSlowPathThreshold": 0.2,
      "SlowPathTriggerThreshold": 0.5,
      "FastPathTimeoutMs": 100,
      "FastPathDetectors": [
        { "Name": "User-Agent Detector", "Signal": "UserAgentAnalyzed" },
        { "Name": "Header Detector", "Signal": "HeadersAnalyzed" },
        { "Name": "IP Detector", "Signal": "IpAnalyzed" },
        { "Name": "Behavioral Detector", "Signal": "BehaviourSampled" },
        { "Name": "Inconsistency Detector", "Signal": "InconsistencyUpdated" }
      ],
      "SlowPathDetectors": [
        { "Name": "ONNX Detector", "Signal": "AiClassificationCompleted" }
      ]
    }
  }
}
```

---

## 1. User-Agent Detection

Matches User-Agent strings against known bot patterns from multiple sources:

- Search engine bots (Googlebot, Bingbot, DuckDuckBot, etc.)
- Social media crawlers (FacebookBot, Twitterbot, LinkedInBot)
- SEO tools (AhrefsBot, SEMrushBot, MajesticBot)
- Scrapers and automation tools
- Known malicious bots

## 2. Header Detection

Analyzes HTTP headers for suspicious patterns:

- Missing standard browser headers
- Inconsistent Accept headers
- Missing or invalid Accept-Language
- Suspicious Connection headers
- Known bot header signatures

## 3. IP Detection

Checks client IP against:

- Known datacenter IP ranges (AWS, Azure, GCP, Oracle Cloud)
- Cloud provider ranges (auto-updated)
- Cloudflare IP ranges

## 4. Behavioral Analysis

Monitors request patterns at multiple identity levels:

- **Per-IP rate limiting** – requests per minute per IP
- **Per-fingerprint tracking** – browser fingerprint hash (when client-side enabled)
- **Per-API key tracking** – via configurable header
- **Per-user tracking** – via claim or header

### Configure Behavioral Analysis

```json
{
  "BotDetection": {
    "MaxRequestsPerMinute": 60,
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

### What Behavioral Analysis Detects

**Volume anomalies:**
- Excessive request rate per IP/fingerprint/API key/user
- Sudden request spikes (5x normal rate by default)
- Accessing many new endpoints suddenly

**Timing anomalies:**
- Rapid sequential requests (<100ms between requests)
- Suspiciously regular timing (low standard deviation in intervals)

**Session anomalies:**
- Missing cookies across multiple requests
- Missing referrer on non-initial requests

## 5. Client-Side Fingerprinting

JavaScript-based browser integrity checking that detects headless browsers and automation frameworks. Uses a signed token system (like XSRF) to prevent spoofing.

### Setup

1. Enable in configuration:
```json
{
  "BotDetection": {
    "ClientSide": {
      "Enabled": true,
      "TokenSecret": "your-secret-key-here",
      "TokenLifetimeSeconds": 300,
      "CollectWebGL": true,
      "CollectCanvas": true
    }
  }
}
```

2. Add the Tag Helper to your `_ViewImports.cshtml`:
```cshtml
@addTagHelper *, Mostlylucid.BotDetection
```

3. Add the script to your layout:
```html
<bot-detection-script />
<!-- or with options -->
<bot-detection-script endpoint="/bot-detection/fingerprint" defer="true" nonce="@cspNonce" />
```

4. Map the fingerprint endpoint in `Program.cs`:
```csharp
app.MapBotDetectionFingerprintEndpoint();
```

### What Client-Side Detection Detects

- `navigator.webdriver` flag (WebDriver)
- PhantomJS, Nightmare, Selenium markers
- Chrome DevTools Protocol (CDP/Puppeteer)
- Missing plugins in Chrome
- Zero outer window dimensions
- Prototype pollution (non-native `Function.bind`)
- Modified `eval.toString()` length
- Notification permissions inconsistencies

### Results via HttpContext

```csharp
var headlessLikelihood = context.GetHeadlessLikelihood();  // 0.0-1.0
var integrityScore = context.GetBrowserIntegrityScore();   // 0-100
```

- `GetHeadlessLikelihood()` returns 0.0–1.0 (higher = more likely headless)
- `GetBrowserIntegrityScore()` returns 0–100 (higher = more "real browser")

## 6. Inconsistency Detection

Catches bots that spoof one signal but forget others:

- UA claims Chrome but missing modern Chrome headers (Sec-Fetch-Mode, sec-ch-ua)
- Desktop UA without Accept-Language header
- Generic `*/*` Accept header with browser UA
- Baidu/Yandex bot with wrong Accept-Language
- Referer from internal/localhost addresses
- HTTP/1.1 Connection header from modern browser

All of these contribute to an internal inconsistency score (0–100), exposed via `context.GetInconsistencyScore()` and included in the overall risk band calculation.

## 7. Risk Assessment

Aggregates signals from strategies 1–6 into actionable risk bands:

| Risk Band | Meaning | Typical Action |
|-----------|---------|----------------|
| `Low` | Looks human | Allow |
| `Elevated` | Slightly suspicious | Allow or throttle |
| `Medium` | Clearly suspicious | Challenge recommended |
| `High` | Strong bot signal | Block |

```csharp
// Get risk band
var risk = context.GetRiskBand(); // Low, Elevated, Medium, High

// Check if should challenge (returns true for Medium/High)
if (context.ShouldChallengeRequest())
{
    return ChallengeWithCaptcha(context);
}

// Or get recommended action
var action = context.GetRecommendedAction(); // Allow, Throttle, Challenge, Block

// Get specific scores
var inconsistencyScore = context.GetInconsistencyScore(); // 0-100
```

## 8. AI Detection (Optional)

Use AI Detection when you need to catch sophisticated or evolving bots that evade pure pattern and heuristic-based methods.

See [ai-detection.md](ai-detection.md) for details on ONNX and Ollama-based AI detection.

---

## Signal Bus Architecture

Detection uses a dual-bus architecture:

### Intra-Request Bus (`BotSignalBus`)
- Per-request, short-lived
- Detectors publish signals as they complete
- Listeners react to signals in real-time
- Consensus-based: waits for all detectors before finalising

Signal types:
- `UserAgentAnalyzed` – UA detection complete
- `HeadersAnalyzed` – Header analysis complete
- `IpAnalyzed` – IP check complete
- `ClientFingerprintReceived` – Client-side data received
- `BehaviourSampled` – Behavioral analysis complete
- `InconsistencyUpdated` – Inconsistency check complete
- `AiClassificationCompleted` – AI/ML inference complete
- `DetectorComplete` – Generic detector completion
- `Finalising` – All detectors reported, scoring begins

### Inter-Request Bus (`LearningEventBus`)
- Long-lived, cross-request
- Background service processes events asynchronously
- Used for learning, pattern discovery, and analytics

Learning event types:
- `HighConfidenceDetection` – Bot detected with high confidence (training data)
- `PatternDiscovered` – New pattern found by AI
- `InconsistencyDetected` – Cross-signal mismatch found
- `UserFeedback` – User confirmed/denied bot detection
- `InferenceRequest` – Request for async AI inference
- `ModelUpdated` – ML model retrained

### Custom Signal Listeners

Implement `IBotSignalListener` to react to detection signals:

```csharp
public class MyCustomListener : IBotSignalListener, ISignalSubscriber
{
    public IEnumerable<BotSignalType> SubscribedSignals => new[]
    {
        BotSignalType.InconsistencyUpdated,
        BotSignalType.Finalising
    };

    public ValueTask OnSignalAsync(
        BotSignalType signal,
        DetectionContext context,
        CancellationToken ct = default)
    {
        if (signal == BotSignalType.InconsistencyUpdated)
        {
            var score = context.GetSignal<double>(SignalKeys.InconsistencyScore);
            // React to inconsistency detection...
        }
        return ValueTask.CompletedTask;
    }
}

// Register in DI
services.AddTransient<IBotSignalListener, MyCustomListener>();
```

### Custom Learning Event Handlers

Implement `ILearningEventHandler` for background processing:

```csharp
public class MyLearningHandler : ILearningEventHandler
{
    public IReadOnlySet<LearningEventType> HandledEventTypes => new HashSet<LearningEventType>
    {
        LearningEventType.HighConfidenceDetection
    };

    public async Task HandleAsync(LearningEvent evt, CancellationToken ct = default)
    {
        if (evt.Confidence >= 0.9 && evt.Features != null)
        {
            // Store for training, update patterns, etc.
            await SaveTrainingDataAsync(evt.Features, evt.Label ?? true, ct);
        }
    }
}

// Register in DI
services.AddSingleton<ILearningEventHandler, MyLearningHandler>();
```

---

## Pattern Reputation System (Learning + Forgetting)

The system learns new bot patterns AND forgets stale ones. This prevents the system from becoming paranoid over time as infrastructure changes (IP reassignments, proxy rotations, misconfigurations get fixed).

### Core Concepts

Each pattern (UA, IP, fingerprint, behavior cluster) has:

| Property | Description |
|----------|-------------|
| `BotScore` | 0.0 (human) to 1.0 (bot) - current belief |
| `Support` | Effective sample count (decays over time) |
| `State` | Neutral → Suspect → ConfirmedBad (with hysteresis) |
| `LastSeen` | For time decay when pattern goes quiet |

### States and Transitions

```
                    ┌──────────────────┐
                    │  ManuallyBlocked │ (admin override, never auto-downgrade)
                    └──────────────────┘
                             ↑ manual
┌─────────┐  score≥0.6   ┌─────────┐  score≥0.9   ┌──────────────┐
│ Neutral │ ──────────→  │ Suspect │ ──────────→  │ ConfirmedBad │
│         │  support≥10  │         │  support≥50  │              │
└─────────┘              └─────────┘              └──────────────┘
     ↑                        │                         │
     │ score≤0.4              │ score≤0.7               │
     └────────────────────────┘ support≥100 ────────────┘
                    (hysteresis: harder to forgive than accuse)
```

### Online Updates (when we see a pattern)

Each detection updates the pattern's reputation via EMA:

```
BotScore_new = (1 - α) * BotScore_old + α * label
```

Where:
- `α` = learning rate (default 0.1)
- `label` = 1.0 for bot, 0.0 for human

### Time Decay (when we DON'T see a pattern)

Stale patterns drift back toward neutral:

```
BotScore_new = BotScore_old + (prior - BotScore_old) * (1 - e^(-Δt/τ))
Support_new = Support_old * e^(-Δt/τ_support)
```

Where:
- `prior` = 0.5 (neutral)
- `τ` = 7 days (score decay)
- `τ_support` = 14 days (support decay)

### Garbage Collection

Patterns are removed when:
- `LastSeen` > 90 days ago
- `Support` < 1.0
- `State` = Neutral

### Configuration

```json
{
  "BotDetection": {
    "Reputation": {
      "LearningRate": 0.1,
      "MaxSupport": 1000,
      "ScoreDecayTauHours": 168,
      "SupportDecayTauHours": 336,
      "Prior": 0.5,
      "PromoteToBadScore": 0.9,
      "PromoteToBadSupport": 50,
      "DemoteFromBadScore": 0.7,
      "DemoteFromBadSupport": 100,
      "GcEligibleDays": 90
    }
  }
}
```

### How It Feeds Back to Fast Path

The fast path uses reputation state to determine behavior:

| State | Fast Path Behavior |
|-------|-------------------|
| `ConfirmedBad` | Can trigger fast-path abort (full UA weight) |
| `Suspect` | Contributes to score (half weight), can't abort alone |
| `Neutral` | Minimal contribution (10% weight) |
| `ConfirmedGood` | Reduces suspicion |
| `ManuallyBlocked` | Always blocked (admin override) |
| `ManuallyAllowed` | Always allowed (admin override) |

### Safety Rails

1. **Manual overrides are never auto-downgraded** - If admin blocks a pattern, only admin can unblock
2. **Asymmetric thresholds** - Promoting to bad needs 50 samples, demoting needs 100 (harder to forgive)
3. **Time decay prevents permanent bans** - Old patterns drift back to neutral
4. **GC only touches neutral patterns** - Active patterns are never auto-deleted
