using Microsoft.AspNetCore.Http;
using Mostlylucid.BotDetection.Middleware;
using Mostlylucid.BotDetection.Models;

namespace Mostlylucid.BotDetection.Extensions;

/// <summary>
///     Extension methods for easy access to bot detection results from HttpContext
/// </summary>
public static class HttpContextExtensions
{
    /// <summary>
    ///     Gets the bot detection result from the current request.
    ///     Returns null if bot detection middleware hasn't run.
    /// </summary>
    public static BotDetectionResult? GetBotDetectionResult(this HttpContext context)
    {
        return context.Items.TryGetValue(BotDetectionMiddleware.BotDetectionResultKey, out var result)
            ? result as BotDetectionResult
            : null;
    }

    /// <summary>
    ///     Returns true if the current request was detected as a bot.
    ///     Returns false if not a bot OR if detection hasn't run.
    /// </summary>
    public static bool IsBot(this HttpContext context)
    {
        return context.GetBotDetectionResult()?.IsBot ?? false;
    }

    /// <summary>
    ///     Returns true if the current request is from a verified good bot (e.g., Googlebot).
    /// </summary>
    public static bool IsVerifiedBot(this HttpContext context)
    {
        var result = context.GetBotDetectionResult();
        return result?.BotType == BotType.VerifiedBot;
    }

    /// <summary>
    ///     Returns true if the current request is from a search engine bot.
    /// </summary>
    public static bool IsSearchEngineBot(this HttpContext context)
    {
        var result = context.GetBotDetectionResult();
        return result?.BotType == BotType.SearchEngine || result?.BotType == BotType.VerifiedBot;
    }

    /// <summary>
    ///     Returns true if the current request is from a potentially malicious bot.
    /// </summary>
    public static bool IsMaliciousBot(this HttpContext context)
    {
        var result = context.GetBotDetectionResult();
        return result?.BotType == BotType.MaliciousBot;
    }

    /// <summary>
    ///     Returns true if the current request appears to be from a human visitor.
    /// </summary>
    public static bool IsHuman(this HttpContext context)
    {
        var result = context.GetBotDetectionResult();
        return result != null && !result.IsBot;
    }

    /// <summary>
    ///     Gets the bot confidence score (0.0 to 1.0).
    ///     Returns 0.0 if detection hasn't run.
    /// </summary>
    public static double GetBotConfidence(this HttpContext context)
    {
        return context.GetBotDetectionResult()?.ConfidenceScore ?? 0.0;
    }

    /// <summary>
    ///     Gets the detected bot type, or null if not a bot or detection hasn't run.
    /// </summary>
    public static BotType? GetBotType(this HttpContext context)
    {
        return context.GetBotDetectionResult()?.BotType;
    }

    /// <summary>
    ///     Gets the detected bot name (e.g., "Googlebot", "AhrefsBot"), or null if unknown.
    /// </summary>
    public static string? GetBotName(this HttpContext context)
    {
        return context.GetBotDetectionResult()?.BotName;
    }

    /// <summary>
    ///     Returns true if the request should be allowed (human or verified good bot).
    ///     Useful for protecting sensitive endpoints while allowing search engines.
    /// </summary>
    public static bool ShouldAllowRequest(this HttpContext context)
    {
        var result = context.GetBotDetectionResult();
        if (result == null) return true; // Allow if detection hasn't run

        // Allow humans and verified bots
        return !result.IsBot || result.BotType == BotType.VerifiedBot;
    }

    /// <summary>
    ///     Returns true if the request should be blocked (detected as bot, not verified).
    /// </summary>
    public static bool ShouldBlockRequest(this HttpContext context)
    {
        var result = context.GetBotDetectionResult();
        if (result == null) return false;

        return result.IsBot && result.BotType != BotType.VerifiedBot;
    }
}
