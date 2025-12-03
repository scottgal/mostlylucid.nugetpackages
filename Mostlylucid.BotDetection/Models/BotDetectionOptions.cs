using Microsoft.Extensions.Options;

namespace Mostlylucid.BotDetection.Models;

/// <summary>
///     Configuration options for bot detection.
///     Supports a range of detection strategies from simple (static patterns) to advanced (LLM).
///     All options are designed to be fail-safe - failures are logged but never crash the app.
/// </summary>
public class BotDetectionOptions
{
    // ==========================================
    // Core Detection Settings
    // ==========================================

    /// <summary>
    ///     Confidence threshold above which a request is classified as a bot (default: 0.7)
    ///     Valid range: 0.0 to 1.0
    ///     Lower values = more aggressive detection (more false positives)
    ///     Higher values = more conservative (fewer false positives, may miss some bots)
    /// </summary>
    public double BotThreshold { get; set; } = 0.7;

    /// <summary>
    ///     Enable test mode (allows ml-bot-test-mode header to override detection)
    ///     WARNING: Only enable in development/testing environments!
    ///     In production, this header is completely ignored for security.
    /// </summary>
    public bool EnableTestMode { get; set; }

    // ==========================================
    // Detection Strategy Toggles
    // ==========================================

    /// <summary>
    ///     Enable user-agent based detection (static pattern matching - fastest)
    ///     Matches against known bot signatures from Matomo, crawler-user-agents, etc.
    ///     Recommended: Always enable unless you have specific requirements.
    /// </summary>
    public bool EnableUserAgentDetection { get; set; } = true;

    /// <summary>
    ///     Enable header analysis (examines Accept, Accept-Language, etc.)
    ///     Detects missing or suspicious HTTP headers that bots often omit.
    ///     Low overhead, recommended for most use cases.
    /// </summary>
    public bool EnableHeaderAnalysis { get; set; } = true;

    /// <summary>
    ///     Enable IP-based detection (checks against datacenter IP ranges)
    ///     Identifies requests from AWS, Azure, GCP, and other cloud providers.
    ///     Useful for detecting automated traffic from servers.
    /// </summary>
    public bool EnableIpDetection { get; set; } = true;

    /// <summary>
    ///     Enable behavioral analysis (rate limiting, request patterns)
    ///     Monitors request frequency per IP address.
    ///     Requires memory to track request counts.
    /// </summary>
    public bool EnableBehavioralAnalysis { get; set; } = true;

    /// <summary>
    ///     Enable AI-based detection (Ollama or ONNX).
    ///     Uses a local model to analyze suspicious patterns.
    ///     Higher latency but can detect sophisticated bots.
    ///     Configure provider via AiDetection section.
    /// </summary>
    public bool EnableLlmDetection { get; set; }

    // ==========================================
    // AI Detection Settings (Ollama or ONNX)
    // ==========================================

    /// <summary>
    ///     Configuration for AI-based bot detection.
    ///     Supports Ollama (LLM) or ONNX (classification model).
    /// </summary>
    public AiDetectionOptions AiDetection { get; set; } = new();

    // ==========================================
    // Blocking Policy Settings
    // ==========================================

    /// <summary>
    ///     Enable automatic blocking of detected bots.
    ///     When false, bots are detected and logged but not blocked.
    ///     Use endpoint-specific [BlockBots] attributes for fine-grained control.
    /// </summary>
    public bool BlockDetectedBots { get; set; } = false;

    /// <summary>
    ///     HTTP status code to return when blocking bots.
    ///     Common values: 403 (Forbidden), 429 (Too Many Requests), 503 (Service Unavailable)
    /// </summary>
    public int BlockStatusCode { get; set; } = 403;

    /// <summary>
    ///     Message to return in response body when blocking bots.
    /// </summary>
    public string BlockMessage { get; set; } = "Access denied";

    /// <summary>
    ///     Minimum confidence score required to block (when BlockDetectedBots is true).
    ///     Set higher than BotThreshold for conservative blocking.
    ///     Valid range: 0.0 to 1.0
    /// </summary>
    public double MinConfidenceToBlock { get; set; } = 0.8;

    /// <summary>
    ///     Allow verified search engine bots (Googlebot, Bingbot, etc.) through even when blocking.
    ///     Recommended: true, unless you have specific SEO requirements.
    /// </summary>
    public bool AllowVerifiedSearchEngines { get; set; } = true;

    /// <summary>
    ///     Allow social media preview bots (Facebook, Twitter, LinkedIn, etc.) through.
    /// </summary>
    public bool AllowSocialMediaBots { get; set; } = true;

    /// <summary>
    ///     Allow monitoring bots (UptimeRobot, Pingdom, etc.) through.
    /// </summary>
    public bool AllowMonitoringBots { get; set; } = true;

    // ==========================================
    // Legacy Ollama Settings (use AiDetection section instead)
    // ==========================================
    // These are kept for backwards compatibility.
    // New deployments should use AiDetection section.

