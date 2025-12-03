using System.Text.Json;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Mostlylucid.BotDetection.Attributes;
using Mostlylucid.BotDetection.Models;
using Mostlylucid.BotDetection.Orchestration;
using Mostlylucid.BotDetection.Policies;

namespace Mostlylucid.BotDetection.Middleware;

/// <summary>
///     Middleware that detects bots and adds detection result to HttpContext.
///     Supports policy-based detection via [BotPolicy] attributes and path patterns.
///     Results are stored in HttpContext.Items for access by downstream middleware, controllers, and views.
/// </summary>
/// <remarks>
///     The following keys are available in HttpContext.Items:
///     <list type="bullet">
///         <item><see cref="BotDetectionResultKey" /> - Full BotDetectionResult object</item>
///         <item><see cref="AggregatedEvidenceKey" /> - Full AggregatedEvidence from orchestrator</item>
///         <item><see cref="IsBotKey" /> - Boolean indicating if request is from a bot</item>
///         <item><see cref="BotConfidenceKey" /> - Double confidence score (0.0-1.0)</item>
///         <item><see cref="BotTypeKey" /> - BotType enum value (nullable)</item>
///         <item><see cref="BotNameKey" /> - String bot name (nullable)</item>
///         <item><see cref="BotCategoryKey" /> - String category from detection reasons (nullable)</item>
///         <item><see cref="DetectionReasonsKey" /> - List of DetectionReason objects</item>
///         <item><see cref="PolicyNameKey" /> - Name of the policy used for detection</item>
///         <item><see cref="PolicyActionKey" /> - PolicyAction taken (if any)</item>
///     </list>
/// </remarks>
public class BotDetectionMiddleware(
    RequestDelegate next,
    ILogger<BotDetectionMiddleware> logger,
    IOptions<BotDetectionOptions> options)
{
    #region HttpContext Item Keys

    /// <summary>Full BotDetectionResult object</summary>
    public const string BotDetectionResultKey = "BotDetectionResult";

    /// <summary>Full AggregatedEvidence from blackboard orchestrator</summary>
    public const string AggregatedEvidenceKey = "BotDetection.AggregatedEvidence";

    /// <summary>Boolean: true if request is from a bot</summary>
    public const string IsBotKey = "BotDetection.IsBot";

    /// <summary>Double: confidence score (0.0-1.0)</summary>
    public const string BotConfidenceKey = "BotDetection.Confidence";

    /// <summary>BotType?: the detected bot type</summary>
    public const string BotTypeKey = "BotDetection.BotType";

    /// <summary>String?: the detected bot name</summary>
    public const string BotNameKey = "BotDetection.BotName";

    /// <summary>String?: primary detection category (e.g., "UserAgent", "IP", "Header")</summary>
    public const string BotCategoryKey = "BotDetection.Category";

    /// <summary>List&lt;DetectionReason&gt;: all detection reasons</summary>
    public const string DetectionReasonsKey = "BotDetection.Reasons";

    /// <summary>String: name of the policy used for this request</summary>
    public const string PolicyNameKey = "BotDetection.PolicyName";

    /// <summary>PolicyAction?: action taken by policy (if any)</summary>
    public const string PolicyActionKey = "BotDetection.PolicyAction";

    #endregion

    private static readonly Dictionary<string, TestModeConfig> TestModeConfigs =
        new(StringComparer.OrdinalIgnoreCase)
        {
            ["disable"] = new(false, 0.0, null, null, "Bot detection disabled via test header"),
            ["human"] = new(false, 0.0, null, null, "Simulated human traffic"),
            ["bot"] = new(true, 1.0, BotType.Unknown, "Test Bot", "Simulated bot traffic"),
            ["googlebot"] = new(true, 0.95, BotType.SearchEngine, "Googlebot", "Simulated Googlebot"),
            ["bingbot"] = new(true, 0.95, BotType.SearchEngine, "Bingbot", "Simulated Bingbot"),
            ["scraper"] = new(true, 0.9, BotType.Scraper, "Test Scraper", "Simulated scraper bot"),
            ["malicious"] = new(true, 1.0, BotType.MaliciousBot, "Test Malicious Bot", "Simulated malicious bot"),
            ["social"] = new(true, 0.85, BotType.SocialMediaBot, "Test Social Bot", "Simulated social media bot"),
            ["socialbot"] = new(true, 0.85, BotType.SocialMediaBot, "Test Social Bot", "Simulated social media bot"),
            ["monitor"] = new(true, 0.8, BotType.MonitoringBot, "Test Monitoring Bot", "Simulated monitoring bot"),
            ["monitoring"] = new(true, 0.8, BotType.MonitoringBot, "Test Monitoring Bot", "Simulated monitoring bot")
        };

    private static readonly Random Jitter = new();

    private readonly ILogger<BotDetectionMiddleware> _logger = logger;
    private readonly RequestDelegate _next = next;
    private readonly BotDetectionOptions _options = options.Value;

    /// <summary>
    ///     Main middleware entry point. Runs bot detection and handles blocking/throttling.
    ///     Uses the BlackboardOrchestrator for full pipeline detection with policy support.
    /// </summary>
    public async Task InvokeAsync(
        HttpContext context,
        BlackboardOrchestrator orchestrator,
        IPolicyRegistry policyRegistry)
    {
        // Check if bot detection is globally enabled
        if (!_options.Enabled)
        {
            await _next(context);
            return;
        }

        // Check for [SkipBotDetection] attribute
        var endpoint = context.GetEndpoint();
        if (endpoint?.Metadata.GetMetadata<SkipBotDetectionAttribute>() != null)
        {
            _logger.LogDebug("Skipping bot detection for {Path} (SkipBotDetection attribute)",
                context.Request.Path);
            await _next(context);
            return;
        }

        // Check skip paths from configuration
        if (ShouldSkipPath(context.Request.Path))
        {
            await _next(context);
            return;
        }

        // Test mode: Allow overriding bot detection via header
        if (_options.EnableTestMode)
        {
            var testMode = context.Request.Headers["ml-bot-test-mode"].FirstOrDefault();
            if (!string.IsNullOrEmpty(testMode))
            {
                await HandleTestMode(context, testMode);
                return;
            }
        }

        // Determine policy to use
        var policy = ResolvePolicy(context, endpoint, policyRegistry);
        var policyAttr = endpoint?.Metadata.GetMetadata<BotPolicyAttribute>();

        // Run full pipeline with orchestrator - always use full detection
        var aggregatedResult = await orchestrator.DetectWithPolicyAsync(context, policy, context.RequestAborted);
        PopulateContextFromAggregated(context, aggregatedResult, policy.Name);

        // Add response headers if enabled
        if (_options.ResponseHeaders.Enabled)
        {
            AddResponseHeaders(context, aggregatedResult, policy.Name);
        }

        // Determine if we should block/throttle
        var shouldBlock = ShouldBlockRequest(aggregatedResult, policy, policyAttr);

        if (shouldBlock.Block)
        {
            await HandleBlockedRequest(context, aggregatedResult, policy, policyAttr, shouldBlock.Action);
            return;
        }

        // Continue pipeline
        await _next(context);
    }

    #region Policy Resolution

    private DetectionPolicy ResolvePolicy(
        HttpContext context,
        Endpoint? endpoint,
        IPolicyRegistry policyRegistry)
    {
        // 0. Check for policy query parameter (for demo/testing - only when test mode enabled)
        if (_options.EnableTestMode && context.Request.Query.TryGetValue("policy", out var policyParam))
        {
            var queryPolicy = policyRegistry.GetPolicy(policyParam.ToString());
            if (queryPolicy != null)
            {
                _logger.LogDebug("Using policy '{Policy}' from query parameter for {Path}",
                    queryPolicy.Name, context.Request.Path);
                return queryPolicy;
            }
        }

        // 1. Check for [BotPolicy] attribute on action/controller
        var policyAttr = endpoint?.Metadata.GetMetadata<BotPolicyAttribute>();
        if (policyAttr != null && !string.IsNullOrEmpty(policyAttr.PolicyName))
        {
            var attrPolicy = policyRegistry.GetPolicy(policyAttr.PolicyName);
            if (attrPolicy != null)
            {
                _logger.LogDebug("Using policy '{Policy}' from attribute for {Path}",
                    attrPolicy.Name, context.Request.Path);
                return attrPolicy;
            }

            _logger.LogWarning("Policy '{Policy}' from attribute not found, falling back to path-based",
                policyAttr.PolicyName);
        }

        // 2. Fall back to path-based policy resolution
        return policyRegistry.GetPolicyForPath(context.Request.Path);
    }

    private bool ShouldSkipPath(PathString path)
    {
        // Check ExcludedPaths first (complete bypass)
        foreach (var excludedPath in _options.ExcludedPaths)
        {
            if (path.StartsWithSegments(excludedPath, StringComparison.OrdinalIgnoreCase))
            {
                _logger.LogDebug("Skipping bot detection for {Path} (ExcludedPaths)", path);
                return true;
            }
        }

        // Also check ResponseHeaders.SkipPaths for backward compatibility
        foreach (var skipPath in _options.ResponseHeaders.SkipPaths)
        {
            if (path.StartsWithSegments(skipPath, StringComparison.OrdinalIgnoreCase))
                return true;
        }

        return false;
    }

    /// <summary>
    ///     Checks if the path has an override that allows it through regardless of detection.
    /// </summary>
    private bool HasPathOverride(PathString path, out string? overrideAction)
    {
        overrideAction = null;

        foreach (var (pattern, action) in _options.PathOverrides)
        {
            if (MatchesPathPattern(path, pattern))
            {
                overrideAction = action;
                return true;
            }
        }

        return false;
    }

    /// <summary>
    ///     Matches a path against a pattern with glob support.
    ///     Supports: exact match, prefix with *, and ** for recursive matching.
    /// </summary>
    private static bool MatchesPathPattern(PathString path, string pattern)
    {
        var pathValue = path.Value ?? "";

        // Exact match
        if (pattern.Equals(pathValue, StringComparison.OrdinalIgnoreCase))
            return true;

        // Prefix match with single * (e.g., "/api/public/*" matches "/api/public/foo" but not "/api/public/foo/bar")
        if (pattern.EndsWith("/*"))
        {
            var prefix = pattern[..^2];
            if (pathValue.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
            {
                var remainder = pathValue[prefix.Length..];
                // Must have exactly one more segment (starts with / and no more /)
                return remainder.StartsWith('/') && !remainder[1..].Contains('/');
            }
        }

        // Recursive match with ** (e.g., "/api/public/**" matches any depth)
        if (pattern.EndsWith("/**"))
        {
            var prefix = pattern[..^3];
            return pathValue.StartsWith(prefix, StringComparison.OrdinalIgnoreCase);
        }

        // Simple prefix match (e.g., "/api/public" matches "/api/public" and "/api/public/anything")
        if (!pattern.Contains('*'))
        {
            return pathValue.StartsWith(pattern, StringComparison.OrdinalIgnoreCase);
        }

        return false;
    }

    #endregion

    #region Response Headers

    private void AddResponseHeaders(
        HttpContext context,
        AggregatedEvidence aggregated,
        string policyName)
    {
        var headerConfig = _options.ResponseHeaders;
        var prefix = headerConfig.HeaderPrefix;

        // Always add policy name if configured
        if (headerConfig.IncludePolicyName)
        {
            context.Response.Headers.TryAdd($"{prefix}Policy", policyName);
        }

        // Risk score (always included)
        context.Response.Headers.TryAdd($"{prefix}Risk-Score", aggregated.BotProbability.ToString("F3"));
        context.Response.Headers.TryAdd($"{prefix}Risk-Band", aggregated.RiskBand.ToString());

        if (headerConfig.IncludeConfidence)
        {
            context.Response.Headers.TryAdd($"{prefix}Confidence", aggregated.Confidence.ToString("F3"));
        }

        if (headerConfig.IncludeDetectors && aggregated.ContributingDetectors.Count > 0)
        {
            context.Response.Headers.TryAdd($"{prefix}Detectors",
                string.Join(",", aggregated.ContributingDetectors));
        }

        if (headerConfig.IncludeProcessingTime)
        {
            context.Response.Headers.TryAdd($"{prefix}Processing-Ms",
                aggregated.TotalProcessingTimeMs.ToString("F1"));
        }

        if (aggregated.PolicyAction.HasValue)
        {
            context.Response.Headers.TryAdd($"{prefix}Action", aggregated.PolicyAction.Value.ToString());
        }

        if (aggregated.PrimaryBotName != null && headerConfig.IncludeBotName)
        {
            context.Response.Headers.TryAdd($"{prefix}Bot-Name", aggregated.PrimaryBotName);
        }

        if (aggregated.EarlyExit)
        {
            context.Response.Headers.TryAdd($"{prefix}Early-Exit", "true");
            if (aggregated.EarlyExitVerdict.HasValue)
            {
                context.Response.Headers.TryAdd($"{prefix}Verdict", aggregated.EarlyExitVerdict.Value.ToString());
            }
        }

        // Full JSON result if enabled (useful for debugging)
        if (headerConfig.IncludeFullJson)
        {
            var jsonResult = new
            {
                risk = aggregated.BotProbability,
                confidence = aggregated.Confidence,
                riskBand = aggregated.RiskBand.ToString(),
                policy = policyName,
                detectors = aggregated.ContributingDetectors,
                categories = aggregated.CategoryBreakdown.ToDictionary(
                    kv => kv.Key,
                    kv => kv.Value.Score)
            };
            var json = JsonSerializer.Serialize(jsonResult);
            // Base64 encode to avoid header encoding issues
            var base64 = Convert.ToBase64String(System.Text.Encoding.UTF8.GetBytes(json));
            context.Response.Headers.TryAdd($"{prefix}Result-Json", base64);
        }
    }

    #endregion

    #region Blocking Logic

    private (bool Block, BotBlockAction Action) ShouldBlockRequest(
        AggregatedEvidence aggregated,
        DetectionPolicy policy,
        BotPolicyAttribute? policyAttr)
    {
        // Check attribute skip
        if (policyAttr?.Skip == true)
            return (false, BotBlockAction.Default);

        // Determine action from attribute or default
        var defaultAction = policyAttr?.BlockAction ?? BotBlockAction.Default;

        // Check if policy action says to block
        if (aggregated.PolicyAction == PolicyAction.Block)
            return (true, defaultAction == BotBlockAction.Default ? BotBlockAction.StatusCode : defaultAction);

        // Check if policy action says to throttle
        if (aggregated.PolicyAction == PolicyAction.Throttle)
            return (true, BotBlockAction.Throttle);

        // Check if policy action says to challenge
        if (aggregated.PolicyAction == PolicyAction.Challenge)
            return (true, BotBlockAction.Challenge);

        // Check if risk exceeds immediate block threshold
        if (aggregated.BotProbability >= policy.ImmediateBlockThreshold)
            return (true, defaultAction == BotBlockAction.Default ? BotBlockAction.StatusCode : defaultAction);

        // Check for verified bad bot
        if (aggregated.EarlyExit && aggregated.EarlyExitVerdict == EarlyExitVerdict.VerifiedBadBot)
            return (true, defaultAction == BotBlockAction.Default ? BotBlockAction.StatusCode : defaultAction);

        return (false, BotBlockAction.Default);
    }

    private async Task HandleBlockedRequest(
        HttpContext context,
        AggregatedEvidence aggregated,
        DetectionPolicy policy,
        BotPolicyAttribute? policyAttr,
        BotBlockAction action)
    {
        var riskScore = aggregated.BotProbability;

        _logger.LogInformation(
            "Blocking request to {Path}: policy={Policy}, risk={Risk:F2}, action={Action}",
            context.Request.Path, policy.Name, riskScore, action);

        switch (action)
        {
            case BotBlockAction.StatusCode:
            case BotBlockAction.Default:
                var statusCode = policyAttr?.BlockStatusCode ?? 403;
                context.Response.StatusCode = statusCode;
                context.Response.ContentType = "application/json";
                await context.Response.WriteAsJsonAsync(new
                {
                    error = "Access denied",
                    reason = "Request blocked by bot detection",
                    riskScore,
                    policy = policy.Name
                });
                break;

            case BotBlockAction.Redirect:
                var redirectUrl = policyAttr?.BlockRedirectUrl ?? _options.Throttling.BlockRedirectUrl ?? "/blocked";
                context.Response.Redirect(redirectUrl);
                break;

            case BotBlockAction.Challenge:
                context.Response.StatusCode = 403;
                context.Response.Headers["X-Bot-Challenge"] = "required";
                context.Response.ContentType = "application/json";
                await context.Response.WriteAsJsonAsync(new
                {
                    error = "Challenge required",
                    challengeType = _options.Throttling.ChallengeType,
                    riskScore
                });
                break;

            case BotBlockAction.Throttle:
                await HandleThrottle(context, riskScore);
                break;

            case BotBlockAction.LogOnly:
                // Log but don't block - continue to next middleware
                _logger.LogWarning(
                    "Bot detected (shadow mode): path={Path}, risk={Risk:F2}, policy={Policy}",
                    context.Request.Path, riskScore, policy.Name);
                await _next(context);
                break;
        }
    }

    private async Task HandleThrottle(HttpContext context, double riskScore)
    {
        var throttleConfig = _options.Throttling;

        // Calculate retry delay with optional jitter
        var baseDelay = throttleConfig.BaseDelaySeconds;
        var delay = baseDelay;

        if (throttleConfig.EnableJitter)
        {
            // Add jitter: ±JitterPercent of base delay
            var jitterRange = baseDelay * (throttleConfig.JitterPercent / 100.0);
            var jitterValue = (Jitter.NextDouble() * 2 - 1) * jitterRange;
            delay = Math.Max(1, baseDelay + (int)jitterValue);
        }

        // Scale delay by risk score if configured
        if (throttleConfig.ScaleByRisk)
        {
            delay = (int)(delay * (1 + riskScore));
        }

        // Cap at max delay
        delay = Math.Min(delay, throttleConfig.MaxDelaySeconds);

        context.Response.StatusCode = 429;
        context.Response.Headers["Retry-After"] = delay.ToString();

        // Add jitter indication header (helps with debugging, doesn't reveal exact algorithm)
        if (throttleConfig.EnableJitter)
        {
            context.Response.Headers["X-Retry-Jitter"] = "applied";
        }

        context.Response.ContentType = "application/json";

        // Optionally delay response to slow down bots
        if (throttleConfig.DelayResponse)
        {
            var responseDelay = Math.Min(throttleConfig.ResponseDelayMs, 5000);
            if (throttleConfig.EnableJitter)
            {
                // Add jitter to response delay too
                var delayJitter = (int)(responseDelay * (throttleConfig.JitterPercent / 100.0));
                responseDelay += Jitter.Next(-delayJitter, delayJitter);
                responseDelay = Math.Max(100, responseDelay);
            }

            await Task.Delay(responseDelay, context.RequestAborted);
        }

        await context.Response.WriteAsJsonAsync(new
        {
            error = "Too many requests",
            retryAfter = delay,
            message = throttleConfig.ThrottleMessage
        });
    }

    #endregion

    #region Test Mode

    private async Task HandleTestMode(HttpContext context, string testMode)
    {
        _logger.LogInformation("Test mode: Simulating bot detection as '{Mode}'", testMode);

        var testResult = CreateTestResult(testMode);
        PopulateContextItems(context, testResult);

        // Add test mode headers
        context.Response.Headers.TryAdd("X-Test-Mode",
            testMode.Equals("disable", StringComparison.OrdinalIgnoreCase) ? "disabled" : "true");

        if (testResult.IsBot)
        {
            context.Response.Headers.TryAdd("X-Bot-Detected", "true");
            context.Response.Headers.TryAdd("X-Bot-Confidence", testResult.ConfidenceScore.ToString("F2"));
        }

        _logger.LogInformation(
            "Test mode result: IsBot={IsBot}, Type={BotType}, Confidence={Confidence:F2}",
            testResult.IsBot, testResult.BotType, testResult.ConfidenceScore);

        await _next(context);
    }

    private static BotDetectionResult CreateTestResult(string testMode)
    {
        if (TestModeConfigs.TryGetValue(testMode, out var config))
        {
            return new BotDetectionResult
            {
                IsBot = config.IsBot,
                ConfidenceScore = config.Confidence,
                BotType = config.BotType,
                BotName = config.BotName,
                Reasons =
                [
                    new DetectionReason
                    {
                        Category = "Test Mode",
                        Detail = config.Detail,
                        ConfidenceImpact = config.Confidence
                    }
                ]
            };
        }

        // Unknown test mode - create generic bot
        return new BotDetectionResult
        {
            IsBot = true,
            ConfidenceScore = 0.7,
            BotType = BotType.Unknown,
            BotName = $"Test {testMode}",
            Reasons =
            [
                new DetectionReason
                {
                    Category = "Test Mode",
                    Detail = $"Simulated '{testMode}' bot",
                    ConfidenceImpact = 0.7
                }
            ]
        };
    }

    private record TestModeConfig(bool IsBot, double Confidence, BotType? BotType, string? BotName, string Detail);

    #endregion

    #region Context Population

    private static void PopulateContextFromAggregated(
        HttpContext context,
        AggregatedEvidence result,
        string policyName)
    {
        // Store full aggregated result
        context.Items[AggregatedEvidenceKey] = result;
        context.Items[PolicyNameKey] = policyName;
        context.Items[PolicyActionKey] = result.PolicyAction;

        // Map to legacy keys for compatibility
        var isBot = result.BotProbability >= 0.5;
        context.Items[IsBotKey] = isBot;
        context.Items[BotConfidenceKey] = result.BotProbability;
        context.Items[BotTypeKey] = result.PrimaryBotType;
        context.Items[BotNameKey] = result.PrimaryBotName;

        // Primary category from highest-contributing category
        if (result.CategoryBreakdown.Count > 0)
        {
            var primaryCategory = result.CategoryBreakdown
                .OrderByDescending(kv => Math.Abs(kv.Value.Score))
                .First();
            context.Items[BotCategoryKey] = primaryCategory.Key;
        }

        // Also create a legacy BotDetectionResult for compatibility
        var legacyResult = new BotDetectionResult
        {
            IsBot = isBot,
            ConfidenceScore = result.BotProbability,
            BotType = result.PrimaryBotType,
            BotName = result.PrimaryBotName,
            Reasons = result.Contributions.Select(c => new DetectionReason
            {
                Category = c.Category,
                Detail = c.Reason,
                ConfidenceImpact = c.ConfidenceDelta
            }).ToList()
        };
        context.Items[BotDetectionResultKey] = legacyResult;
    }

    private static void PopulateContextItems(HttpContext context, BotDetectionResult result)
    {
        // Full result object (for complete access)
        context.Items[BotDetectionResultKey] = result;

        // Individual properties (for quick access without casting)
        context.Items[IsBotKey] = result.IsBot;
        context.Items[BotConfidenceKey] = result.ConfidenceScore;
        context.Items[BotTypeKey] = result.BotType;
        context.Items[BotNameKey] = result.BotName;
        context.Items[DetectionReasonsKey] = result.Reasons;

        // Primary category (from highest-confidence reason)
        if (result.Reasons.Count > 0)
        {
            var primaryReason = result.Reasons.OrderByDescending(r => r.ConfidenceImpact).First();
            context.Items[BotCategoryKey] = primaryReason.Category;
        }
    }

    #endregion
}

/// <summary>
///     Extension methods for adding bot detection middleware.
/// </summary>
public static class BotDetectionMiddlewareExtensions
{
    /// <summary>
    ///     Add bot detection middleware to the pipeline.
    ///     Should be called after UseRouting() but before UseAuthorization().
    /// </summary>
    /// <example>
    ///     app.UseRouting();
    ///     app.UseBotDetection();
    ///     app.UseAuthorization();
    /// </example>
    public static IApplicationBuilder UseBotDetection(this IApplicationBuilder builder)
    {
        return builder.UseMiddleware<BotDetectionMiddleware>();
    }
}
