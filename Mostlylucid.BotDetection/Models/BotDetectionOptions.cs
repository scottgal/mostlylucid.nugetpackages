namespace Mostlylucid.BotDetection.Models;

/// <summary>
///     Configuration options for bot detection
/// </summary>
public class BotDetectionOptions
{
    /// <summary>
    ///     Confidence threshold above which a request is classified as a bot (default: 0.7)
    /// </summary>
    public double BotThreshold { get; set; } = 0.7;

    /// <summary>
    ///     Enable test mode (allows ml-bot-test-mode header to override detection)
    ///     Only enable in development/testing environments!
    /// </summary>
    public bool EnableTestMode { get; set; } = false;

    /// <summary>
    ///     Enable user-agent based detection
    /// </summary>
    public bool EnableUserAgentDetection { get; set; } = true;

    /// <summary>
    ///     Enable header analysis
    /// </summary>
    public bool EnableHeaderAnalysis { get; set; } = true;

    /// <summary>
    ///     Enable IP-based detection
    /// </summary>
    public bool EnableIpDetection { get; set; } = true;

    /// <summary>
    ///     Enable behavioral analysis
    /// </summary>
    public bool EnableBehavioralAnalysis { get; set; } = true;

    /// <summary>
    ///     Enable LLM-based detection (requires Ollama)
    /// </summary>
    public bool EnableLlmDetection { get; set; } = false;

    /// <summary>
    ///     Ollama API endpoint (e.g., http://localhost:11434)
    /// </summary>
    public string? OllamaEndpoint { get; set; } = "http://localhost:11434";

    /// <summary>
    ///     Ollama model to use for bot detection (e.g., "qwen2.5:1.5b")
    /// </summary>
    public string? OllamaModel { get; set; } = "qwen2.5:1.5b";

    /// <summary>
    ///     Timeout for LLM detection in milliseconds
    /// </summary>
    public int LlmTimeoutMs { get; set; } = 2000;

    /// <summary>
    ///     Maximum requests per IP per minute (for rate limiting)
    /// </summary>
    public int MaxRequestsPerMinute { get; set; } = 60;

    /// <summary>
    ///     Cache duration for detection results in seconds
    /// </summary>
    public int CacheDurationSeconds { get; set; } = 300;

    /// <summary>
    ///     Known good bot patterns (won't be flagged even if other signals present)
    /// </summary>
    public List<string> WhitelistedBotPatterns { get; set; } = new()
    {
        "Googlebot", "Bingbot", "Slackbot", "DuckDuckBot", "Baiduspider",
        "YandexBot", "Sogou", "Exabot", "facebot", "ia_archiver"
    };

    /// <summary>
    ///     Known datacenter/hosting IP ranges (increases suspicion)
    /// </summary>
    public List<string> DatacenterIpPrefixes { get; set; } = new()
    {
        "3.0.0.0/8", "13.0.0.0/8", "18.0.0.0/8", "52.0.0.0/8", // AWS
        "20.0.0.0/8", "40.0.0.0/8", "104.0.0.0/8", // Azure
        "34.0.0.0/8", "35.0.0.0/8", // GCP
        "138.0.0.0/8", "139.0.0.0/8", "140.0.0.0/8" // Oracle Cloud
    };
}