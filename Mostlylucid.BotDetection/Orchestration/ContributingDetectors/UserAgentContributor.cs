using System.Collections.Immutable;
using System.Diagnostics;
using System.Text.RegularExpressions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Mostlylucid.BotDetection.Data;
using Mostlylucid.BotDetection.Models;

namespace Mostlylucid.BotDetection.Orchestration.ContributingDetectors;

/// <summary>
///     User-Agent based bot detection.
///     Runs in the first wave (no dependencies).
///     Emits signals for other detectors to consume.
/// </summary>
public class UserAgentContributor : ContributingDetectorBase
{
    private readonly ILogger<UserAgentContributor> _logger;
    private readonly BotDetectionOptions _options;
    private readonly ICompiledPatternCache? _patternCache;

    public UserAgentContributor(
        ILogger<UserAgentContributor> logger,
        IOptions<BotDetectionOptions> options,
        ICompiledPatternCache? patternCache = null)
    {
        _logger = logger;
        _options = options.Value;
        _patternCache = patternCache;
    }

    public override string Name => "UserAgent";
    public override int Priority => 10; // Run early

    // No triggers - runs in first wave
    public override IReadOnlyList<TriggerCondition> TriggerConditions => Array.Empty<TriggerCondition>();

    public override Task<IReadOnlyList<DetectionContribution>> ContributeAsync(
        BlackboardState state,
        CancellationToken cancellationToken = default)
    {
        var userAgent = state.UserAgent;

        if (string.IsNullOrWhiteSpace(userAgent))
        {
            return Task.FromResult(Single(DetectionContribution.Bot(
                Name, "UserAgent", 0.8,
                "Missing User-Agent header",
                BotType.Unknown)));
        }

        var contributions = new List<DetectionContribution>();

        // Check for known good bots (whitelisted)
        var (isWhitelisted, whitelistName) = CheckWhitelist(userAgent);
        if (isWhitelisted)
        {
            return Task.FromResult(Single(DetectionContribution.VerifiedGoodBot(
                Name,
                whitelistName!,
                $"Whitelisted bot pattern: {whitelistName}")
                with
                {
                    Signals = ImmutableDictionary<string, object>.Empty
                        .Add(SignalKeys.UserAgent, userAgent)
                        .Add(SignalKeys.UserAgentIsBot, true)
                        .Add(SignalKeys.UserAgentBotType, BotType.SearchEngine.ToString())
                        .Add(SignalKeys.UserAgentBotName, whitelistName!)
                }));
        }

        // Check for known bot patterns
        var (isBot, confidence, botType, botName, reason) = AnalyzeUserAgent(userAgent);

        if (isBot)
        {
            contributions.Add(DetectionContribution.Bot(
                    Name, "UserAgent", confidence,
                    reason,
                    botType, botName)
                with
                {
                    Signals = ImmutableDictionary<string, object>.Empty
                        .Add(SignalKeys.UserAgent, userAgent)
                        .Add(SignalKeys.UserAgentIsBot, true)
                        .Add(SignalKeys.UserAgentBotType, botType?.ToString() ?? "Unknown")
                        .Add(SignalKeys.UserAgentBotName, botName ?? "")
                });
        }
        else
        {
            // Emit negative contribution (human-like) with signals for other detectors
            contributions.Add(new DetectionContribution
            {
                DetectorName = Name,
                Category = "UserAgent",
                ConfidenceDelta = -0.2, // Negative = evidence of human
                Weight = 1.0,
                Reason = "User-Agent appears normal",
                Signals = ImmutableDictionary<string, object>.Empty
                    .Add(SignalKeys.UserAgent, userAgent)
                    .Add(SignalKeys.UserAgentIsBot, false)
            });
        }

        return Task.FromResult<IReadOnlyList<DetectionContribution>>(contributions);
    }

    private (bool isWhitelisted, string? name) CheckWhitelist(string userAgent)
    {
        foreach (var pattern in _options.WhitelistedBotPatterns)
        {
            if (userAgent.Contains(pattern, StringComparison.OrdinalIgnoreCase))
            {
                return (true, pattern);
            }
        }
        return (false, null);
    }

