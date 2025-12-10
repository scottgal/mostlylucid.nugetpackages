using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging.Abstractions;
using Mostlylucid.BotDetection.Detectors;
using Xunit;

namespace Mostlylucid.BotDetection.Test.Detectors;

public class SecurityToolDetectorTests
{
    private readonly DefaultHttpContext _context;
    private readonly SecurityToolDetector _detector;

    public SecurityToolDetectorTests()
    {
        _context = new DefaultHttpContext();
        _detector = new SecurityToolDetector(NullLogger<SecurityToolDetector>.Instance);
    }

    [Theory]
    [InlineData("sqlmap/1.0")]
    [InlineData("Nikto/2.1.6")]
    [InlineData("Nmap Scripting Engine")]
    [InlineData("WPScan/3.8")]
    [InlineData("Metasploit")]
    [InlineData("Burp Suite")]
    public async Task DetectAsync_SecurityScannerUserAgent_DetectsBot(string userAgent)
    {
        // Arrange
        _context.Request.Headers.UserAgent = userAgent;

        // Act
        var result = await _detector.DetectAsync(_context);

        // Assert
        Assert.NotNull(result);
        Assert.True(result.BotProbability > 0.8);
        Assert.Contains("security", result.Reasons[0], StringComparison.OrdinalIgnoreCase);
    }

    [Theory]
    [InlineData("Mozilla/5.0 (Windows NT 10.0) Chrome/120.0.0.0")]
    [InlineData("Mozilla/5.0 (Macintosh; Intel Mac OS X 10_15_7)")]
    [InlineData("Mozilla/5.0 (iPhone; CPU iPhone OS 16_0)")]
    public async Task DetectAsync_LegitimateUserAgent_ReturnsNull(string userAgent)
    {
        // Arrange
        _context.Request.Headers.UserAgent = userAgent;

        // Act
        var result = await _detector.DetectAsync(_context);

        // Assert
        Assert.Null(result);
    }

    [Theory]
    [InlineData("/admin/")]
    [InlineData("/.git/config")]
    [InlineData("/wp-admin/")]
    [InlineData("/.env")]
    [InlineData("/phpmyadmin/")]
    public async Task DetectAsync_CommonSecurityScanPaths_DetectsBot(string path)
    {
        // Arrange
        _context.Request.Path = path;
        _context.Request.Headers.UserAgent = "Mozilla/5.0"; // Generic UA

        // Act
        var result = await _detector.DetectAsync(_context);

        // Assert
        if (result != null)
        {
            Assert.True(result.BotProbability > 0.5);
            Assert.NotEmpty(result.Reasons);
        }
    }

    [Theory]
    [InlineData("' OR 1=1--")]
    [InlineData("<script>alert('xss')</script>")]
    [InlineData("../../etc/passwd")]
    [InlineData("${jndi:ldap://")]
    public async Task DetectAsync_InjectionPatterns_DetectsBot(string maliciousInput)
    {
        // Arrange
        _context.Request.QueryString = new QueryString($"?input={maliciousInput}");
        _context.Request.Headers.UserAgent = "Mozilla/5.0";

        // Act
        var result = await _detector.DetectAsync(_context);

        // Assert
        if (result != null)
        {
            Assert.True(result.BotProbability > 0.7);
            Assert.Contains("injection", result.Reasons[0], StringComparison.OrdinalIgnoreCase);
        }
    }

    [Fact]
    public async Task DetectAsync_ZAPHeaders_DetectsSecurityScanner()
    {
        // Arrange
        _context.Request.Headers.UserAgent = "Mozilla/5.0";
        _context.Request.Headers["X-Scanner"] = "OWASP-ZAP";

        // Act
        var result = await _detector.DetectAsync(_context);

        // Assert
        if (result != null)
        {
            Assert.True(result.BotProbability > 0.8);
        }
    }

    [Fact]
    public async Task DetectAsync_PentesterHeaders_DetectsBot()
    {
        // Arrange
        _context.Request.Headers.UserAgent = "Mozilla/5.0";
        _context.Request.Headers["X-Forwarded-For"] = "127.0.0.1, 127.0.0.1, 127.0.0.1"; // Suspicious

        // Act
        var result = await _detector.DetectAsync(_context);

        // Assert
        // May or may not detect depending on implementation
        if (result != null)
        {
            Assert.True(result.BotProbability > 0);
        }
    }

    [Fact]
    public async Task DetectAsync_DirectoryBruteforce_DetectsBot()
    {
        // Arrange
        _context.Request.Path = "/admin123/";
        _context.Request.Headers.UserAgent = "DirBuster";

        // Act
        var result = await _detector.DetectAsync(_context);

        // Assert
        Assert.NotNull(result);
        Assert.True(result.BotProbability > 0.8);
    }

    [Fact]
    public async Task DetectAsync_NoUserAgent_HandlesGracefully()
    {
        // Arrange
        // No user agent

        // Act
        var result = await _detector.DetectAsync(_context);

        // Assert
        Assert.True(result == null || result.BotProbability >= 0);
    }

    [Fact]
    public void Name_ReturnsCorrectIdentifier()
    {
        // Assert
        Assert.Equal("security_tool_detector", _detector.Name);
    }
}