    /// <summary>
    ///     [OBSOLETE] Use AiDetection.Ollama.Endpoint instead.
    ///     This property will be removed in a future version.
    /// </summary>
    [Obsolete("Use AiDetection.Ollama.Endpoint instead. This property will be removed in v1.0.")]
    public string? OllamaEndpoint { get; set; } = "http://localhost:11434";

    /// <summary>
    ///     [OBSOLETE] Use AiDetection.Ollama.Model instead.
    ///     This property will be removed in a future version.
    /// </summary>
    [Obsolete("Use AiDetection.Ollama.Model instead. This property will be removed in v1.0.")]
    public string? OllamaModel { get; set; } = "gemma3:1b";

    /// <summary>
    ///     [OBSOLETE] Use AiDetection.TimeoutMs instead.
    ///     This property will be removed in a future version.
    /// </summary>
    [Obsolete("Use AiDetection.TimeoutMs instead. This property will be removed in v1.0.")]
    public int LlmTimeoutMs { get; set; } = 2000;

    /// <summary>
    ///     [OBSOLETE] Use AiDetection.MaxConcurrentRequests instead.
    ///     This property will be removed in a future version.
    /// </summary>
    [Obsolete("Use AiDetection.MaxConcurrentRequests instead. This property will be removed in v1.0.")]
    public int MaxConcurrentLlmRequests { get; set; } = 5;

    // ==========================================
    // Behavioral Analysis Settings
    // ==========================================

    /// <summary>
    ///     Maximum requests per IP per minute for behavioral analysis (default: 60)
    ///     Valid range: 1 to 10000
    ///     Requests above this threshold increase bot confidence score.
    /// </summary>
    public int MaxRequestsPerMinute { get; set; } = 60;

    /// <summary>
    ///     Time window for behavioral analysis in seconds.
    ///     Tracks request counts within this sliding window.
    /// </summary>
    public int BehavioralWindowSeconds { get; set; } = 60;

    /// <summary>
    ///     Advanced behavioral analysis configuration.
    ///     Enables tracking at multiple identity levels (fingerprint, API key, user ID).
    /// </summary>
    public BehavioralOptions Behavioral { get; set; } = new();

    // ==========================================
    // Caching Settings
    // ==========================================

    /// <summary>
    ///     Cache duration for detection results in seconds (default: 300)
    ///     Valid range: 0 to 86400 (24 hours)
    ///     Set to 0 to disable caching (not recommended for production).
    /// </summary>
    public int CacheDurationSeconds { get; set; } = 300;

    /// <summary>
    ///     Maximum number of cached detection results.
    ///     Prevents memory exhaustion from cache growth.
    /// </summary>
    public int MaxCacheEntries { get; set; } = 10000;

    // ==========================================
    // Background Update Service Settings
    // ==========================================

    /// <summary>
    ///     Enable the background service that updates bot lists automatically.
    ///     When disabled, lists are only loaded once at startup.
    /// </summary>
    public bool EnableBackgroundUpdates { get; set; } = true;

    /// <summary>
    ///     Interval between bot list update checks in hours (default: 24)
    ///     Valid range: 1 to 168 (1 week)
    ///     Lists are only downloaded if they're older than this.
    /// </summary>
    public int UpdateIntervalHours { get; set; } = 24;

    /// <summary>
    ///     Interval between update check polls in minutes (default: 60)
    ///     The service checks if an update is needed at this interval.
    ///     Valid range: 5 to 1440 (24 hours)
    /// </summary>
    public int UpdateCheckIntervalMinutes { get; set; } = 60;

    /// <summary>
    ///     Timeout for downloading bot lists in seconds (default: 30)
    ///     If exceeded, the download fails gracefully and retries later.
    /// </summary>
    public int ListDownloadTimeoutSeconds { get; set; } = 30;

    /// <summary>
    ///     Maximum retries for failed list downloads before giving up until next interval.
    /// </summary>
    public int MaxDownloadRetries { get; set; } = 3;

    /// <summary>
    ///     Delay startup initialization to avoid slowing app startup.
    ///     Lists will be loaded after this delay.
    /// </summary>
    public int StartupDelaySeconds { get; set; } = 5;

    // ==========================================
    // External Data Sources Configuration
    // ==========================================

    /// <summary>
    ///     Configuration for all external data sources.
    ///     Each source can be individually enabled/disabled with custom URLs.
    ///     See DataSourceOptions for default URLs and documentation.
    /// </summary>
    public DataSourcesOptions DataSources { get; set; } = new();

    // ==========================================
    // Pattern Learning Settings (Future)
    // ==========================================

    /// <summary>
    ///     Enable learning and storing new bot patterns.
    ///     Patterns are learned from requests with high confidence scores.
    /// </summary>
    public bool EnablePatternLearning { get; set; } = false;