    private (bool isBot, double confidence, BotType? type, string? name, string reason)
        AnalyzeUserAgent(string userAgent)
    {
        // Check compiled patterns from data sources
        if (_patternCache != null)
        {
            if (_patternCache.MatchesAnyPattern(userAgent, out var matchedPattern))
            {
                return (true, 0.9, BotType.Unknown, null,
                    $"Matched pattern: {matchedPattern}");
            }
        }

        // Check common bot indicators
        if (IsCommonBotPattern(userAgent, out var botType, out var botName))
        {
            return (true, 0.9, botType, botName, $"Known bot pattern: {botName}");
        }

        // Check for suspicious patterns
        if (IsSuspiciousUserAgent(userAgent, out var suspiciousReason))
        {
            return (true, 0.6, BotType.Unknown, null, suspiciousReason);
        }

        return (false, 0.0, null, null, "Normal user agent");
    }

    private static bool IsCommonBotPattern(string userAgent, out BotType? botType, out string? botName)
    {
        // Common bot patterns with high confidence
        // Using Scraper for automation tools since BotType.Automation doesn't exist
        var patterns = new (string pattern, BotType type, string name)[]
        {
            ("curl/", BotType.Scraper, "curl"),
            ("wget/", BotType.Scraper, "wget"),
            ("python-requests", BotType.Scraper, "python-requests"),
            ("python-urllib", BotType.Scraper, "python-urllib"),
            ("scrapy", BotType.Scraper, "Scrapy"),
            ("selenium", BotType.Scraper, "Selenium"),
            ("headless", BotType.Scraper, "Headless browser"),
            ("phantomjs", BotType.Scraper, "PhantomJS"),
            ("puppeteer", BotType.Scraper, "Puppeteer"),
            ("playwright", BotType.Scraper, "Playwright"),
            ("httrack", BotType.Scraper, "HTTrack"),
            ("libwww-perl", BotType.Scraper, "libwww-perl"),
            ("java/", BotType.Scraper, "Java HTTP client"),
            ("apache-httpclient", BotType.Scraper, "Apache HttpClient"),
            ("okhttp", BotType.Scraper, "OkHttp"),
            ("go-http-client", BotType.Scraper, "Go HTTP client"),
            ("node-fetch", BotType.Scraper, "node-fetch"),
            ("axios/", BotType.Scraper, "axios"),
        };

        foreach (var (pattern, type, name) in patterns)
        {
            if (userAgent.Contains(pattern, StringComparison.OrdinalIgnoreCase))
            {
                botType = type;
                botName = name;
                return true;
            }
        }

        botType = null;
        botName = null;
        return false;
    }

    private static bool IsSuspiciousUserAgent(string userAgent, out string reason)
    {
        // Very short user agent
        if (userAgent.Length < 20)
        {
            reason = "Suspiciously short User-Agent";
            return true;
        }

        // Contains "bot" or "crawler" but not whitelisted
        if (Regex.IsMatch(userAgent, @"\b(bot|crawler|spider|scraper)\b", RegexOptions.IgnoreCase))
        {
            reason = "Contains bot/crawler keyword";
            return true;
        }

        // Empty version numbers (common in simple bots)
        if (Regex.IsMatch(userAgent, @"Mozilla/\d+\.\d+\s*$"))
        {
            reason = "Bare Mozilla version without details";
            return true;
        }

        reason = string.Empty;
        return false;
    }
}

/// <summary>
///     Inconsistency detection that runs after raw signals are collected.
///     Looks for mismatches between claimed identity and actual behavior.
/// </summary>
public class InconsistencyContributor : ContributingDetectorBase
{
    private readonly ILogger<InconsistencyContributor> _logger;

    public InconsistencyContributor(ILogger<InconsistencyContributor> logger)
    {
        _logger = logger;
    }

    public override string Name => "Inconsistency";
    public override int Priority => 50; // Run after raw signal detectors

    // Wait for UA and IP signals
    public override IReadOnlyList<TriggerCondition> TriggerConditions =>
    [
        Triggers.WhenSignalExists(SignalKeys.UserAgent),
    ];

