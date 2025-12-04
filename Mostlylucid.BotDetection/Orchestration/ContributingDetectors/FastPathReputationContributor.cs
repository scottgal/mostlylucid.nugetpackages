using System.Collections.Immutable;
using System.Security.Cryptography;
using System.Text;
using Microsoft.Extensions.Logging;
using Mostlylucid.BotDetection.Data;
using Mostlylucid.BotDetection.Models;

namespace Mostlylucid.BotDetection.Orchestration.ContributingDetectors;

/// <summary>
///     Ultra-fast Wave 0 contributor that checks for CONFIRMED BAD patterns only.
///     Runs FIRST (no dependencies) to enable instant abort for known malicious actors.
///
///     This is the "12 basic shapes" fast-path check:
///     - Only checks patterns that are ConfirmedBad or ManuallyBlocked
///     - Skips Neutral/Suspect patterns (those are handled by ReputationBiasContributor later)
///     - Uses raw UA/IP directly without waiting for signal extraction
///     - Enables circuit-breaker style early exit before expensive analysis
///
///     Works in tandem with ReputationBiasContributor:
///     - FastPathReputationContributor (Priority 3) - Instant abort for known bad
///     - ReputationBiasContributor (Priority 45) - Bias for scoring after signals extracted
/// </summary>
public class FastPathReputationContributor : ContributingDetectorBase
{
    private readonly ILogger<FastPathReputationContributor> _logger;
    private readonly IPatternReputationCache _reputationCache;

    public FastPathReputationContributor(
        ILogger<FastPathReputationContributor> logger,
        IPatternReputationCache reputationCache)
    {
        _logger = logger;
        _reputationCache = reputationCache;
    }

    public override string Name => "FastPathReputation";
    public override int Priority => 3; // Run FIRST - before any signal extraction

    // No triggers - runs immediately in Wave 0
    public override IReadOnlyList<TriggerCondition> TriggerConditions => Array.Empty<TriggerCondition>();

    public override Task<IReadOnlyList<DetectionContribution>> ContributeAsync(
        BlackboardState state,
        CancellationToken cancellationToken = default)
    {
        // Fast path: check raw UA and IP against known bad patterns
        // Only ConfirmedBad and ManuallyBlocked trigger instant abort

        PatternReputation? matchedPattern = null;
        string matchType = "";

        // Check raw User-Agent first (most common fast-path hit)
        if (!string.IsNullOrWhiteSpace(state.UserAgent))
        {
            var uaPatternId = CreateUaPatternId(state.UserAgent);
            var uaReputation = _reputationCache.Get(uaPatternId);

            if (uaReputation?.CanTriggerFastAbort == true)
            {
                matchedPattern = uaReputation;
                matchType = "UserAgent";
            }
        }

        // Check raw IP if UA didn't match
        if (matchedPattern == null && !string.IsNullOrWhiteSpace(state.ClientIp))
        {
            var ipPatternId = CreateIpPatternId(state.ClientIp);
            var ipReputation = _reputationCache.Get(ipPatternId);

            if (ipReputation?.CanTriggerFastAbort == true)
            {
                matchedPattern = ipReputation;
                matchType = "IP";
            }
        }

        // No fast-path match - let other detectors handle it
        if (matchedPattern == null)
        {
            return Task.FromResult<IReadOnlyList<DetectionContribution>>(Array.Empty<DetectionContribution>());
        }

        // FAST PATH HIT - create instant abort contribution
        _logger.LogWarning(
            "Fast-path reputation abort: {PatternId} ({MatchType}) state={State} score={Score:F2} support={Support:F0}",
            matchedPattern.PatternId, matchType, matchedPattern.State, matchedPattern.BotScore, matchedPattern.Support);

        var signals = ImmutableDictionary<string, object>.Empty
            .Add(SignalKeys.ReputationFastPathHit, true)
            .Add(SignalKeys.ReputationCanAbort, true)
            .Add($"reputation.fastpath.{matchType.ToLowerInvariant()}.pattern_id", matchedPattern.PatternId)
            .Add($"reputation.fastpath.{matchType.ToLowerInvariant()}.state", matchedPattern.State.ToString())
            .Add($"reputation.fastpath.{matchType.ToLowerInvariant()}.score", matchedPattern.BotScore)
            .Add($"reputation.fastpath.{matchType.ToLowerInvariant()}.support", matchedPattern.Support);

        var contribution = DetectionContribution.VerifiedBadBot(
            Name,
            matchedPattern.PatternId,
            $"[FastPath] Known bad {matchType}: {matchedPattern.State} (score={matchedPattern.BotScore:F2}, support={matchedPattern.Support:F0})",
            BotType.MaliciousBot) with
        {
            ConfidenceDelta = matchedPattern.BotScore,
            Weight = 3.0, // Very high weight for confirmed patterns - instant abort
            Signals = signals
        };

        return Task.FromResult<IReadOnlyList<DetectionContribution>>(new[] { contribution });
    }

