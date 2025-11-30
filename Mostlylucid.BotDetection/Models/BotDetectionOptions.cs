using Microsoft.Extensions.Options;

namespace Mostlylucid.BotDetection.Models;

/// <summary>
///     Configuration options for bot detection.
///     Supports a range of detection strategies from simple (static patterns) to advanced (LLM).
/// </summary>
public class BotDetectionOptions
{
    /// <summary>
    ///     Confidence threshold above which a request is classified as a bot (default: 0.7)
    ///     Valid range: 0.0 to 1.0
    /// </summary>
    public double BotThreshold { get; set; } = 0.7;

    /// <summary>
    ///     Enable test mode (allows ml-bot-test-mode header to override detection)
    ///     WARNING: Only enable in development/testing environments!
    /// </summary>
    public bool EnableTestMode { get; set; }

    /// <summary>
    ///     Enable user-agent based detection (static pattern matching - fastest)
    /// </summary>
    public bool EnableUserAgentDetection { get; set; } = true;

    /// <summary>
    ///     Enable header analysis (examines Accept, Accept-Language, etc.)
    /// </summary>
    public bool EnableHeaderAnalysis { get; set; } = true;

    /// <summary>
    ///     Enable IP-based detection (checks against datacenter IP ranges)
    /// </summary>
    public bool EnableIpDetection { get; set; } = true;

    /// <summary>
    ///     Enable behavioral analysis (rate limiting, request patterns)
    /// </summary>
    public bool EnableBehavioralAnalysis { get; set; } = true;

    /// <summary>
    ///     Enable LLM-based detection (requires Ollama - most advanced)
    /// </summary>
    public bool EnableLlmDetection { get; set; }

    /// <summary>
    ///     Ollama API endpoint (e.g., http://localhost:11434)
    /// </summary>
    public string? OllamaEndpoint { get; set; } = "http://localhost:11434";

    /// <summary>
    ///     Ollama model to use for bot detection (e.g., "qwen2.5:1.5b")
    /// </summary>
    public string? OllamaModel { get; set; } = "qwen2.5:1.5b";

    /// <summary>
    ///     Timeout for LLM detection in milliseconds (default: 2000ms)
    ///     Valid range: 100 to 30000
    /// </summary>
    public int LlmTimeoutMs { get; set; } = 2000;

    /// <summary>
    ///     Maximum requests per IP per minute for behavioral analysis (default: 60)
    ///     Valid range: 1 to 10000
    /// </summary>
    public int MaxRequestsPerMinute { get; set; } = 60;

    /// <summary>
    ///     Cache duration for detection results in seconds (default: 300)
    ///     Valid range: 0 to 86400 (24 hours)
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
    ///     Known datacenter/hosting IP ranges (CIDR notation, increases suspicion)
    /// </summary>
    public List<string> DatacenterIpPrefixes { get; set; } = new()
    {
        "3.0.0.0/8", "13.0.0.0/8", "18.0.0.0/8", "52.0.0.0/8", // AWS
        "20.0.0.0/8", "40.0.0.0/8", "104.0.0.0/8", // Azure
        "34.0.0.0/8", "35.0.0.0/8", // GCP
        "138.0.0.0/8", "139.0.0.0/8", "140.0.0.0/8" // Oracle Cloud
    };
}

/// <summary>
///     Validates BotDetectionOptions on startup
/// </summary>
public class BotDetectionOptionsValidator : IValidateOptions<BotDetectionOptions>
{
    public ValidateOptionsResult Validate(string? name, BotDetectionOptions options)
    {
        var errors = new List<string>();

        if (options.BotThreshold < 0.0 || options.BotThreshold > 1.0)
            errors.Add($"BotThreshold must be between 0.0 and 1.0, got {options.BotThreshold}");

        if (options.LlmTimeoutMs < 100 || options.LlmTimeoutMs > 30000)
            errors.Add($"LlmTimeoutMs must be between 100 and 30000, got {options.LlmTimeoutMs}");

        if (options.MaxRequestsPerMinute < 1 || options.MaxRequestsPerMinute > 10000)
            errors.Add($"MaxRequestsPerMinute must be between 1 and 10000, got {options.MaxRequestsPerMinute}");

        if (options.CacheDurationSeconds < 0 || options.CacheDurationSeconds > 86400)
            errors.Add($"CacheDurationSeconds must be between 0 and 86400, got {options.CacheDurationSeconds}");

        if (options.EnableLlmDetection && string.IsNullOrWhiteSpace(options.OllamaEndpoint))
            errors.Add("OllamaEndpoint must be specified when LLM detection is enabled");

        if (options.EnableLlmDetection && string.IsNullOrWhiteSpace(options.OllamaModel))
            errors.Add("OllamaModel must be specified when LLM detection is enabled");

        // Validate CIDR patterns
        foreach (var prefix in options.DatacenterIpPrefixes)
        {
            if (!IsValidCidr(prefix))
                errors.Add($"Invalid CIDR notation in DatacenterIpPrefixes: {prefix}");
        }

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
}