    public override Task<IReadOnlyList<DetectionContribution>> ContributeAsync(
        BlackboardState state,
        CancellationToken cancellationToken = default)
    {
        var contributions = new List<DetectionContribution>();

        var userAgent = state.GetSignal<string>(SignalKeys.UserAgent) ?? "";
        var isDatacenter = state.GetSignal<bool>(SignalKeys.IpIsDatacenter);
        var headers = state.HttpContext.Request.Headers;

        // Check for datacenter IP + browser UA (common bot pattern)
        if (isDatacenter && LooksLikeBrowser(userAgent))
        {
            contributions.Add(DetectionContribution.Bot(
                Name, "Inconsistency", 0.7,
                "Browser User-Agent from datacenter IP",
                BotType.Unknown,
                weight: 1.5)); // High weight for this signal
        }

        // Check for missing Accept-Language with browser UA
        if (LooksLikeBrowser(userAgent) && !headers.ContainsKey("Accept-Language"))
        {
            contributions.Add(DetectionContribution.Bot(
                Name, "Inconsistency", 0.5,
                "Browser User-Agent without Accept-Language header",
                BotType.Unknown));
        }

        // Check for Chrome UA without sec-ch-ua headers
        if (userAgent.Contains("Chrome/") && !headers.ContainsKey("sec-ch-ua"))
        {
            contributions.Add(DetectionContribution.Bot(
                Name, "Inconsistency", 0.4,
                "Chrome User-Agent without Client Hints",
                BotType.Scraper)); // Likely automation/scraper
        }

        // Check for modern browser claiming old version
        if (IsOutdatedBrowser(userAgent))
        {
            contributions.Add(DetectionContribution.Bot(
                Name, "Inconsistency", 0.3,
                "Outdated browser version in User-Agent",
                BotType.Unknown));
        }

        if (contributions.Count == 0)
        {
            // No inconsistencies found - add negative signal (human indicator)
            contributions.Add(new DetectionContribution
            {
                DetectorName = Name,
                Category = "Inconsistency",
                ConfidenceDelta = -0.05,
                Weight = 0.8,
                Reason = "No header/UA inconsistencies detected"
            });
        }

        return Task.FromResult<IReadOnlyList<DetectionContribution>>(contributions);
    }

    private static bool LooksLikeBrowser(string userAgent) =>
        userAgent.Contains("Mozilla/") &&
        (userAgent.Contains("Chrome") || userAgent.Contains("Firefox") ||
         userAgent.Contains("Safari") || userAgent.Contains("Edge"));

    private static bool IsOutdatedBrowser(string userAgent)
    {
        // Check for very old Chrome versions
        var chromeMatch = Regex.Match(userAgent, @"Chrome/(\d+)");
        if (chromeMatch.Success && int.TryParse(chromeMatch.Groups[1].Value, out var version))
        {
            // Chrome versions below 90 are considered very outdated
            return version < 90;
        }

        return false;
    }
}

/// <summary>
///     Expensive AI-based detector that only runs when risk is elevated.
///     Uses trigger conditions to avoid running on obvious humans.
/// </summary>
public class AiContributor : ContributingDetectorBase
{
    private readonly ILogger<AiContributor> _logger;

    public AiContributor(ILogger<AiContributor> logger)
    {
        _logger = logger;
    }

    public override string Name => "AI";
    public override int Priority => 100; // Run last
    public override TimeSpan ExecutionTimeout => TimeSpan.FromSeconds(5); // Longer timeout for AI

    // Only run when risk is medium or higher AND we have signals to analyze
    public override IReadOnlyList<TriggerCondition> TriggerConditions =>
    [
        Triggers.AllOf(
            Triggers.WhenRiskMediumOrHigher,
            Triggers.WhenDetectorCount(2) // At least 2 other detectors ran
        )
    ];

    public override async Task<IReadOnlyList<DetectionContribution>> ContributeAsync(
        BlackboardState state,
        CancellationToken cancellationToken = default)
    {
        _logger.LogDebug("AI detector running for request {RequestId}", state.RequestId);

        // In a real implementation, this would call ONNX or LLM
        // For now, return a placeholder that demonstrates the pattern
        await Task.Delay(10, cancellationToken); // Simulate some processing

        // Example: AI confirms or adjusts the existing risk assessment
        var currentRisk = state.CurrentRiskScore;

        if (currentRisk > 0.8)
        {
            return Single(DetectionContribution.Bot(
                Name, "AI", 0.2, // Small adjustment
                "AI analysis confirms high-risk signals",
                weight: 0.5));
        }

        if (currentRisk > 0.5)
        {
            // Uncertain - AI provides additional signal
            return Single(DetectionContribution.Info(
                Name, "AI",
                "AI analysis: borderline case, monitoring"));
        }

        return None();
    }
}
