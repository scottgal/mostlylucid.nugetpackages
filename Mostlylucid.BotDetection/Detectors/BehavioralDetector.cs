using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Mostlylucid.BotDetection.Models;

namespace Mostlylucid.BotDetection.Detectors;

/// <summary>
///     Detects bots based on behavioral patterns (rate limiting, request patterns)
/// </summary>
public class BehavioralDetector(
    ILogger<BehavioralDetector> logger,
    IOptions<BotDetectionOptions> options,
    IMemoryCache cache)
    : IDetector
{
    private readonly IMemoryCache _cache = cache;
    private readonly ILogger<BehavioralDetector> _logger = logger;
    private readonly BotDetectionOptions _options = options.Value;

    public string Name => "Behavioral Detector";

    public Task<DetectorResult> DetectAsync(HttpContext context, CancellationToken cancellationToken = default)
    {
        var result = new DetectorResult();
        var confidence = 0.0;
        var reasons = new List<DetectionReason>();

        var ipAddress = GetClientIp(context);
        if (string.IsNullOrEmpty(ipAddress)) return Task.FromResult(result);

        // Rate limiting check
        var requestCount = IncrementRequestCount(ipAddress);
        if (requestCount > _options.MaxRequestsPerMinute)
        {
            var excess = requestCount - _options.MaxRequestsPerMinute;
            var impact = Math.Min(0.3 + excess * 0.05, 0.9);
            confidence += impact;
            reasons.Add(new DetectionReason
            {
                Category = "Behavioral",
                Detail =
                    $"Excessive request rate: {requestCount} requests/min (limit: {_options.MaxRequestsPerMinute})",
                ConfidenceImpact = impact
            });
        }

        // Check request timing patterns
        var timingPattern = AnalyzeRequestTiming(ipAddress);
        if (timingPattern.IsSuspicious)
        {
            confidence += 0.3;
            reasons.Add(new DetectionReason
            {
                Category = "Behavioral",
                Detail = $"Suspicious request timing: {timingPattern.Description}",
                ConfidenceImpact = 0.3
            });
        }

        // Check for rapid sequential requests (no human delay)
        var lastRequestTime = GetLastRequestTime(ipAddress);
        var currentTime = DateTime.UtcNow;
        if (lastRequestTime.HasValue)
        {
            var timeSinceLastRequest = (currentTime - lastRequestTime.Value).TotalMilliseconds;
            if (timeSinceLastRequest < 100) // Less than 100ms between requests
            {
                confidence += 0.4;
                reasons.Add(new DetectionReason
                {
                    Category = "Behavioral",
                    Detail = $"Extremely fast requests: {timeSinceLastRequest:F0}ms between requests",
                    ConfidenceImpact = 0.4
                });
            }
        }

        UpdateLastRequestTime(ipAddress, currentTime);

        // Check for no referrer on non-initial requests
        if (!context.Request.Headers.ContainsKey("Referer") &&
            context.Request.Path != "/" &&
            requestCount > 1)
        {
            confidence += 0.15;
            reasons.Add(new DetectionReason
            {
                Category = "Behavioral",
                Detail = "No referrer on subsequent request",
                ConfidenceImpact = 0.15
            });
        }

        // Check for missing cookies (bots often don't maintain sessions)
        if (!context.Request.Cookies.Any() && requestCount > 2)
        {
            confidence += 0.25;
            reasons.Add(new DetectionReason
            {
                Category = "Behavioral",
                Detail = "No cookies maintained across multiple requests",
                ConfidenceImpact = 0.25
            });
        }

        result.Confidence = Math.Min(confidence, 1.0);
        result.Reasons = reasons;

        if (result.Confidence > 0.6) result.BotType = BotType.Scraper;

        return Task.FromResult(result);
    }

    private string? GetClientIp(HttpContext context)
    {
        return context.Request.Headers["X-Forwarded-For"].FirstOrDefault()?.Split(',')[0].Trim()
               ?? context.Connection.RemoteIpAddress?.ToString();
    }

    private int IncrementRequestCount(string ipAddress)
    {
        var key = $"bot_detect_count_{ipAddress}";
        var count = _cache.GetOrCreate(key, entry =>
        {
            entry.AbsoluteExpirationRelativeToNow = TimeSpan.FromMinutes(1);
            return 0;
        });

        count++;
        _cache.Set(key, count, TimeSpan.FromMinutes(1));
        return count;
    }

    private DateTime? GetLastRequestTime(string ipAddress)
    {
        var key = $"bot_detect_time_{ipAddress}";
        return _cache.Get<DateTime?>(key);
    }

    private void UpdateLastRequestTime(string ipAddress, DateTime time)
    {
        var key = $"bot_detect_time_{ipAddress}";
        _cache.Set(key, time, TimeSpan.FromMinutes(5));
    }

    private (bool IsSuspicious, string Description) AnalyzeRequestTiming(string ipAddress)
    {
        var key = $"bot_detect_timing_{ipAddress}";
        var timings = _cache.GetOrCreate(key, entry =>
        {
            entry.AbsoluteExpirationRelativeToNow = TimeSpan.FromMinutes(5);
            return new List<DateTime>();
        }) ?? new List<DateTime>();

        timings.Add(DateTime.UtcNow);

        // Keep only last 10 requests
        if (timings.Count > 10) timings = timings.Skip(timings.Count - 10).ToList();

        _cache.Set(key, timings, TimeSpan.FromMinutes(5));

        // Check if requests are too evenly spaced (bot-like)
        if (timings.Count >= 5)
        {
            var intervals = new List<double>();
            for (var i = 1; i < timings.Count; i++) intervals.Add((timings[i] - timings[i - 1]).TotalSeconds);

            // Calculate standard deviation
            var mean = intervals.Average();
            var variance = intervals.Average(x => Math.Pow(x - mean, 2));
            var stdDev = Math.Sqrt(variance);

            // Very low standard deviation means requests are too regular
            if (stdDev < 0.5 && mean < 5) return (true, $"Too regular interval: {mean:F2}s ± {stdDev:F2}s");
        }

        return (false, string.Empty);
    }
}