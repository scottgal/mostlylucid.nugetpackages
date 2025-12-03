using System.Collections.Immutable;
using Microsoft.Extensions.Logging;

namespace Mostlylucid.BotDetection.Orchestration.ContributingDetectors;

/// <summary>
///     HTTP Header analysis for bot detection.
///     Runs in the first wave (no dependencies).
///     Analyzes request headers for bot indicators.
/// </summary>
public class HeaderContributor : ContributingDetectorBase
{
    private readonly ILogger<HeaderContributor> _logger;

    public HeaderContributor(ILogger<HeaderContributor> logger)
    {
        _logger = logger;
    }

    public override string Name => "Header";
    public override int Priority => 15; // Run early, after UserAgent

    // No triggers - runs in first wave
    public override IReadOnlyList<TriggerCondition> TriggerConditions => Array.Empty<TriggerCondition>();

    public override Task<IReadOnlyList<DetectionContribution>> ContributeAsync(
        BlackboardState state,
        CancellationToken cancellationToken = default)
    {
        var contributions = new List<DetectionContribution>();
        var headers = state.HttpContext.Request.Headers;
        var signals = ImmutableDictionary.CreateBuilder<string, object>();

        // Check for missing essential headers
        var hasAcceptLanguage = headers.ContainsKey("Accept-Language");
        var hasAccept = headers.ContainsKey("Accept");
        var hasConnection = headers.ContainsKey("Connection");
        var hasAcceptEncoding = headers.ContainsKey("Accept-Encoding");

        signals.Add("header.has_accept_language", hasAcceptLanguage);
        signals.Add("header.has_accept", hasAccept);
        signals.Add("header.has_accept_encoding", hasAcceptEncoding);
        signals.Add("header.count", headers.Count);

        // Missing Accept header - suspicious
        if (!hasAccept)
        {
            contributions.Add(DetectionContribution.Bot(
                Name, "Header", 0.4,
                "Missing Accept header",
                Models.BotType.Unknown)
                with { Signals = signals.ToImmutable() });
        }

        // Missing Accept-Language with browser UA
        var userAgent = state.UserAgent ?? "";
        var looksLikeBrowser = userAgent.Contains("Mozilla/") &&
                               (userAgent.Contains("Chrome") || userAgent.Contains("Firefox") ||
                                userAgent.Contains("Safari") || userAgent.Contains("Edge"));

        if (looksLikeBrowser && !hasAcceptLanguage)
        {
            contributions.Add(DetectionContribution.Bot(
                Name, "Header", 0.5,
                "Browser User-Agent without Accept-Language",
                Models.BotType.Scraper));
        }

        // Check for proxy headers (X-Forwarded-For, Via)
        var hasXForwardedFor = headers.ContainsKey("X-Forwarded-For");
        var hasVia = headers.ContainsKey("Via");
        signals.Add("header.has_proxy_headers", hasXForwardedFor || hasVia);

        // Check for unusual header ordering or content
        var headerCount = headers.Count;
        if (headerCount < 3)
        {
            contributions.Add(DetectionContribution.Bot(
                Name, "Header", 0.6,
                $"Very few headers ({headerCount})",
                Models.BotType.Scraper));
        }

        // Check for bot-specific headers
        if (headers.ContainsKey("X-Requested-With") &&
            headers["X-Requested-With"].ToString() == "XMLHttpRequest" &&
            !hasAcceptLanguage)
        {
            contributions.Add(DetectionContribution.Bot(
                Name, "Header", 0.4,
                "AJAX request without Accept-Language",
                Models.BotType.Scraper));
        }

        // No bot indicators found
        if (contributions.Count == 0)
        {
            contributions.Add(new DetectionContribution
            {
                DetectorName = Name,
                Category = "Header",
                ConfidenceDelta = -0.15, // Negative = evidence of human
                Weight = 1.0,
                Reason = "Headers appear normal",
                Signals = signals.ToImmutable()
            });
        }

        return Task.FromResult<IReadOnlyList<DetectionContribution>>(contributions);
    }
}