    /// <summary>
    ///     Minimum confidence score to learn a pattern.
    ///     Only requests above this threshold contribute to learning.
    /// </summary>
    public double MinConfidenceToLearn { get; set; } = 0.9;

    /// <summary>
    ///     Maximum number of learned patterns to store.
    ///     Oldest patterns are removed when limit is reached.
    /// </summary>
    public int MaxLearnedPatterns { get; set; } = 1000;

    /// <summary>
    ///     Interval for consolidating/cleaning learned patterns in hours.
    ///     Removes low-value and duplicate patterns.
    /// </summary>
    public int PatternConsolidationIntervalHours { get; set; } = 24;

    // ==========================================
    // Storage Settings
    // ==========================================

    /// <summary>
    ///     Storage provider for bot patterns and IP ranges.
    ///     - Sqlite (default): Fast, recommended for production
    ///     - Json: Simple file-based, useful for debugging or small deployments
    /// </summary>
    public StorageProvider StorageProvider { get; set; } = StorageProvider.Sqlite;

    /// <summary>
    ///     Path to the storage file (SQLite database or JSON file).
    ///     Default for SQLite: {AppContext.BaseDirectory}/botdetection.db
    ///     Default for JSON: {AppContext.BaseDirectory}/botdetection.json
    /// </summary>
    public string? DatabasePath { get; set; }

    /// <summary>
    ///     Enable database WAL mode for better concurrent access (SQLite only).
    ///     Recommended for production.
    /// </summary>
    public bool EnableDatabaseWalMode { get; set; } = true;

    // ==========================================
    // Whitelists and Customization
    // ==========================================

    /// <summary>
    ///     Known good bot patterns (won't be flagged even if other signals present)
    /// </summary>
    public List<string> WhitelistedBotPatterns { get; set; } =
    [
        "Googlebot", "Bingbot", "Slackbot", "DuckDuckBot", "Baiduspider",
        "YandexBot", "Sogou", "Exabot", "facebot", "ia_archiver"
    ];

    /// <summary>
    ///     Known datacenter/hosting IP ranges (CIDR notation, increases suspicion)
    /// </summary>
    public List<string> DatacenterIpPrefixes { get; set; } =
    [
        "3.0.0.0/8", "13.0.0.0/8", "18.0.0.0/8", "52.0.0.0/8", // AWS
        "20.0.0.0/8", "40.0.0.0/8", "104.0.0.0/8", // Azure
        "34.0.0.0/8", "35.0.0.0/8", // GCP
        "138.0.0.0/8", "139.0.0.0/8", "140.0.0.0/8" // Oracle Cloud
    ];

    /// <summary>
    ///     Custom bot patterns to add to detection (regex patterns).
    /// </summary>
    public List<string> CustomBotPatterns { get; set; } = [];

    /// <summary>
    ///     IP addresses or CIDR ranges to always allow (bypass detection).
    /// </summary>
    public List<string> WhitelistedIps { get; set; } = [];

    /// <summary>
    ///     IP addresses or CIDR ranges to always block.
    /// </summary>
    public List<string> BlacklistedIps { get; set; } = [];

    // ==========================================
    // Logging Settings
    // ==========================================

    /// <summary>
    ///     Log all detection results (not just bots).
    ///     Useful for debugging but can be verbose.
    /// </summary>
    public bool LogAllRequests { get; set; } = false;

    /// <summary>
    ///     Log detailed detection reasons.
    /// </summary>
    public bool LogDetailedReasons { get; set; } = true;

    /// <summary>
    ///     Log performance metrics (processing time, cache hits, etc.)
    /// </summary>
    public bool LogPerformanceMetrics { get; set; } = false;

    /// <summary>
    ///     Log IP addresses in logs (disable for privacy compliance).
    /// </summary>
    public bool LogIpAddresses { get; set; } = true;

    /// <summary>
    ///     Log user agent strings in logs (disable for privacy compliance).
    /// </summary>
    public bool LogUserAgents { get; set; } = true;

    // ==========================================
    // Client-Side Detection Settings
    // ==========================================

    /// <summary>
    ///     Configuration for client-side browser fingerprinting.
    ///     Enables JavaScript-based headless browser and automation detection.
    /// </summary>
    public ClientSideOptions ClientSide { get; set; } = new();
}

// ==========================================
// Client-Side Detection Configuration
// ==========================================

/// <summary>
///     Configuration for client-side browser fingerprinting and headless detection.
///     Uses a lightweight JavaScript snippet to collect browser signals.
/// </summary>
public class ClientSideOptions
{
    /// <summary>
    ///     Enable client-side browser fingerprinting.
    ///     When enabled, use the &lt;bot-detection-script /&gt; tag helper to inject the JS.
    ///     Default: false
    /// </summary>
    public bool Enabled { get; set; } = false;

