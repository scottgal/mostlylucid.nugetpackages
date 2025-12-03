using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Mostlylucid.BotDetection.Filters;
using Mostlylucid.BotDetection.Services;

namespace Mostlylucid.BotDetection.Extensions;

/// <summary>
///     Extension methods for minimal API route builders
/// </summary>
public static class RouteBuilderExtensions
{
    /// <summary>
    ///     Adds bot blocking to this endpoint.
    /// </summary>
    /// <example>
    ///     app.MapGet("/api/data", () => "sensitive")
    ///        .BlockBots();
    /// </example>
    public static RouteHandlerBuilder BlockBots(this RouteHandlerBuilder builder,
        bool allowVerifiedBots = false,
        bool allowSearchEngines = false,
        double minConfidence = 0.0,
        int statusCode = 403)
    {
        return builder.AddEndpointFilter(new BlockBotsEndpointFilter(
            allowVerifiedBots, allowSearchEngines, minConfidence, statusCode));
    }

    /// <summary>
    ///     Requires human visitors for this endpoint (blocks all bots including verified).
    /// </summary>
    /// <example>
    ///     app.MapPost("/api/submit", () => "submitted")
    ///        .RequireHuman();
    /// </example>
    public static RouteHandlerBuilder RequireHuman(this RouteHandlerBuilder builder, int statusCode = 403)
    {
        return builder.AddEndpointFilter(new RequireHumanEndpointFilter(statusCode));
    }

    /// <summary>
    ///     Maps built-in bot detection diagnostic endpoints.
    /// </summary>
    /// <example>
    ///     app.MapBotDetectionEndpoints("/bot-detection");
    ///     // Creates:
    ///     //   GET /bot-detection/check   - Check current request
    ///     //   GET /bot-detection/stats   - Get statistics
    ///     //   GET /bot-detection/health  - Health check
    /// </example>
    public static IEndpointRouteBuilder MapBotDetectionEndpoints(
        this IEndpointRouteBuilder endpoints,
        string prefix = "/bot-detection")
    {
        var group = endpoints.MapGroup(prefix)
            .WithTags("Bot Detection");

        // Check current request
        group.MapGet("/check", (HttpContext context) =>
        {
            var result = context.GetBotDetectionResult();

            if (result == null)
            {
                return Results.Ok(new
                {
                    status = "unknown",
                    message = "Bot detection middleware has not run for this request",
                    isBot = false,
                    isHuman = true,
                    isVerifiedBot = false,
                    isSearchEngineBot = false,
                    confidenceScore = 0.0,
                    botType = (string?)null,
                    botName = (string?)null
                });
            }

            return Results.Ok(new
            {
                isBot = result.IsBot,
                isHuman = !result.IsBot,
                isVerifiedBot = context.IsVerifiedBot(),
                isSearchEngineBot = context.IsSearchEngineBot(),
                confidenceScore = result.ConfidenceScore,
                botType = result.BotType?.ToString(),
                botName = result.BotName,
                processingTimeMs = result.ProcessingTimeMs,
                reasons = result.Reasons.Select(r => new
                {
                    category = r.Category,
                    // Clean up any newlines/carriage returns in detail text
                    detail = r.Detail?.Replace("\r\n", " ").Replace("\r", " ").Replace("\n", " ").Trim(),
                    impact = r.ConfidenceImpact
                })
            });
        })
        .WithName("BotDetection_Check")
        .WithSummary("Check if the current request is from a bot");

        // Get statistics
        group.MapGet("/stats", (IBotDetectionService botService) =>
        {
            var stats = botService.GetStatistics();
            return Results.Ok(new
            {
                totalRequests = stats.TotalRequests,
                botsDetected = stats.BotsDetected,
                botPercentage = stats.TotalRequests > 0
                    ? Math.Round((double)stats.BotsDetected / stats.TotalRequests * 100, 2)
                    : 0,
                verifiedBots = stats.VerifiedBots,
                maliciousBots = stats.MaliciousBots,
                averageProcessingTimeMs = Math.Round(stats.AverageProcessingTimeMs, 2),
                botTypeBreakdown = stats.BotTypeBreakdown
            });
        })
        .WithName("BotDetection_Stats")
        .WithSummary("Get bot detection statistics");

        // Health check
        group.MapGet("/health", (IBotDetectionService botService) =>
        {
            var stats = botService.GetStatistics();
            return Results.Ok(new
            {
                status = "healthy",
                service = "BotDetection",
                totalRequests = stats.TotalRequests,
                averageResponseMs = Math.Round(stats.AverageProcessingTimeMs, 2)
            });
        })
        .WithName("BotDetection_Health")
        .WithSummary("Bot detection service health check");

        return endpoints;
    }
}
