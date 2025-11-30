using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Mostlylucid.BotDetection.Models;
using Mostlylucid.BotDetection.Services;

namespace Mostlylucid.BotDetection.Middleware;

/// <summary>
///     Middleware that detects bots and adds detection result to HttpContext
/// </summary>
public class BotDetectionMiddleware(
    RequestDelegate next,
    ILogger<BotDetectionMiddleware> logger,
    IOptions<BotDetectionOptions> options)
{
    public const string BotDetectionResultKey = "BotDetectionResult";

    private static readonly Dictionary<string, TestModeConfig> TestModeConfigs = new(StringComparer.OrdinalIgnoreCase)
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

    private readonly ILogger<BotDetectionMiddleware> _logger = logger;
    private readonly RequestDelegate _next = next;
    private readonly BotDetectionOptions _options = options.Value;

    public async Task InvokeAsync(HttpContext context, IBotDetectionService botDetectionService)
    {
        // Test mode: Allow overriding bot detection via header (off by default)
        // Security: Only check header when test mode is enabled to prevent information leakage
        if (_options.EnableTestMode)
        {
            var testMode = context.Request.Headers["ml-bot-test-mode"].FirstOrDefault();
            if (!string.IsNullOrEmpty(testMode))
            {
                _logger.LogInformation("Test mode: Simulating bot detection as '{Mode}'", testMode);

                var testResult = CreateTestResult(testMode);
                context.Items[BotDetectionResultKey] = testResult;

                // Add test mode headers
                context.Response.Headers.TryAdd("X-Test-Mode", testMode.Equals("disable", StringComparison.OrdinalIgnoreCase) ? "disabled" : "true");

                if (testResult.IsBot)
                {
                    context.Response.Headers.TryAdd("X-Bot-Detected", "true");
                    context.Response.Headers.TryAdd("X-Bot-Confidence", testResult.ConfidenceScore.ToString("F2"));
                }

                _logger.LogInformation(
                    "Test mode result: IsBot={IsBot}, Type={BotType}, Confidence={Confidence:F2}",
                    testResult.IsBot, testResult.BotType, testResult.ConfidenceScore);

                await _next(context);
                return;
            }
        }

        // Run normal bot detection
        var result = await botDetectionService.DetectAsync(context, context.RequestAborted);

        // Store result in HttpContext for access by controllers/views
        context.Items[BotDetectionResultKey] = result;

        // Add custom header with bot detection result (for debugging)
        if (result.IsBot)
        {
            context.Response.Headers.TryAdd("X-Bot-Detected", "true");
            context.Response.Headers.TryAdd("X-Bot-Confidence", result.ConfidenceScore.ToString("F2"));

            _logger.LogInformation(
                "Bot detected: {BotType}, Confidence: {Confidence:F2}, IP: {IP}",
                result.BotType, result.ConfidenceScore, context.Connection.RemoteIpAddress);
        }

        // Continue pipeline
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
                Reasons = new List<DetectionReason>
                {
                    new()
                    {
                        Category = "Test Mode",
                        Detail = config.Detail,
                        ConfidenceImpact = config.Confidence
                    }
                }
            };
        }

        // Unknown test mode - create generic bot
        return new BotDetectionResult
        {
            IsBot = true,
            ConfidenceScore = 0.7,
            BotType = BotType.Unknown,
            BotName = $"Test {testMode}",
            Reasons = new List<DetectionReason>
            {
                new()
                {
                    Category = "Test Mode",
                    Detail = $"Simulated '{testMode}' bot",
                    ConfidenceImpact = 0.7
                }
            }
        };
    }

    private record TestModeConfig(bool IsBot, double Confidence, BotType? BotType, string? BotName, string Detail);
}

/// <summary>
///     Extension methods for adding bot detection middleware
/// </summary>
public static class BotDetectionMiddlewareExtensions
{
    /// <summary>
    ///     Add bot detection middleware to the pipeline
    /// </summary>
    public static IApplicationBuilder UseBotDetection(this IApplicationBuilder builder)
    {
        return builder.UseMiddleware<BotDetectionMiddleware>();
    }
}
