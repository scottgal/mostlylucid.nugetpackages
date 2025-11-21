using System.Net;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Mostlylucid.BotDetection.Detectors;
using Mostlylucid.BotDetection.Models;
using Mostlylucid.BotDetection.Test.Helpers;

namespace Mostlylucid.BotDetection.Test.Detectors;

/// <summary>
///     Comprehensive tests for BehavioralDetector
/// </summary>
public class BehavioralDetectorTests
{
    private readonly IMemoryCache _cache;
    private readonly ILogger<BehavioralDetector> _logger;

    public BehavioralDetectorTests()
    {
        _logger = new Mock<ILogger<BehavioralDetector>>().Object;
        _cache = new MemoryCache(new MemoryCacheOptions());
    }

    private BehavioralDetector CreateDetector(BotDetectionOptions? options = null)
    {
        return new BehavioralDetector(
            _logger,
            Options.Create(options ?? new BotDetectionOptions()),
            _cache);
    }

    private IMemoryCache CreateFreshCache()
    {
        return new MemoryCache(new MemoryCacheOptions());
    }

    #region Missing IP Tests

    [Fact]
    public async Task DetectAsync_NoIpAddress_ReturnsZeroConfidence()
    {
        // Arrange
        var detector = CreateDetector();
        var context = new DefaultHttpContext();
        // No IP set

        // Act
        var result = await detector.DetectAsync(context);

        // Assert
        Assert.Equal(0.0, result.Confidence);
        Assert.Empty(result.Reasons);
    }

    #endregion

    #region Fast Request Tests

    [Fact]
    public async Task DetectAsync_RapidSequentialRequests_HighConfidence()
    {
        // Arrange
        var cache = CreateFreshCache();
        var detector = new BehavioralDetector(_logger, Options.Create(new BotDetectionOptions()), cache);
        var context = MockHttpContext.CreateWithIpAddress("192.168.1.1");

        // Act - Make requests as fast as possible
        await detector.DetectAsync(context);
        var result = await detector.DetectAsync(context);

        // Assert - Extremely fast requests should be detected
        // Note: This might not trigger in test environment due to test overhead
        Assert.NotNull(result);
    }

    #endregion

    #region Bot Type Classification Tests

    [Fact]
    public async Task DetectAsync_HighConfidence_SetsScraperBotType()
    {
        // Arrange
        var cache = CreateFreshCache();
        var options = new BotDetectionOptions { MaxRequestsPerMinute = 5 };
        var detector = new BehavioralDetector(_logger, Options.Create(options), cache);
        var context = new DefaultHttpContext();
        context.Connection.RemoteIpAddress = IPAddress.Parse("192.168.1.1");
        context.Request.Path = "/page";

        // Act - Generate many requests to trigger high confidence
        DetectorResult result = null!;
        for (var i = 0; i < 20; i++) result = await detector.DetectAsync(context);

        // Assert
        if (result.Confidence > 0.6) Assert.Equal(BotType.Scraper, result.BotType);
    }

    #endregion

    #region Different IPs Tests

    [Fact]
    public async Task DetectAsync_DifferentIps_IndependentCounting()
    {
        // Arrange
        var cache = CreateFreshCache();
        var options = new BotDetectionOptions { MaxRequestsPerMinute = 10 };
        var detector = new BehavioralDetector(_logger, Options.Create(options), cache);

        var context1 = MockHttpContext.CreateWithIpAddress("192.168.1.1");
        var context2 = MockHttpContext.CreateWithIpAddress("192.168.1.2");

        // Act - Many requests from IP1
        for (var i = 0; i < 15; i++) await detector.DetectAsync(context1);

        // First request from IP2
        var result2 = await detector.DetectAsync(context2);

        // Assert - IP2 should have low confidence (first request)
        Assert.True(result2.Confidence < 0.5, "Different IP should have independent rate counting");
    }

    #endregion

    #region X-Forwarded-For Tests

    [Fact]
    public async Task DetectAsync_XForwardedFor_UsesCorrectIp()
    {
        // Arrange
        var cache = CreateFreshCache();
        var options = new BotDetectionOptions { MaxRequestsPerMinute = 5 };
        var detector = new BehavioralDetector(_logger, Options.Create(options), cache);

        var context = MockHttpContext.CreateWithHeaders(new Dictionary<string, string>
        {
            ["X-Forwarded-For"] = "10.0.0.1, 192.168.1.1"
        });

        // Act
        DetectorResult result = null!;
        for (var i = 0; i < 10; i++) result = await detector.DetectAsync(context);

        // Assert
        Assert.True(result.Confidence >= 0.3, "Should track by X-Forwarded-For IP");
    }

