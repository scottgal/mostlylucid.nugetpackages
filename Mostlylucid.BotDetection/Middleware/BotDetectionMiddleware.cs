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
    private readonly ILogger<BotDetectionMiddleware> _logger = logger;
    private readonly RequestDelegate _next = next;
    private readonly BotDetectionOptions _options = options.Value;

    public async Task InvokeAsync(HttpContext context, IBotDetectionService botDetectionService)
    {
        // Test mode: Allow overriding bot detection via header (off by default)
        // Usage: ml-bot-test-mode: disable     (bypasses all detection)
        //        ml-bot-test-mode: bot          (forces bot detection)
        //        ml-bot-test-mode: human        (forces human detection)
        //        ml-bot-test-mode: googlebot    (simulates Googlebot)
        //        ml-bot-test-mode: scraper      (simulates scraper bot)
        // Security: Only check header when test mode is enabled to prevent information leakage
        if (_options.EnableTestMode)
        {
            var testMode = context.Request.Headers["ml-bot-test-mode"].FirstOrDefault();
            if (!string.IsNullOrEmpty(testMode))
            {
                _logger.LogInformation("Test mode: Simulating bot detection as '{Mode}'", testMode);

                // If "disable", bypass all detection
                if (testMode.Equals("disable", StringComparison.OrdinalIgnoreCase))
                {
                    var disabledResult = new BotDetectionResult
                    {
                        IsBot = false,
                        ConfidenceScore = 0.0,
                        Reasons = new List<DetectionReason>
                        {
                            new()
                            {
                                Category = "Test Mode",
                                Detail = "Bot detection disabled via test header",
                                ConfidenceImpact = 0.0
                            }
                        }
                    };

                    context.Items[BotDetectionResultKey] = disabledResult;
                    context.Response.Headers.TryAdd("X-Test-Mode", "disabled");
                    await _next(context);
                    return;
                }

                // Create test result based on test mode value
                var testResult = CreateTestResult(testMode);
                context.Items[BotDetectionResultKey] = testResult;

                // Add test mode headers
                context.Response.Headers.TryAdd("X-Test-Mode", "true");
                if (testResult.IsBot)
                {
                    context.Response.Headers.TryAdd("X-Bot-Detected", "true");
                    context.Response.Headers.TryAdd("X-Bot-Confidence",
                        testResult.ConfidenceScore.ToString("F2"));
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
            context.Response.Headers.TryAdd("X-Bot-Confidence",
                result.ConfidenceScore.ToString("F2"));

            _logger.LogInformation(
                "Bot detected: {BotType}, Confidence: {Confidence:F2}, IP: {IP}",
                result.BotType, result.ConfidenceScore, context.Connection.RemoteIpAddress);
        }

        // Continue pipeline
        await _next(context);
    }

    private BotDetectionResult CreateTestResult(string testMode)
    {
        return testMode.ToLowerInvariant() switch
        {
            "human" => new BotDetectionResult
            {
                IsBot = false,
                ConfidenceScore = 0.0,
                Reasons = new List<DetectionReason>
                {
                    new()
                    {
                        Category = "Test Mode",
                        Detail = "Simulated human traffic",
                        ConfidenceImpact = 0.0
                    }
                }
            },
            "bot" => new BotDetectionResult
            {
                IsBot = true,
                ConfidenceScore = 1.0,
                BotType = BotType.Unknown,
                BotName = "Test Bot",
                Reasons = new List<DetectionReason>
                {
                    new()
                    {
                        Category = "Test Mode",
                        Detail = "Simulated bot traffic",
                        ConfidenceImpact = 1.0
                    }
                }
            },
            "googlebot" => new BotDetectionResult
            {
                IsBot = true,
                ConfidenceScore = 0.95,
                BotType = BotType.SearchEngine,
                BotName = "Googlebot",
                Reasons = new List<DetectionReason>
                {
                    new()
                    {
                        Category = "Test Mode",
                        Detail = "Simulated Googlebot",
                        ConfidenceImpact = 0.95
                    }
                }
            },
            "bingbot" => new BotDetectionResult
            {
                IsBot = true,
                ConfidenceScore = 0.95,
                BotType = BotType.SearchEngine,
                BotName = "Bingbot",
                Reasons = new List<DetectionReason>
                {
                    new()
                    {
                        Category = "Test Mode",
                        Detail = "Simulated Bingbot",
                        ConfidenceImpact = 0.95
                    }
                }
            },
            "scraper" => new BotDetectionResult
            {
                IsBot = true,
                ConfidenceScore = 0.9,
                BotType = BotType.Scraper,
                BotName = "Test Scraper",
                Reasons = new List<DetectionReason>
                {
                    new()
                    {
                        Category = "Test Mode",
                        Detail = "Simulated scraper bot",
                        ConfidenceImpact = 0.9
                    }
                }
            },
            "malicious" => new BotDetectionResult
            {
                IsBot = true,
                ConfidenceScore = 1.0,
                BotType = BotType.MaliciousBot,
                BotName = "Test Malicious Bot",
                Reasons = new List<DetectionReason>
                {
                    new()
                    {
                        Category = "Test Mode",
                        Detail = "Simulated malicious bot",
                        ConfidenceImpact = 1.0
                    }
                }
            },
            "social" or "socialbot" => new BotDetectionResult
            {
                IsBot = true,
                ConfidenceScore = 0.85,
                BotType = BotType.SocialMediaBot,
                BotName = "Test Social Bot",
                Reasons = new List<DetectionReason>
                {
                    new()
                    {
                        Category = "Test Mode",
                        Detail = "Simulated social media bot",
                        ConfidenceImpact = 0.85
                    }
                }
            },
            "monitor" or "monitoring" => new BotDetectionResult
            {
                IsBot = true,
                ConfidenceScore = 0.8,
                BotType = BotType.MonitoringBot,
                BotName = "Test Monitoring Bot",
                Reasons = new List<DetectionReason>
                {
                    new()
                    {
                        Category = "Test Mode",
                        Detail = "Simulated monitoring bot",
                        ConfidenceImpact = 0.8
                    }
                }
            },
            _ => new BotDetectionResult
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
            }
        };
    }
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