    /// <summary>
    ///     Secret key for signing browser tokens (like XSRF tokens).
    ///     Tokens are used to validate fingerprint submissions and prevent spoofing.
    ///     If not set, a random key is generated (tokens won't survive restarts).
    ///     Recommended: Set a stable secret in production.
    /// </summary>
    public string? TokenSecret { get; set; }

    /// <summary>
    ///     Token lifetime in seconds.
    ///     Fingerprint must be submitted within this time of page load.
    ///     Default: 300 (5 minutes)
    /// </summary>
    public int TokenLifetimeSeconds { get; set; } = 300;

    /// <summary>
    ///     How long to cache fingerprint results for correlation with requests.
    ///     Default: 1800 (30 minutes)
    /// </summary>
    public int FingerprintCacheDurationSeconds { get; set; } = 1800;

    /// <summary>
    ///     Client-side collection timeout in milliseconds.
    ///     Default: 5000 (5 seconds)
    /// </summary>
    public int CollectionTimeoutMs { get; set; } = 5000;

    /// <summary>
    ///     Collect WebGL fingerprint data (vendor, renderer).
    ///     Provides higher entropy but some consider it more invasive.
    ///     Default: true
    /// </summary>
    public bool CollectWebGL { get; set; } = true;

    /// <summary>
    ///     Collect canvas fingerprint hash.
    ///     Used for consistency checking, not full fingerprinting.
    ///     Default: true
    /// </summary>
    public bool CollectCanvas { get; set; } = true;

    /// <summary>
    ///     Collect audio context fingerprint.
    ///     Currently not implemented, reserved for future use.
    ///     Default: false
    /// </summary>
    public bool CollectAudio { get; set; } = false;

    /// <summary>
    ///     Minimum browser integrity score to consider "trusted".
    ///     Scores below this contribute to bot confidence.
    ///     Range: 0-100. Default: 70
    /// </summary>
    public int MinIntegrityScore { get; set; } = 70;

    /// <summary>
    ///     Headless likelihood threshold above which to flag as bot.
    ///     Range: 0.0-1.0. Default: 0.5
    /// </summary>
    public double HeadlessThreshold { get; set; } = 0.5;
}

// ==========================================
// Behavioral Analysis Configuration
// ==========================================

/// <summary>
///     Advanced configuration for behavioral analysis.
///     Enables tracking at multiple identity levels beyond IP address.
/// </summary>
public class BehavioralOptions
{
    /// <summary>
    ///     HTTP header name to extract API key from for per-API-key rate limiting.
    ///     Example: "X-Api-Key", "Authorization"
    ///     Leave empty to disable API key tracking.
    /// </summary>
    public string? ApiKeyHeader { get; set; }

    /// <summary>
    ///     Rate limit per API key per minute.
    ///     If 0, defaults to MaxRequestsPerMinute * 2.
    /// </summary>
    public int ApiKeyRateLimit { get; set; } = 0;

    /// <summary>
    ///     Claim name to extract user ID from for per-user rate limiting.
    ///     Example: "sub", "nameidentifier", "userId"
    ///     Used when User.Identity.IsAuthenticated is true.
    /// </summary>
    public string? UserIdClaim { get; set; }

    /// <summary>
    ///     HTTP header name to extract user ID from (fallback when not authenticated).
    ///     Example: "X-User-Id"
    ///     Leave empty to disable header-based user tracking.
    /// </summary>
    public string? UserIdHeader { get; set; }

    /// <summary>
    ///     Rate limit per authenticated user per minute.
    ///     If 0, defaults to MaxRequestsPerMinute * 3.
    /// </summary>
    public int UserRateLimit { get; set; } = 0;

    /// <summary>
    ///     Enable behavior anomaly detection (sudden request spikes, unusual path access).
    ///     Detects when an identity suddenly changes behavior patterns.
    ///     Default: true
    /// </summary>
    public bool EnableAnomalyDetection { get; set; } = true;

    /// <summary>
    ///     Threshold multiplier for detecting request spikes.
    ///     A spike is detected when current rate exceeds average * this multiplier.
    ///     Default: 5.0 (5x the normal rate)
    /// </summary>
    public double SpikeThresholdMultiplier { get; set; } = 5.0;

    /// <summary>
    ///     Threshold for new path access rate to consider anomalous.
    ///     Range: 0.0-1.0. If 80%+ of recent requests are to new paths, flag as anomaly.
    ///     Default: 0.8
    /// </summary>
    public double NewPathAnomalyThreshold { get; set; } = 0.8;
}

// ==========================================
// Storage Provider Configuration
// ==========================================

/// <summary>
///     Specifies the storage provider for bot patterns and IP ranges.
/// </summary>
public enum StorageProvider
{
    /// <summary>
    ///     SQLite database storage (default).
    ///     Fast indexed queries, good for production with many patterns.
    ///     File: botdetection.db
    /// </summary>
    Sqlite,

    /// <summary>
    ///     JSON file storage.
    ///     Simple, human-readable, good for debugging or small deployments.
    ///     Loads entire file into memory on each operation.
    ///     File: botdetection.json
    /// </summary>
    Json
}

