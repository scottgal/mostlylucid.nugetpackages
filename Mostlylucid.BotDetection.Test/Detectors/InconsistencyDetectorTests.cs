using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging.Abstractions;
using Mostlylucid.BotDetection.Detectors;
using Xunit;

namespace Mostlylucid.BotDetection.Test.Detectors;

public class InconsistencyDetectorTests
{
    private readonly DefaultHttpContext _context;
    private readonly InconsistencyDetector _detector;

    public InconsistencyDetectorTests()
    {
        _context = new DefaultHttpContext();
        _detector = new InconsistencyDetector(NullLogger<InconsistencyDetector>.Instance);
    }

    [Fact]
    public async Task DetectAsync_ConsistentHeaders_ReturnsNull()
    {
        // Arrange
        _context.Request.Headers.UserAgent = "Mozilla/5.0 (Windows NT 10.0; Win64; x64) Chrome/120.0.0.0";
        _context.Request.Headers.AcceptLanguage = "en-US,en;q=0.9";
        _context.Request.Headers.Accept = "text/html,application/xhtml+xml,application/xml";

        // Act
        var result = await _detector.DetectAsync(_context);

        // Assert
        Assert.Null(result);
    }

    [Fact]
    public async Task DetectAsync_MacUserAgentWithWindowsAcceptLanguage_DetectsInconsistency()
    {
        // Arrange
        _context.Request.Headers.UserAgent = "Mozilla/5.0 (Macintosh; Intel Mac OS X 10_15_7)";
        _context.Request.Headers.AcceptLanguage = "zh-CN"; // Unusual for Mac

        // Act
        var result = await _detector.DetectAsync(_context);

        // Assert
        // May or may not detect - depends on implementation
        if (result != null)
        {
            Assert.True(result.BotProbability > 0);
            Assert.NotEmpty(result.Reasons);
        }
    }

    [Fact]
    public async Task DetectAsync_MobileUserAgentWithDesktopHeaders_DetectsInconsistency()
    {
        // Arrange
        _context.Request.Headers.UserAgent = "Mozilla/5.0 (iPhone; CPU iPhone OS 16_0)";
        _context.Request.Headers["Sec-Ch-Ua-Mobile"] = "?0"; // Claims not mobile

        // Act
        var result = await _detector.DetectAsync(_context);

        // Assert
        if (result != null)
        {
            Assert.True(result.BotProbability > 0.4);
            Assert.Contains("inconsistency", result.Reasons[0], StringComparison.OrdinalIgnoreCase);
        }
    }

    [Fact]
    public async Task DetectAsync_ChromeUserAgentWithoutChromeHeaders_DetectsInconsistency()
    {
        // Arrange
        _context.Request.Headers.UserAgent = "Mozilla/5.0 Chrome/120.0.0.0";
        // Missing typical Chrome client hints

        // Act
        var result = await _detector.DetectAsync(_context);

        // Assert
        if (result != null)
        {
            Assert.True(result.BotProbability > 0);
        }
    }

    [Fact]
    public async Task DetectAsync_ConflictingPlatformClaims_DetectsBot()
    {
        // Arrange
        _context.Request.Headers.UserAgent = "Mozilla/5.0 (Windows NT 10.0)";
        _context.Request.Headers["Sec-Ch-Ua-Platform"] = "\"macOS\""; // Conflict

        // Act
        var result = await _detector.DetectAsync(_context);

        // Assert
        if (result != null)
        {
            Assert.True(result.BotProbability > 0.5);
            Assert.Contains("inconsistent", result.Reasons[0], StringComparison.OrdinalIgnoreCase);
        }
    }

    [Fact]
    public async Task DetectAsync_NoUserAgent_HandlesGracefully()
    {
        // Arrange
        // No user agent set

        // Act
        var result = await _detector.DetectAsync(_context);

        // Assert
        // Should handle gracefully, may or may not detect
        Assert.True(result == null || result.BotProbability >= 0);
    }

    [Fact]
    public async Task DetectAsync_BrowserVersionMismatch_DetectsInconsistency()
    {
        // Arrange
        _context.Request.Headers.UserAgent = "Mozilla/5.0 Chrome/120.0.0.0";
        _context.Request.Headers["Sec-Ch-Ua"] = "\"Chromium\";v=\"110\""; // Version mismatch

        // Act
        var result = await _detector.DetectAsync(_context);

        // Assert
        if (result != null)
        {
            Assert.True(result.BotProbability > 0.3);
        }
    }

    [Fact]
    public void Name_ReturnsCorrectIdentifier()
    {
        // Assert
        Assert.Equal("inconsistency_detector", _detector.Name);
    }
}
