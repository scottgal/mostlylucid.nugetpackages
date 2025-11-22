using System.Diagnostics;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Mostlylucid.BotDetection.Detectors;
using Mostlylucid.BotDetection.Models;

namespace Mostlylucid.BotDetection.Services;

/// <summary>
///     Main bot detection service that orchestrates multiple detection strategies
/// </summary>
public class BotDetectionService(
    ILogger<BotDetectionService> logger,
    IOptions<BotDetectionOptions> options,
    IMemoryCache cache,
    IEnumerable<IDetector> detectors)
    : IBotDetectionService
{
    private readonly BotDetectionStatistics _statistics = new();
    private readonly object _statsLock = new();

    public async Task<BotDetectionResult> DetectAsync(HttpContext context,
        CancellationToken cancellationToken = default)
    {
        var sw = Stopwatch.StartNew();

        try
        {
            // Check cache first
            var cacheKey = BuildCacheKey(context);
            if (cache.TryGetValue<BotDetectionResult>(cacheKey, out var cachedResult) && cachedResult != null)
            {
                logger.LogDebug("Returning cached bot detection result");
                return cachedResult;
            }

            var result = new BotDetectionResult();
            var detectorResults = new List<DetectorResult>();

            // Run all enabled detectors
            foreach (var detector in detectors)
                try
                {
                    var detectorResult = await detector.DetectAsync(context, cancellationToken);
                    detectorResults.Add(detectorResult);

                    logger.LogDebug("{Detector} confidence: {Confidence:F2}",
                        detector.Name, detectorResult.Confidence);
                }
                catch (Exception ex)
                {
                    logger.LogError(ex, "Detector {Detector} failed", detector.Name);
                }

            // Combine results using weighted scoring
            result = CombineResults(detectorResults);
            sw.Stop();
            result.ProcessingTimeMs = sw.ElapsedMilliseconds;

            // Cache result
            cache.Set(cacheKey, result, TimeSpan.FromSeconds(options.Value.CacheDurationSeconds));

            // Update statistics
            UpdateStatistics(result);

            logger.LogInformation(
                "Bot detection complete: IsBot={IsBot}, Confidence={Confidence:F2}, Time={Time}ms",
                result.IsBot, result.ConfidenceScore, result.ProcessingTimeMs);

            return result;
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Bot detection failed");
            sw.Stop();
            return new BotDetectionResult
            {
                IsBot = false,
                ConfidenceScore = 0.0,
                ProcessingTimeMs = sw.ElapsedMilliseconds
            };
        }
    }

    public BotDetectionStatistics GetStatistics()
    {
        lock (_statsLock)
        {
            return new BotDetectionStatistics
            {
                TotalRequests = _statistics.TotalRequests,
                BotsDetected = _statistics.BotsDetected,
                VerifiedBots = _statistics.VerifiedBots,
                MaliciousBots = _statistics.MaliciousBots,
                AverageProcessingTimeMs = _statistics.AverageProcessingTimeMs,
                BotTypeBreakdown = new Dictionary<string, int>(_statistics.BotTypeBreakdown)
            };
        }
    }

    private BotDetectionResult CombineResults(List<DetectorResult> detectorResults)
    {
        var result = new BotDetectionResult();

        // Combine all reasons
        foreach (var detectorResult in detectorResults) result.Reasons.AddRange(detectorResult.Reasons);

        // Calculate weighted confidence score
        // Strategy: Take maximum confidence, but boost if multiple detectors agree
        var confidences = detectorResults.Select(r => r.Confidence).ToList();
        var maxConfidence = confidences.Any() ? confidences.Max() : 0.0;
        var avgConfidence = confidences.Any() ? confidences.Average() : 0.0;

        // If multiple detectors show suspicion, boost the score
        var suspiciousDetectors = confidences.Count(c => c > 0.3);
        var agreementBoost = suspiciousDetectors > 1 ? (suspiciousDetectors - 1) * 0.1 : 0.0;

        result.ConfidenceScore = Math.Min(maxConfidence + agreementBoost, 1.0);
        result.IsBot = result.ConfidenceScore >= options.Value.BotThreshold;

        // Determine bot type (prefer specific types over unknown)
        var botTypes = detectorResults
            .Where(r => r.BotType.HasValue && r.BotType != BotType.Unknown)
            .Select(r => r.BotType!.Value)
            .ToList();

        if (botTypes.Any())
        {
            // Prioritize verified bots, then malicious, then others
            if (botTypes.Contains(BotType.VerifiedBot))
            {
                result.BotType = BotType.VerifiedBot;
                result.IsBot = false; // Verified bots are allowed
                result.ConfidenceScore = 0.0;
            }
            else if (botTypes.Contains(BotType.MaliciousBot))
            {
                result.BotType = BotType.MaliciousBot;
            }
            else
            {
                result.BotType = botTypes.First();
            }
        }

        // Extract bot name if identified
        var botName = detectorResults.FirstOrDefault(r => !string.IsNullOrEmpty(r.BotName))?.BotName;
        if (!string.IsNullOrEmpty(botName)) result.BotName = botName;

        return result;
    }

    private string BuildCacheKey(HttpContext context)
    {
        var userAgent = context.Request.Headers.UserAgent.ToString();
        var ip = context.Connection.RemoteIpAddress?.ToString() ?? "unknown";

        // Include key headers that might affect detection
        var accept = context.Request.Headers.Accept.ToString();
        var acceptLanguage = context.Request.Headers.AcceptLanguage.ToString();

        return $"bot_detect_{ip}_{userAgent.GetHashCode()}_{accept.GetHashCode()}_{acceptLanguage.GetHashCode()}";
    }

    private void UpdateStatistics(BotDetectionResult result)
    {
        lock (_statsLock)
        {
            _statistics.TotalRequests++;

            if (result.IsBot)
            {
                _statistics.BotsDetected++;

                if (result.BotType == BotType.VerifiedBot)
                    _statistics.VerifiedBots++;

                if (result.BotType == BotType.MaliciousBot)
                    _statistics.MaliciousBots++;

                if (result.BotType.HasValue)
                {
                    var typeName = result.BotType.Value.ToString();
                    _statistics.BotTypeBreakdown.TryGetValue(typeName, out var count);
                    _statistics.BotTypeBreakdown[typeName] = count + 1;
                }
            }

            // Update average processing time (rolling average)
            if (_statistics.TotalRequests == 1)
                _statistics.AverageProcessingTimeMs = result.ProcessingTimeMs;
            else
                _statistics.AverageProcessingTimeMs =
                    (_statistics.AverageProcessingTimeMs * (_statistics.TotalRequests - 1) + result.ProcessingTimeMs)
                    / _statistics.TotalRequests;
        }
    }
}