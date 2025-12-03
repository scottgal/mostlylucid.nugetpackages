using Microsoft.AspNetCore.Http;
using Mostlylucid.BotDetection.Models;

namespace Mostlylucid.BotDetection.Extensions;

/// <summary>
///     Extension methods for integrating bot detection with YARP (Yet Another Reverse Proxy).
/// </summary>
/// <remarks>
///     These extensions provide utilities for adding bot detection headers to proxied requests
///     and implementing bot-aware routing logic.
/// </remarks>
public static class YarpExtensions
{
    /// <summary>
    ///     Adds bot detection result headers to an outgoing request.
    ///     Call this from a YARP request transform to pass bot info to backend services.
    /// </summary>
    /// <param name="httpContext">The current HttpContext with bot detection results</param>
    /// <param name="addHeader">Action to add headers (receives header name and value)</param>
    /// <example>
    ///     <code>
    ///     builder.Services.AddReverseProxy()
    ///         .LoadFromConfig(configuration.GetSection("ReverseProxy"))
    ///         .AddTransforms(context =>
    ///         {
    ///             context.AddRequestTransform(transformContext =>
    ///             {
    ///                 transformContext.HttpContext.AddBotDetectionHeaders(
    ///                     (name, value) => transformContext.ProxyRequest.Headers.TryAddWithoutValidation(name, value));
    ///                 return ValueTask.CompletedTask;
    ///             });
    ///         });
    ///     </code>
    /// </example>
    public static void AddBotDetectionHeaders(this HttpContext httpContext, Action<string, string> addHeader)
    {
        var isBot = httpContext.IsBot();
        var confidence = httpContext.GetBotConfidence();

        addHeader("X-Bot-Detected", isBot.ToString().ToLowerInvariant());
        addHeader("X-Bot-Confidence", confidence.ToString("F2"));

        if (isBot)
        {
            var botType = httpContext.GetBotType();
            var botName = httpContext.GetBotName();
            var category = httpContext.GetBotCategory();

            if (botType.HasValue)
                addHeader("X-Bot-Type", botType.Value.ToString());

            if (!string.IsNullOrEmpty(botName))
                addHeader("X-Bot-Name", botName);

            if (!string.IsNullOrEmpty(category))
                addHeader("X-Bot-Category", category);

            // Add convenience flags
            addHeader("X-Is-Search-Engine", httpContext.IsSearchEngineBot().ToString().ToLowerInvariant());
            addHeader("X-Is-Malicious-Bot", httpContext.IsMaliciousBot().ToString().ToLowerInvariant());
            addHeader("X-Is-Social-Bot", httpContext.IsSocialMediaBot().ToString().ToLowerInvariant());
        }
    }

    /// <summary>
    ///     Adds comprehensive bot detection headers including all detection reasons.
    /// </summary>
    /// <param name="httpContext">The current HttpContext with bot detection results</param>
    /// <param name="addHeader">Action to add headers</param>
    public static void AddBotDetectionHeadersVerbose(this HttpContext httpContext, Action<string, string> addHeader)
    {
        // Add basic headers
        httpContext.AddBotDetectionHeaders(addHeader);

        // Add detection reasons as a semicolon-separated list
        var reasons = httpContext.GetDetectionReasons();
        if (reasons.Count > 0)
        {
            var reasonSummary = string.Join("; ", reasons.Select(r => $"{r.Category}: {r.Detail}"));
            addHeader("X-Bot-Detection-Reasons", reasonSummary);
        }
    }

    /// <summary>
    ///     Determines the YARP cluster to route to based on bot detection results.
    /// </summary>
    /// <param name="httpContext">The current HttpContext</param>
    /// <param name="defaultCluster">Cluster for normal traffic</param>
    /// <param name="crawlerCluster">Optional cluster for search engine bots</param>
    /// <param name="blockCluster">Optional cluster that returns 403 for malicious bots</param>
    /// <returns>The cluster ID to route to</returns>
    public static string GetBotAwareCluster(
        this HttpContext httpContext,
        string defaultCluster,
        string? crawlerCluster = null,
        string? blockCluster = null)
    {
        // Block malicious bots
        if (blockCluster != null && httpContext.IsMaliciousBot())
            return blockCluster;

        // Route search engines to crawler-optimized cluster
        if (crawlerCluster != null && httpContext.IsSearchEngineBot())
            return crawlerCluster;

        return defaultCluster;
    }

    /// <summary>
    ///     Checks if the request should be blocked based on bot detection.
    /// </summary>
    /// <param name="httpContext">The current HttpContext</param>
    /// <param name="minConfidence">Minimum confidence threshold to block</param>
    /// <param name="allowSearchEngines">Whether to allow search engine bots</param>
    /// <param name="allowSocialBots">Whether to allow social media bots</param>
    /// <returns>True if the request should be blocked</returns>
    public static bool ShouldBlockBot(
        this HttpContext httpContext,
        double minConfidence = 0.7,
        bool allowSearchEngines = true,
        bool allowSocialBots = true)
    {
        if (!httpContext.IsBot())
            return false;

        var confidence = httpContext.GetBotConfidence();
        if (confidence < minConfidence)
            return false;

        // Always block malicious bots
        if (httpContext.IsMaliciousBot())
            return true;

        // Check allowed types
        if (allowSearchEngines && httpContext.IsSearchEngineBot())
            return false;

        if (allowSocialBots && httpContext.IsSocialMediaBot())
            return false;

        return true;
    }
}
