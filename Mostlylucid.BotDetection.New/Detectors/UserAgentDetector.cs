using System.Text.RegularExpressions;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Mostlylucid.BotDetection.Data;
using Mostlylucid.BotDetection.Models;

namespace Mostlylucid.BotDetection.Detectors;

/// <summary>
///     Detects bots based on User-Agent analysis
/// </summary>
public class UserAgentDetector(
    ILogger<UserAgentDetector> logger,
    IOptions<BotDetectionOptions> options)
    : IDetector
{
    private readonly ILogger<UserAgentDetector> _logger = logger;
    private readonly BotDetectionOptions _options = options.Value;

    public string Name => "User-Agent Detector";

    public Task<DetectorResult> DetectAsync(HttpContext context, CancellationToken cancellationToken = default)
    {
        var result = new DetectorResult();
        var userAgent = context.Request.Headers.UserAgent.ToString();

        if (string.IsNullOrWhiteSpace(userAgent))
        {
            result.Confidence = 0.8;
            result.Reasons.Add(new DetectionReason
            {
                Category = "User-Agent",
                Detail = "Missing User-Agent header",
                ConfidenceImpact = 0.8
            });
            return Task.FromResult(result);
        }

        // Check for known good bots (whitelisted)
        foreach (var (pattern, name) in BotSignatures.GoodBots)
            if (userAgent.Contains(pattern, StringComparison.OrdinalIgnoreCase))
                if (_options.WhitelistedBotPatterns.Any(wp =>
                        userAgent.Contains(wp, StringComparison.OrdinalIgnoreCase)))
                {
                    result.Confidence = 0.0; // Known good bot
                    result.BotType = BotType.VerifiedBot;
                    result.BotName = name;
                    result.Reasons.Add(new DetectionReason
                    {
                        Category = "User-Agent",
                        Detail = $"Known verified bot: {name}",
                        ConfidenceImpact = -1.0 // Negative impact = reduces bot score
                    });
                    return Task.FromResult(result);
                }

        var confidence = 0.0;
        var reasons = new List<DetectionReason>();

        // Check for malicious bot patterns
        foreach (var pattern in BotSignatures.MaliciousBotPatterns)
            if (userAgent.Contains(pattern, StringComparison.OrdinalIgnoreCase))
            {
                confidence += 0.3;
                reasons.Add(new DetectionReason
                {
                    Category = "User-Agent",
                    Detail = $"Suspicious pattern detected: {pattern}",
                    ConfidenceImpact = 0.3
                });
            }

        // Check for automation frameworks
        foreach (var framework in BotSignatures.AutomationFrameworks)
            if (userAgent.Contains(framework, StringComparison.OrdinalIgnoreCase))
            {
                confidence += 0.5;
                reasons.Add(new DetectionReason
                {
                    Category = "User-Agent",
                    Detail = $"Automation framework detected: {framework}",
                    ConfidenceImpact = 0.5
                });
                result.BotType = BotType.Scraper;
            }

        // Check regex patterns
        foreach (var pattern in BotSignatures.BotPatterns)
            try
            {
                if (Regex.IsMatch(userAgent, pattern, RegexOptions.IgnoreCase, TimeSpan.FromMilliseconds(100)))
                {
                    confidence += 0.2;
                    reasons.Add(new DetectionReason
                    {
                        Category = "User-Agent",
                        Detail = $"Bot pattern matched: {pattern}",
                        ConfidenceImpact = 0.2
                    });
                }
            }
            catch (RegexMatchTimeoutException)
            {
                _logger.LogWarning("Regex timeout for pattern: {Pattern}", pattern);
            }

        // Suspiciously short or simple user agents
        if (userAgent.Length < 20)
        {
            confidence += 0.4;
            reasons.Add(new DetectionReason
            {
                Category = "User-Agent",
                Detail = $"Suspiciously short User-Agent ({userAgent.Length} chars)",
                ConfidenceImpact = 0.4
            });
        }

        // Contains URL in User-Agent (common in bots)
        if (userAgent.Contains("http://", StringComparison.OrdinalIgnoreCase) ||
            userAgent.Contains("https://", StringComparison.OrdinalIgnoreCase))
        {
            confidence += 0.3;
            reasons.Add(new DetectionReason
            {
                Category = "User-Agent",
                Detail = "User-Agent contains URL",
                ConfidenceImpact = 0.3
            });
        }

        result.Confidence = Math.Min(confidence, 1.0);
        result.Reasons = reasons;

        if (result.Confidence > 0.5 && result.BotType == null) result.BotType = BotType.Scraper;

        return Task.FromResult(result);
    }
}