// ==========================================
// AI Detection Configuration Classes
// ==========================================

/// <summary>
///     Specifies the AI provider for bot detection.
/// </summary>
public enum AiProvider
{
    /// <summary>
    ///     Use Ollama with a local LLM (requires Ollama server).
    ///     BEST QUALITY: Full reasoning capabilities, can explain decisions, handles edge cases well.
    ///     REQUIREMENTS: Ollama server running, ~1-4GB RAM depending on model.
    ///     LATENCY: 50-500ms per request depending on model size.
    ///     USE WHEN: You need high accuracy and have Ollama infrastructure.
    /// </summary>
    Ollama,

    /// <summary>
    ///     Use ONNX Runtime with a lightweight classification model.
    ///     COMPACT: Works offline, no external dependencies, minimal resources.
    ///     QUALITY: Good for common patterns, less nuanced than LLM.
    ///     LATENCY: 1-10ms per request (very fast).
    ///     USE WHEN: You need fast, lightweight detection without external servers.
    ///     NOTE: Falls back to heuristics if no model is available.
    /// </summary>
    Onnx
}

/// <summary>
///     Configuration for AI-based bot detection.
///     Supports Ollama (LLM) or ONNX (classification model).
/// </summary>
public class AiDetectionOptions
{
    /// <summary>
    ///     The AI provider to use for bot detection.
    ///     Default: Ollama (for backwards compatibility)
    /// </summary>
    public AiProvider Provider { get; set; } = AiProvider.Ollama;

    /// <summary>
    ///     Timeout for AI detection in milliseconds.
    ///     If exceeded, AI detection is skipped (fail-safe).
    ///     Valid range: 100 to 30000. Default: 2000ms
    /// </summary>
    public int TimeoutMs { get; set; } = 2000;

    /// <summary>
    ///     Maximum concurrent AI requests.
    ///     Prevents overwhelming the AI backend.
    ///     Valid range: 1 to 100. Default: 5
    /// </summary>
    public int MaxConcurrentRequests { get; set; } = 5;

    /// <summary>
    ///     Ollama-specific configuration.
    ///     Only used when Provider is Ollama.
    /// </summary>
    public OllamaOptions Ollama { get; set; } = new();

    /// <summary>
    ///     ONNX-specific configuration.
    ///     Only used when Provider is Onnx.
    /// </summary>
    public OnnxOptions Onnx { get; set; } = new();
}

/// <summary>
///     Ollama-specific configuration for AI detection.
/// </summary>
public class OllamaOptions
{
    /// <summary>
    ///     Ollama API endpoint URL.
    ///     Default: "http://localhost:11434"
    /// </summary>
    public string Endpoint { get; set; } = "http://localhost:11434";

    /// <summary>
    ///     Ollama model to use for bot detection.
    ///     Default: "gemma3:1b" (1B params, 8K context, fast)
    ///     Alternatives:
    ///     - "qwen2.5:1.5b" - Good reasoning, slightly larger
    ///     - "phi3:mini" - Microsoft's small model
    ///     - "tinyllama" - Very small, basic classification
    /// </summary>
    public string Model { get; set; } = "gemma3:1b";

    /// <summary>
    ///     Whether to use JSON mode for structured output.
    ///     When true, uses Ollama's JSON mode for reliable parsing.
    ///     Default: true
    /// </summary>
    public bool UseJsonMode { get; set; } = true;

    /// <summary>
    ///     Custom system prompt for bot detection.
    ///     Use {REQUEST_INFO} as placeholder for the request data.
    ///     If empty, uses the default compact prompt optimized for small models.
    ///     Default prompt (~350 tokens) is designed for 8K context models like gemma3:1b.
    /// </summary>
    public string? CustomPrompt { get; set; }

