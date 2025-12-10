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
    ///     Adds FULL bot detection headers including all metadata for UI display.
    ///     This includes: results, probabilities, reasons, detector contributions, YARP info, etc.
    ///
    ///     USE THIS for comprehensive dashboard display behind YARP proxy.
    /// </summary>
    /// <param name="httpContext">The current HttpContext with bot detection results</param>
    /// <param name="addHeader">Action to add headers</param>
    public static void AddBotDetectionHeadersFull(this HttpContext httpContext, Action<string, string> addHeader)
    {
        // Add basic headers
        httpContext.AddBotDetectionHeaders(addHeader);

        // Get aggregated evidence from context if available
        if (httpContext.Items.TryGetValue("BotDetection.Evidence", out var evidenceObj) &&
            evidenceObj is Orchestration.AggregatedEvidence evidence)
        {
            // Core detection results
            addHeader("X-Bot-Detection-Result", evidence.BotProbability > 0.5 ? "true" : "false");
            addHeader("X-Bot-Detection-Probability", evidence.BotProbability.ToString("F4"));
            addHeader("X-Bot-Detection-Confidence", evidence.Confidence.ToString("F4"));
            addHeader("X-Bot-Detection-RiskBand", evidence.RiskBand.ToString());

            // Bot identification
            if (evidence.PrimaryBotType.HasValue)
                addHeader("X-Bot-Detection-BotType", evidence.PrimaryBotType.Value.ToString());

            if (!string.IsNullOrEmpty(evidence.PrimaryBotName))
                addHeader("X-Bot-Detection-BotName", evidence.PrimaryBotName);

            // Policy and action
            if (!string.IsNullOrEmpty(evidence.PolicyName))
                addHeader("X-Bot-Detection-Policy", evidence.PolicyName);

            var action = evidence.PolicyAction?.ToString() ?? evidence.TriggeredActionPolicyName;
            if (!string.IsNullOrEmpty(action))
                addHeader("X-Bot-Detection-Action", action);

            // Processing metrics
            addHeader("X-Bot-Detection-ProcessingMs", evidence.TotalProcessingTimeMs.ToString("F2"));

            // Top reasons (JSON array for easy parsing)
            var topReasons = evidence.Contributions
                .Where(c => !string.IsNullOrEmpty(c.Reason))
                .OrderByDescending(c => Math.Abs(c.ConfidenceDelta * c.Weight))
                .Take(5)
                .Select(c => c.Reason)
                .ToList();

            if (topReasons.Any())
            {
                var reasonsJson = System.Text.Json.JsonSerializer.Serialize(topReasons);
                addHeader("X-Bot-Detection-Reasons", reasonsJson);
            }

            // Detector contributions (JSON array)
            var contributionsData = evidence.Contributions
                .GroupBy(c => c.DetectorName)
                .Select(g => new
                {
                    Name = g.Key,
                    Category = g.First().Category,
                    ConfidenceDelta = g.Sum(c => c.ConfidenceDelta),
                    Weight = g.Sum(c => c.Weight),
                    Contribution = g.Sum(c => c.ConfidenceDelta * c.Weight),
                    Reason = string.Join("; ", g.Select(c => c.Reason).Where(r => !string.IsNullOrEmpty(r))),
                    ExecutionTimeMs = g.Sum(c => c.ProcessingTimeMs),
                    Priority = g.First().Priority
                })
                .OrderByDescending(d => Math.Abs(d.Contribution))
                .ToList();

            if (contributionsData.Any())
            {
                var contributionsJson = System.Text.Json.JsonSerializer.Serialize(contributionsData);
                addHeader("X-Bot-Detection-Contributions", contributionsJson);
            }

            // Request metadata
            addHeader("X-Bot-Detection-RequestId", httpContext.TraceIdentifier);
        }

        // YARP routing info (if available)
        if (httpContext.Items.TryGetValue("Yarp.Cluster", out var cluster) && cluster != null)
        {
            addHeader("X-Bot-Detection-Cluster", cluster.ToString()!);
        }

        if (httpContext.Items.TryGetValue("Yarp.Destination", out var dest) && dest != null)
        {
            addHeader("X-Bot-Detection-Destination", dest.ToString()!);
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
