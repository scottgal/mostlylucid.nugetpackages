using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging.Abstractions;
using Mostlylucid.BotDetection.Detectors;
using Mostlylucid.BotDetection.Models;
using Xunit;

namespace Mostlylucid.BotDetection.Test.Detectors;

public class VersionAgeDetectorTests
{
    private readonly DefaultHttpContext _context;
    private readonly VersionAgeDetector _detector;

    public VersionAgeDetectorTests()
    {
        _context = new DefaultHttpContext();
        _detector = new VersionAgeDetector(NullLogger<VersionAgeDetector>.Instance);
    }

    [Theory]
    [InlineData("Chrome/120.0.0.0", false)] // Recent version
    [InlineData("Chrome/50.0.0.0", true)]   // Old version (2016)
    [InlineData("Firefox/120.0", false)]    // Recent version
    [InlineData("Firefox/40.0", true)]      // Old version (2015)
    public async Task DetectAsync_IdentifiesOldBrowserVersions(string userAgent, bool shouldDetect)
    {
        // Arrange
        _context.Request.Headers.UserAgent = userAgent;

        // Act
        var result = await _detector.DetectAsync(_context);

        // Assert
        if (shouldDetect)
        {
            Assert.NotNull(result);
            Assert.True(result.BotProbability > 0.3);
            Assert.Contains("old", result.Reasons[0], StringComparison.OrdinalIgnoreCase);
        }
        else
        {
            Assert.Null(result);
        }
    }

    [Theory]
    [InlineData("Mozilla/5.0 (Windows NT 10.0) AppleWebKit/537.36 Chrome/120.0.0.0")]
    [InlineData("Mozilla/5.0 (Macintosh; Intel Mac OS X 10_15_7) AppleWebKit/537.36")]
    public async Task DetectAsync_AllowsRecentVersions(string userAgent)
    {
        // Arrange
        _context.Request.Headers.UserAgent = userAgent;

        // Act
        var result = await _detector.DetectAsync(_context);

        // Assert
        Assert.Null(result);
    }

    [Fact]
    public async Task DetectAsync_HandlesNoUserAgent()
    {
        // Arrange
        // No user agent set

        // Act
        var result = await _detector.DetectAsync(_context);

        // Assert
        Assert.Null(result);
    }

    [Theory]
    [InlineData("IE/6.0")]     // Very old IE
    [InlineData("IE/8.0")]     // Old IE
    [InlineData("MSIE 9.0")]   // Old IE format
    public async Task DetectAsync_DetectsVeryOldInternetExplorer(string userAgent)
    {
        // Arrange
        _context.Request.Headers.UserAgent = userAgent;

        // Act
        var result = await _detector.DetectAsync(_context);

        // Assert
        if (result != null)
        {
            Assert.True(result.BotProbability > 0.5);
        }
    }

    [Fact]
    public void Name_ReturnsCorrectIdentifier()
    {
        // Assert
        Assert.Equal("version_age_detector", _detector.Name);
    }
}