    /// <summary>
    ///     Default compact prompt template for bot detection.
    ///     Optimized for minimal token usage with small models.
    ///     Uses strict JSON schema to prevent malformed output.
    /// </summary>
    public const string DefaultPrompt = @"You are a bot detector. Analyze the HTTP request below and classify it.

REQUEST:
{REQUEST_INFO}

RULES:
- Bot indicators: missing Accept-Language, missing Referer, simple User-Agent, */* Accept header
- Known bots: curl, wget, python-requests, scrapy, selenium, headless, phantom
- When uncertain, classify as human (isBot=false)

OUTPUT: Return ONLY a single JSON object matching this exact schema:
{
  ""isBot"": <boolean>,
  ""confidence"": <number 0.0-1.0>,
  ""reasoning"": ""<max 50 chars>"",
  ""botType"": ""<scraper|searchengine|monitor|malicious|unknown>"",
  ""pattern"": ""<identifier or empty>""
}

JSON:";
}

/// <summary>
///     ONNX-specific configuration for AI detection.
///     Uses a lightweight classification model for bot detection.
///     COMPACT but less accurate than Ollama LLM - good for fast, offline detection.
/// </summary>
public class OnnxOptions
{
    /// <summary>
    ///     Path to the ONNX model file.
    ///     If not specified, looks for {AppContext.BaseDirectory}/models/bot_classifier.onnx
    ///     The models/ directory is created automatically and should be gitignored.
    /// </summary>
    public string? ModelPath { get; set; }

    /// <summary>
    ///     URL to download the ONNX model from if not present locally.
    ///     Set to empty string to disable auto-download and use heuristic fallback.
    ///     You can train your own model or use a pre-trained text classifier.
    /// </summary>
    public string ModelDownloadUrl { get; set; } = "";

    /// <summary>
    ///     Whether to download the model automatically at startup.
    ///     If false and model doesn't exist, falls back to heuristic detection.
    ///     Default: true
    /// </summary>
    public bool AutoDownloadModel { get; set; } = true;

    /// <summary>
    ///     Whether to use GPU acceleration if available.
    ///     Requires Microsoft.ML.OnnxRuntime.Gpu package.
    ///     Default: false (CPU only - recommended for most deployments)
    /// </summary>
    public bool UseGpu { get; set; } = false;

    /// <summary>
    ///     Enable heuristic fallback when no model is available.
    ///     The heuristic uses feature weights learned from typical bot patterns.
    ///     Default: true
    /// </summary>
    public bool EnableHeuristicFallback { get; set; } = true;
}

// ==========================================
// Data Source Configuration Classes
// ==========================================

/// <summary>
///     Configuration for all external data sources used by bot detection.
///     Each source can be individually enabled/disabled with custom URLs.
/// </summary>
public class DataSourcesOptions
{
    // ==========================================
    // Bot Pattern Sources (User-Agent matching)
    // ==========================================

    /// <summary>
    ///     IsBot patterns - the most comprehensive bot pattern source.
    ///     Aggregates patterns from: crawler-user-agents, matomo, myip.ms, and more.
    ///     Enabled by default as the primary pattern source.
    /// </summary>
    public DataSourceConfig IsBot { get; set; } = new()
    {
        Enabled = true,
        Url = "https://raw.githubusercontent.com/omrilotan/isbot/main/src/patterns.json",
        Description = "IsBot patterns from omrilotan/isbot - comprehensive bot regex patterns (JSON array)"
    };

    /// <summary>
    ///     Matomo Device Detector bot list.
    ///     Provides categorized bot patterns with metadata (name, category, url).
    ///     Disabled by default as isbot already incorporates these patterns.
    ///     Enable if you need bot category information.
    /// </summary>
    public DataSourceConfig Matomo { get; set; } = new()
    {
        Enabled = false,
        Url = "https://raw.githubusercontent.com/matomo-org/device-detector/master/regexes/bots.yml",
        Description = "Matomo Device Detector - categorized bot patterns with metadata (YAML)"
    };

    /// <summary>
    ///     Crawler User Agents list.
    ///     Community-maintained list with crawler URLs.
    ///     Disabled by default as isbot already incorporates these patterns.
    /// </summary>
    public DataSourceConfig CrawlerUserAgents { get; set; } = new()
    {
        Enabled = false,
        Url = "https://raw.githubusercontent.com/monperrus/crawler-user-agents/master/crawler-user-agents.json",
        Description = "Crawler User Agents - community-maintained crawler patterns (JSON)"
    };

    // ==========================================
    // IP Range Sources (Datacenter detection)
    // ==========================================

    /// <summary>
    ///     AWS IP ranges - official Amazon IP ranges.
    ///     Used to detect requests from AWS infrastructure.
    /// </summary>
    public DataSourceConfig AwsIpRanges { get; set; } = new()
    {
        Enabled = true,
        Url = "https://ip-ranges.amazonaws.com/ip-ranges.json",
        Description = "AWS IP ranges - official Amazon cloud IP ranges (JSON)"
    };

    /// <summary>
    ///     Google Cloud IP ranges - official GCP IP ranges.
    ///     Used to detect requests from Google Cloud infrastructure.
    /// </summary>
    public DataSourceConfig GcpIpRanges { get; set; } = new()
    {
        Enabled = true,
        Url = "https://www.gstatic.com/ipranges/cloud.json",
        Description = "Google Cloud IP ranges - official GCP IP ranges (JSON)"
    };

    /// <summary>
    ///     Azure IP ranges - official Microsoft Azure IP ranges.
    ///     Disabled by default as the download URL changes weekly.
    ///     You must manually update the URL from:
    ///     https://www.microsoft.com/en-us/download/details.aspx?id=56519
    /// </summary>
    public DataSourceConfig AzureIpRanges { get; set; } = new()
    {
        Enabled = false,
        Url = "",
        Description = "Azure IP ranges - URL changes weekly, requires manual update"
    };

    /// <summary>
    ///     Cloudflare IPv4 ranges - official Cloudflare IP ranges.
    ///     Can be used to identify traffic proxied through Cloudflare.
    /// </summary>
    public DataSourceConfig CloudflareIpv4 { get; set; } = new()
    {
        Enabled = true,
        Url = "https://www.cloudflare.com/ips-v4",
        Description = "Cloudflare IPv4 ranges - official Cloudflare IPs (text, one CIDR per line)"
    };

    /// <summary>
    ///     Cloudflare IPv6 ranges - official Cloudflare IP ranges.
    /// </summary>
    public DataSourceConfig CloudflareIpv6 { get; set; } = new()
    {
        Enabled = true,
        Url = "https://www.cloudflare.com/ips-v6",
        Description = "Cloudflare IPv6 ranges - official Cloudflare IPs (text, one CIDR per line)"
    };
}

/// <summary>
///     Configuration for a single external data source.
/// </summary>
public class DataSourceConfig
{
    /// <summary>
    ///     Whether this data source is enabled.
    ///     Disabled sources are not fetched.
    /// </summary>
    public bool Enabled { get; set; }

    /// <summary>
    ///     URL to fetch the data from.
    ///     Leave empty to disable fetching (uses fallback patterns).
    /// </summary>
    public string Url { get; set; } = "";

    /// <summary>
    ///     Human-readable description of this data source.
    ///     For documentation purposes.
    /// </summary>
    public string Description { get; set; } = "";
}

/// <summary>
///     Validates BotDetectionOptions on startup.
///     Invalid configuration logs warnings but doesn't crash the app.
/// </summary>
public class BotDetectionOptionsValidator : IValidateOptions<BotDetectionOptions>
{
    public ValidateOptionsResult Validate(string? name, BotDetectionOptions options)
    {
        var errors = new List<string>();
        var warnings = new List<string>();

        // Critical validations (would cause runtime errors)
        if (options.BotThreshold < 0.0 || options.BotThreshold > 1.0)
            errors.Add($"BotThreshold must be between 0.0 and 1.0, got {options.BotThreshold}");

        if (options.AiDetection.TimeoutMs < 100 || options.AiDetection.TimeoutMs > 30000)
            errors.Add($"AiDetection.TimeoutMs must be between 100 and 30000, got {options.AiDetection.TimeoutMs}");

        if (options.MaxRequestsPerMinute < 1 || options.MaxRequestsPerMinute > 10000)
            errors.Add($"MaxRequestsPerMinute must be between 1 and 10000, got {options.MaxRequestsPerMinute}");

        if (options.CacheDurationSeconds < 0 || options.CacheDurationSeconds > 86400)
            errors.Add($"CacheDurationSeconds must be between 0 and 86400, got {options.CacheDurationSeconds}");

        if (options.UpdateIntervalHours < 1 || options.UpdateIntervalHours > 168)
            errors.Add($"UpdateIntervalHours must be between 1 and 168, got {options.UpdateIntervalHours}");

        if (options.UpdateCheckIntervalMinutes < 5 || options.UpdateCheckIntervalMinutes > 1440)
            errors.Add($"UpdateCheckIntervalMinutes must be between 5 and 1440, got {options.UpdateCheckIntervalMinutes}");

        // Validate Ollama settings only when using Ollama provider
        if (options.EnableLlmDetection && options.AiDetection.Provider == AiProvider.Ollama)
        {
            if (string.IsNullOrWhiteSpace(options.AiDetection.Ollama.Endpoint))
                errors.Add("AiDetection.Ollama.Endpoint must be specified when using Ollama provider");

            if (string.IsNullOrWhiteSpace(options.AiDetection.Ollama.Model))
                errors.Add("AiDetection.Ollama.Model must be specified when using Ollama provider");
        }

        if (options.MinConfidenceToBlock < options.BotThreshold)
            warnings.Add($"MinConfidenceToBlock ({options.MinConfidenceToBlock}) is less than BotThreshold ({options.BotThreshold}), this may cause unexpected blocking");

        // Validate BehavioralOptions
        ValidateBehavioralOptions(options.Behavioral, errors, warnings);

        // Validate ClientSideOptions
        ValidateClientSideOptions(options.ClientSide, errors, warnings);

        // Validate CIDR patterns
        foreach (var prefix in options.DatacenterIpPrefixes)
        {
            if (!IsValidCidr(prefix))
                errors.Add($"Invalid CIDR notation in DatacenterIpPrefixes: {prefix}");
        }

        foreach (var ip in options.WhitelistedIps)
        {
            if (!IsValidIpOrCidr(ip))
                errors.Add($"Invalid IP or CIDR in WhitelistedIps: {ip}");
        }

        foreach (var ip in options.BlacklistedIps)
        {
            if (!IsValidIpOrCidr(ip))
                errors.Add($"Invalid IP or CIDR in BlacklistedIps: {ip}");
        }

        // Return errors, but log warnings
        return errors.Count > 0
            ? ValidateOptionsResult.Fail(errors)
            : ValidateOptionsResult.Success;
    }

    private static bool IsValidCidr(string cidr)
    {
        var parts = cidr.Split('/');
        if (parts.Length != 2) return false;

        if (!System.Net.IPAddress.TryParse(parts[0], out _))
            return false;

        if (!int.TryParse(parts[1], out var prefix))
            return false;

        return prefix >= 0 && prefix <= 128;
    }

    private static bool IsValidIpOrCidr(string value)
    {
        if (value.Contains('/'))
            return IsValidCidr(value);

        return System.Net.IPAddress.TryParse(value, out _);
    }

    private static void ValidateBehavioralOptions(BehavioralOptions options, List<string> errors, List<string> warnings)
    {
        // Rate limit validations
        if (options.ApiKeyRateLimit < 0)
            errors.Add($"Behavioral.ApiKeyRateLimit cannot be negative, got {options.ApiKeyRateLimit}");

        if (options.UserRateLimit < 0)
            errors.Add($"Behavioral.UserRateLimit cannot be negative, got {options.UserRateLimit}");

        // Spike threshold validation
        if (options.SpikeThresholdMultiplier < 1.0)
            errors.Add($"Behavioral.SpikeThresholdMultiplier must be >= 1.0, got {options.SpikeThresholdMultiplier}");

        if (options.SpikeThresholdMultiplier > 100.0)
            warnings.Add($"Behavioral.SpikeThresholdMultiplier is very high ({options.SpikeThresholdMultiplier}), spike detection may be ineffective");

        // New path anomaly threshold validation
        if (options.NewPathAnomalyThreshold < 0.0 || options.NewPathAnomalyThreshold > 1.0)
            errors.Add($"Behavioral.NewPathAnomalyThreshold must be between 0.0 and 1.0, got {options.NewPathAnomalyThreshold}");

        // Warn if API key header is set but rate limit is 0
        if (!string.IsNullOrEmpty(options.ApiKeyHeader) && options.ApiKeyRateLimit == 0)
            warnings.Add("Behavioral.ApiKeyHeader is set but ApiKeyRateLimit is 0 (will use 2x MaxRequestsPerMinute as default)");

        // Warn if user ID is configured but rate limit is 0
        if ((!string.IsNullOrEmpty(options.UserIdHeader) || !string.IsNullOrEmpty(options.UserIdClaim)) && options.UserRateLimit == 0)
            warnings.Add("User ID tracking is configured but UserRateLimit is 0 (will use 3x MaxRequestsPerMinute as default)");
    }

    private static void ValidateClientSideOptions(ClientSideOptions options, List<string> errors, List<string> warnings)
    {
        if (!options.Enabled)
            return; // Skip validation if disabled

        // Token lifetime validation
        if (options.TokenLifetimeSeconds < 30)
            errors.Add($"ClientSide.TokenLifetimeSeconds must be >= 30 seconds, got {options.TokenLifetimeSeconds}");

        if (options.TokenLifetimeSeconds > 86400)
            warnings.Add($"ClientSide.TokenLifetimeSeconds is very long ({options.TokenLifetimeSeconds}s), tokens may be reused inappropriately");

        // Fingerprint cache validation
        if (options.FingerprintCacheDurationSeconds < 0)
            errors.Add($"ClientSide.FingerprintCacheDurationSeconds cannot be negative, got {options.FingerprintCacheDurationSeconds}");

        // Collection timeout validation
        if (options.CollectionTimeoutMs < 100)
            errors.Add($"ClientSide.CollectionTimeoutMs must be >= 100ms, got {options.CollectionTimeoutMs}");

        if (options.CollectionTimeoutMs > 30000)
            warnings.Add($"ClientSide.CollectionTimeoutMs is very long ({options.CollectionTimeoutMs}ms), may affect user experience");

        // Integrity score validation
        if (options.MinIntegrityScore < 0 || options.MinIntegrityScore > 100)
            errors.Add($"ClientSide.MinIntegrityScore must be between 0 and 100, got {options.MinIntegrityScore}");

        // Headless threshold validation
        if (options.HeadlessThreshold < 0.0 || options.HeadlessThreshold > 1.0)
            errors.Add($"ClientSide.HeadlessThreshold must be between 0.0 and 1.0, got {options.HeadlessThreshold}");

        // Warn if no collection methods enabled
        if (!options.CollectWebGL && !options.CollectCanvas && !options.CollectAudio)
            warnings.Add("ClientSide detection is enabled but all collection methods (WebGL, Canvas, Audio) are disabled");

        // Warn about production secret
        if (options.TokenSecret == "demo-secret-key-change-in-production" ||
            options.TokenSecret == "your-secret-key" ||
            options.TokenSecret?.Length < 16)
            warnings.Add("ClientSide.TokenSecret should be a strong, unique secret in production (at least 16 characters)");
    }
}
