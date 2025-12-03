# Detection Strategies

Mostlylucid.BotDetection uses multiple detection strategies that work together to provide comprehensive bot detection.

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

- **IP-based rate limiting** - Requests per minute per IP
- **Fingerprint-based tracking** - Per browser fingerprint hash (when client-side enabled)
- **API key tracking** - Per API key rate limiting (via configurable header)
- **User-based tracking** - Per authenticated user rate limiting (via claim or header)
- **Anomaly detection** - Detects sudden behavior changes (request spikes, new path access patterns)
- **Request timing analysis** - Detects suspiciously regular request intervals

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

- Excessive request rate per IP/fingerprint/API key/user
- Sudden request spikes (5x normal rate by default)
- Rapid sequential requests (<100ms between requests)
- Suspiciously regular timing (low standard deviation in intervals)
- Missing cookies across multiple requests
- Missing referrer on non-initial requests
- Accessing many new endpoints suddenly

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
- Notification permission inconsistencies

### Results via HttpContext

```csharp
var headlessLikelihood = context.GetHeadlessLikelihood();  // 0.0-1.0
var integrityScore = context.GetBrowserIntegrityScore();   // 0-100
```

## 6. Inconsistency Detection

Catches bots that spoof one signal but forget others:

- UA claims Chrome but missing modern Chrome headers (Sec-Fetch-Mode, sec-ch-ua)
- Desktop UA without Accept-Language header
- Generic `*/*` Accept header with browser UA
- Baidu/Yandex bot with wrong Accept-Language
- Referer from internal/localhost addresses
- HTTP/1.1 Connection header from modern browser

## 7. Risk Assessment

Use risk bands to decide how to handle requests:

```csharp
// Get risk band
var risk = context.GetRiskBand(); // Low, Elevated, Medium, High

// Check if should challenge (returns true for Elevated/Medium)
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

See [ai-detection.md](ai-detection.md) for details on ONNX and Ollama-based AI detection.