    /// <summary>
    ///     Create pattern ID for User-Agent using simple normalization.
    ///     Fast path uses simplified normalization for speed.
    /// </summary>
    private static string CreateUaPatternId(string userAgent)
    {
        // Simple normalization for fast path matching
        var normalized = NormalizeForFastPath(userAgent);
        var hash = ComputeHash(normalized);
        return $"ua:{hash}";
    }

    /// <summary>
    ///     Create pattern ID for IP (CIDR range).
    /// </summary>
    private static string CreateIpPatternId(string ip)
    {
        var normalized = NormalizeIpToRange(ip);
        return $"ip:{normalized}";
    }

    /// <summary>
    ///     Simple normalization for fast-path matching.
    ///     Extracts key indicators without expensive regex.
    /// </summary>
    private static string NormalizeForFastPath(string ua)
    {
        if (string.IsNullOrWhiteSpace(ua))
            return "empty";

        var lower = ua.ToLowerInvariant().Trim();
        var indicators = new List<string>(12); // Pre-sized for "12 basic shapes"

        // Browser detection (mutually exclusive)
        if (lower.Contains("chrome")) indicators.Add("chrome");
        else if (lower.Contains("firefox")) indicators.Add("firefox");
        else if (lower.Contains("safari")) indicators.Add("safari");
        else if (lower.Contains("edge")) indicators.Add("edge");

        // OS detection (mutually exclusive)
        if (lower.Contains("windows")) indicators.Add("windows");
        else if (lower.Contains("mac")) indicators.Add("macos");
        else if (lower.Contains("linux")) indicators.Add("linux");
        else if (lower.Contains("android")) indicators.Add("android");
        else if (lower.Contains("iphone") || lower.Contains("ipad")) indicators.Add("ios");

        // Bot indicators (can be multiple)
        if (lower.Contains("bot")) indicators.Add("bot");
        if (lower.Contains("crawler")) indicators.Add("crawler");
        if (lower.Contains("spider")) indicators.Add("spider");
        if (lower.Contains("scraper")) indicators.Add("scraper");
        if (lower.Contains("headless")) indicators.Add("headless");
        if (lower.Contains("python")) indicators.Add("python");
        if (lower.Contains("curl")) indicators.Add("curl");
        if (lower.Contains("wget")) indicators.Add("wget");

        // Length bucket
        var lengthBucket = ua.Length switch
        {
            < 20 => "tiny",
            < 50 => "short",
            < 150 => "normal",
            < 300 => "long",
            _ => "huge"
        };
        indicators.Add($"len:{lengthBucket}");

        return string.Join(",", indicators.OrderBy(x => x));
    }

    /// <summary>
    ///     Normalize IP to CIDR range for pattern matching.
    /// </summary>
    private static string NormalizeIpToRange(string ip)
    {
        if (string.IsNullOrWhiteSpace(ip))
            return "unknown";

        // Handle IPv6
        if (ip.Contains(':'))
        {
            var parts = ip.Split(':');
            if (parts.Length >= 3)
            {
                return $"{parts[0]}:{parts[1]}:{parts[2]}::/48";
            }
            return ip;
        }

        // Handle IPv4 - normalize to /24
        var octets = ip.Split('.');
        if (octets.Length == 4)
        {
            return $"{octets[0]}.{octets[1]}.{octets[2]}.0/24";
        }

        return ip;
    }

    /// <summary>
    ///     Compute SHA256 hash of input, return first 16 chars.
    /// </summary>
    private static string ComputeHash(string input)
    {
        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(input));
        return Convert.ToHexString(bytes)[..16].ToLowerInvariant();
    }
}