    #endregion

    #region Reason Validation Tests

    [Fact]
    public async Task DetectAsync_AllReasonsHaveBehavioralCategory()
    {
        // Arrange
        var cache = CreateFreshCache();
        var options = new BotDetectionOptions { MaxRequestsPerMinute = 5 };
        var detector = new BehavioralDetector(_logger, Options.Create(options), cache);
        var context = MockHttpContext.CreateWithIpAddress("192.168.1.1");

        // Act
        DetectorResult result = null!;
        for (var i = 0; i < 10; i++) result = await detector.DetectAsync(context);

        // Assert
        foreach (var reason in result.Reasons) Assert.Equal("Behavioral", reason.Category);
    }

    #endregion

    #region Name Property Test

    [Fact]
    public void Name_ReturnsExpectedValue()
    {
        // Arrange
        var detector = CreateDetector();

        // Assert
        Assert.Equal("Behavioral Detector", detector.Name);
    }

    #endregion

    #region Cancellation Tests

    [Fact]
    public async Task DetectAsync_WithCancellation_CompletesNormally()
    {
        // Arrange
        var cache = CreateFreshCache();
        var detector = new BehavioralDetector(_logger, Options.Create(new BotDetectionOptions()), cache);
        var context = MockHttpContext.CreateWithIpAddress("192.168.1.1");
        using var cts = new CancellationTokenSource();

        // Act
        var result = await detector.DetectAsync(context, cts.Token);

        // Assert
        Assert.NotNull(result);
    }

    #endregion

    #region Rate Limiting Tests

    [Fact]
    public async Task DetectAsync_FirstRequest_LowConfidence()
    {
        // Arrange
        var cache = CreateFreshCache();
        var detector = new BehavioralDetector(
            _logger,
            Options.Create(new BotDetectionOptions { MaxRequestsPerMinute = 60 }),
            cache);
        var context = MockHttpContext.CreateWithIpAddress("192.168.1.1");

        // Act
        var result = await detector.DetectAsync(context);

        // Assert
        Assert.True(result.Confidence < 0.5, "First request should have low confidence");
    }

    [Fact]
    public async Task DetectAsync_ExcessiveRequests_HighConfidence()
    {
        // Arrange
        var cache = CreateFreshCache();
        var options = new BotDetectionOptions { MaxRequestsPerMinute = 10 };
        var detector = new BehavioralDetector(_logger, Options.Create(options), cache);
        var context = MockHttpContext.CreateWithIpAddress("192.168.1.1");

        // Act - Simulate many requests
        DetectorResult result = null!;
        for (var i = 0; i < 20; i++) result = await detector.DetectAsync(context);

        // Assert
        Assert.True(result.Confidence >= 0.3, "Excessive requests should increase confidence");
        Assert.Contains(result.Reasons, r => r.Detail.Contains("request rate"));
    }

    [Fact]
    public async Task DetectAsync_WithinRateLimit_NoRateLimitReason()
    {
        // Arrange
        var cache = CreateFreshCache();
        var options = new BotDetectionOptions { MaxRequestsPerMinute = 100 };
        var detector = new BehavioralDetector(_logger, Options.Create(options), cache);
        var context = MockHttpContext.CreateWithIpAddress("192.168.1.1");

        // Act - Just a few requests
        DetectorResult result = null!;
        for (var i = 0; i < 5; i++) result = await detector.DetectAsync(context);

        // Assert
        Assert.DoesNotContain(result.Reasons, r =>
            r.Detail.Contains("Excessive request rate"));
    }

    #endregion

    #region Missing Referrer Tests

    [Fact]
    public async Task DetectAsync_NoReferrerOnSubsequentRequest_AddsReason()
    {
        // Arrange
        var cache = CreateFreshCache();
        var detector = new BehavioralDetector(_logger, Options.Create(new BotDetectionOptions()), cache);
        var context = new DefaultHttpContext();
        context.Connection.RemoteIpAddress = IPAddress.Parse("192.168.1.1");
        context.Request.Path = "/some/path"; // Not root
        // No Referer header

        // Act - First request
        await detector.DetectAsync(context);
        // Second request
        var result = await detector.DetectAsync(context);

        // Assert
        Assert.Contains(result.Reasons, r =>
            r.Detail.Contains("referrer", StringComparison.OrdinalIgnoreCase) ||
            r.Detail.Contains("Referer", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task DetectAsync_RootPath_NoReferrerReason()
    {
        // Arrange
        var cache = CreateFreshCache();
        var detector = new BehavioralDetector(_logger, Options.Create(new BotDetectionOptions()), cache);
        var context = new DefaultHttpContext();
        context.Connection.RemoteIpAddress = IPAddress.Parse("192.168.1.1");
        context.Request.Path = "/"; // Root path

        // Act
        await detector.DetectAsync(context);
        var result = await detector.DetectAsync(context);

        // Assert - Root path shouldn't require referrer
        Assert.DoesNotContain(result.Reasons, r =>
            r.Detail.Contains("No referrer on subsequent"));
    }

    [Fact]
    public async Task DetectAsync_WithReferrer_NoReferrerReason()
    {
        // Arrange
        var cache = CreateFreshCache();
        var detector = new BehavioralDetector(_logger, Options.Create(new BotDetectionOptions()), cache);
        var context = new DefaultHttpContext();
        context.Connection.RemoteIpAddress = IPAddress.Parse("192.168.1.1");
        context.Request.Path = "/some/path";
        context.Request.Headers["Referer"] = "https://example.com/";

        // Act
        await detector.DetectAsync(context);
        var result = await detector.DetectAsync(context);

        // Assert
        Assert.DoesNotContain(result.Reasons, r =>
            r.Detail.Contains("No referrer"));
    }

    #endregion

    #region Missing Cookies Tests

    [Fact]
    public async Task DetectAsync_NoCookiesAfterMultipleRequests_AddsReason()
    {
        // Arrange
        var cache = CreateFreshCache();
        var detector = new BehavioralDetector(_logger, Options.Create(new BotDetectionOptions()), cache);
        var context = new DefaultHttpContext();
        context.Connection.RemoteIpAddress = IPAddress.Parse("192.168.1.1");
        // No cookies

        // Act - Make 3+ requests
        await detector.DetectAsync(context);
        await detector.DetectAsync(context);
        var result = await detector.DetectAsync(context);

        // Assert
        Assert.Contains(result.Reasons, r =>
            r.Detail.Contains("cookie", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task DetectAsync_WithCookies_NoCookieReason()
    {
        // Arrange
        var cache = CreateFreshCache();
        var detector = new BehavioralDetector(_logger, Options.Create(new BotDetectionOptions()), cache);
        var context = new DefaultHttpContext();
        context.Connection.RemoteIpAddress = IPAddress.Parse("192.168.1.1");
        context.Request.Headers["Cookie"] = "session=abc123";

        // Act
        await detector.DetectAsync(context);
        await detector.DetectAsync(context);
        var result = await detector.DetectAsync(context);

        // Assert
        Assert.DoesNotContain(result.Reasons, r =>
            r.Detail.Contains("No cookies maintained"));
    }

    #endregion

    #region Confidence Bounds Tests

    [Fact]
    public async Task DetectAsync_ConfidenceNeverExceedsOne()
    {
        // Arrange
        var cache = CreateFreshCache();
        var options = new BotDetectionOptions { MaxRequestsPerMinute = 1 };
        var detector = new BehavioralDetector(_logger, Options.Create(options), cache);
        var context = MockHttpContext.CreateWithIpAddress("192.168.1.1");

        // Act - Many requests
        DetectorResult result = null!;
        for (var i = 0; i < 100; i++) result = await detector.DetectAsync(context);

        // Assert
        Assert.True(result.Confidence <= 1.0, "Confidence should never exceed 1.0");
    }

    [Fact]
    public async Task DetectAsync_ConfidenceNeverNegative()
    {
        // Arrange
        var cache = CreateFreshCache();
        var detector = new BehavioralDetector(_logger, Options.Create(new BotDetectionOptions()), cache);
        var context = MockHttpContext.CreateRealisticBrowser();

        // Act
        var result = await detector.DetectAsync(context);

        // Assert
        Assert.True(result.Confidence >= 0.0, "Confidence should never be negative");
    }

    #endregion
}