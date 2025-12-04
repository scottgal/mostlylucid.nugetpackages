using System.Net;
using System.Text.Json;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Moq;
using Moq.Protected;
using Mostlylucid.BotDetection.Data;
using Mostlylucid.BotDetection.Models;
using Xunit;

namespace Mostlylucid.BotDetection.Orchestration.Tests.Unit;

/// <summary>
///     Unit tests for BotListFetcher.GetSecurityToolPatternsAsync.
///     Tests fetching security tool patterns from external sources.
/// </summary>
public class BotListFetcherSecurityToolsTests : IDisposable
{
    private readonly Mock<ILogger<BotListFetcher>> _loggerMock;
    private readonly IMemoryCache _cache;
    private readonly BotDetectionOptions _options;

    public BotListFetcherSecurityToolsTests()
    {
        _loggerMock = new Mock<ILogger<BotListFetcher>>();
        _cache = new MemoryCache(new MemoryCacheOptions());
        _options = new BotDetectionOptions
        {
            DataSources = new DataSourcesOptions
            {
                ScannerUserAgents = new DataSourceConfig
                {
                    Enabled = true,
                    Url = "https://example.com/scanners.json"
                },
                CoreRuleSetScanners = new DataSourceConfig
                {
                    Enabled = true,
                    Url = "https://example.com/crs-scanners.txt"
                }
            }
        };
    }

    public void Dispose()
    {
        _cache.Dispose();
    }

    private (BotListFetcher Fetcher, Mock<HttpMessageHandler> HandlerMock) CreateFetcher()
    {
        var handlerMock = new Mock<HttpMessageHandler>();
        var httpClient = new HttpClient(handlerMock.Object);

        var httpClientFactory = new Mock<IHttpClientFactory>();
        httpClientFactory.Setup(f => f.CreateClient(It.IsAny<string>()))
            .Returns(httpClient);

        var fetcher = new BotListFetcher(
            httpClientFactory.Object,
            _cache,
            _loggerMock.Object,
            Options.Create(_options));

        return (fetcher, handlerMock);
    }

    // ==========================================
    // digininja/scanner_user_agents JSON Tests
    // ==========================================

    [Fact]
    public async Task GetSecurityToolPatternsAsync_ParsesDigininjaJson_Correctly()
    {
        // Arrange
        var (fetcher, handlerMock) = CreateFetcher();
        var jsonContent = JsonSerializer.Serialize(new[]
        {
            new { pattern = "sqlmap", name = "SQLMap", category = "SqlInjection" },
            new { pattern = "nikto", name = "Nikto", category = "VulnerabilityScanner" },
            new { pattern = "nmap", name = "Nmap", category = "PortScanner" }
        });

        SetupHttpResponse(handlerMock, _options.DataSources.ScannerUserAgents.Url!, jsonContent);
        SetupHttpResponse(handlerMock, _options.DataSources.CoreRuleSetScanners.Url!, "");

        // Act
        var patterns = await fetcher.GetSecurityToolPatternsAsync();

        // Assert
        Assert.Equal(3, patterns.Count);

        var sqlmap = patterns.First(p => p.Pattern == "sqlmap");
        Assert.Equal("SQLMap", sqlmap.Name);
        Assert.Equal("SqlInjection", sqlmap.Category);
        Assert.Equal("digininja/scanner_user_agents", sqlmap.Source);
        Assert.False(sqlmap.IsRegex);
    }

    [Fact]
    public async Task GetSecurityToolPatternsAsync_HandlesRegexPatterns()
    {
        // Arrange
        var (fetcher, handlerMock) = CreateFetcher();
        var jsonContent = JsonSerializer.Serialize(new[]
        {
            new { pattern = @"sqlmap[\s/]?\d", name = "SQLMap", category = "SqlInjection" },
            new { pattern = "simple-pattern", name = "Simple", category = "Other" }
        });

        SetupHttpResponse(handlerMock, _options.DataSources.ScannerUserAgents.Url!, jsonContent);
        SetupHttpResponse(handlerMock, _options.DataSources.CoreRuleSetScanners.Url!, "");

        // Act
        var patterns = await fetcher.GetSecurityToolPatternsAsync();

        // Assert
        var regexPattern = patterns.First(p => p.Pattern.Contains("sqlmap"));
        Assert.True(regexPattern.IsRegex);

        var simplePattern = patterns.First(p => p.Pattern == "simple-pattern");
        Assert.False(simplePattern.IsRegex);
    }

    [Fact]
    public async Task GetSecurityToolPatternsAsync_InfersToolNameFromPattern()
    {
        // Arrange
        var (fetcher, handlerMock) = CreateFetcher();
        var jsonContent = JsonSerializer.Serialize(new[]
        {
            new { pattern = "sqlmap/1.5", name = (string?)null, category = (string?)null }
        });

        SetupHttpResponse(handlerMock, _options.DataSources.ScannerUserAgents.Url!, jsonContent);
        SetupHttpResponse(handlerMock, _options.DataSources.CoreRuleSetScanners.Url!, "");

        // Act
        var patterns = await fetcher.GetSecurityToolPatternsAsync();

        // Assert
        var pattern = patterns.First();
        Assert.Equal("Sqlmap", pattern.Name);
    }

    // ==========================================
    // OWASP CoreRuleSet Text Format Tests
    // ==========================================

    [Fact]
    public async Task GetSecurityToolPatternsAsync_ParsesCoreRuleSetText_Correctly()
    {
        // Arrange
        var (fetcher, handlerMock) = CreateFetcher();

        SetupHttpResponse(handlerMock, _options.DataSources.ScannerUserAgents.Url!, "[]");
        SetupHttpResponse(handlerMock, _options.DataSources.CoreRuleSetScanners.Url!, @"
# This is a comment
gobuster
feroxbuster
ffuf

# Another comment
wpscan
");

        // Act
        var patterns = await fetcher.GetSecurityToolPatternsAsync();

        // Assert
        Assert.Equal(4, patterns.Count);
        Assert.Contains(patterns, p => p.Pattern == "gobuster");
        Assert.Contains(patterns, p => p.Pattern == "feroxbuster");
        Assert.Contains(patterns, p => p.Pattern == "ffuf");
        Assert.Contains(patterns, p => p.Pattern == "wpscan");

        // All should have OWASP source
        Assert.All(patterns, p => Assert.Equal("OWASP/CoreRuleSet", p.Source));
    }

    [Fact]
    public async Task GetSecurityToolPatternsAsync_SkipsCommentsAndEmptyLines()
    {
        // Arrange
        var (fetcher, handlerMock) = CreateFetcher();

        SetupHttpResponse(handlerMock, _options.DataSources.ScannerUserAgents.Url!, "[]");
        SetupHttpResponse(handlerMock, _options.DataSources.CoreRuleSetScanners.Url!, @"
# Comment line
   # Indented comment
pattern1


pattern2
");

        // Act
        var patterns = await fetcher.GetSecurityToolPatternsAsync();

        // Assert
        Assert.Equal(2, patterns.Count);
        Assert.Contains(patterns, p => p.Pattern == "pattern1");
        Assert.Contains(patterns, p => p.Pattern == "pattern2");
    }

    // ==========================================
    // Deduplication Tests
    // ==========================================

    [Fact]
    public async Task GetSecurityToolPatternsAsync_DeduplicatesPatterns()
    {
        // Arrange
        var (fetcher, handlerMock) = CreateFetcher();
        var jsonContent = JsonSerializer.Serialize(new[]
        {
            new { pattern = "sqlmap", name = "SQLMap", category = "SqlInjection" },
            new { pattern = "SQLMAP", name = "SQLMap Upper", category = "SqlInjection" } // Duplicate (case-insensitive)
        });

        SetupHttpResponse(handlerMock, _options.DataSources.ScannerUserAgents.Url!, jsonContent);
        SetupHttpResponse(handlerMock, _options.DataSources.CoreRuleSetScanners.Url!, "sqlmap\nSQLMAP"); // More duplicates

        // Act
        var patterns = await fetcher.GetSecurityToolPatternsAsync();

        // Assert - Should have only one sqlmap pattern (first one wins)
        Assert.Single(patterns);
        Assert.Equal("sqlmap", patterns[0].Pattern);
        Assert.Equal("digininja/scanner_user_agents", patterns[0].Source); // First source wins
    }

    // ==========================================
    // Caching Tests
    // ==========================================

    [Fact]
    public async Task GetSecurityToolPatternsAsync_UsesCachedResults()
    {
        // Arrange
        var (fetcher, handlerMock) = CreateFetcher();
        var jsonContent = JsonSerializer.Serialize(new[]
        {
            new { pattern = "sqlmap", name = "SQLMap", category = "SqlInjection" }
        });

        SetupHttpResponse(handlerMock, _options.DataSources.ScannerUserAgents.Url!, jsonContent);
        SetupHttpResponse(handlerMock, _options.DataSources.CoreRuleSetScanners.Url!, "");

        // Act - Call twice
        var result1 = await fetcher.GetSecurityToolPatternsAsync();
        var result2 = await fetcher.GetSecurityToolPatternsAsync();

        // Assert - Should return same instance (cached)
        Assert.Same(result1, result2);

        // HTTP should only be called once for each URL
        handlerMock.Protected().Verify(
            "SendAsync",
            Times.Exactly(2), // Once for each URL
            ItExpr.IsAny<HttpRequestMessage>(),
            ItExpr.IsAny<CancellationToken>());
    }

    // ==========================================
    // Error Handling Tests
    // ==========================================

    [Fact]
    public async Task GetSecurityToolPatternsAsync_ReturnsFromOtherSource_WhenOneSourceFails()
    {
        // Arrange
        var (fetcher, handlerMock) = CreateFetcher();

        // Scanner user agents fails
        SetupHttpResponse(handlerMock, _options.DataSources.ScannerUserAgents.Url!, HttpStatusCode.InternalServerError);
        // CoreRuleSet succeeds
        SetupHttpResponse(handlerMock, _options.DataSources.CoreRuleSetScanners.Url!, "gobuster\nnikto");

        // Act
        var patterns = await fetcher.GetSecurityToolPatternsAsync();

        // Assert - Should have patterns from CRS
        Assert.Equal(2, patterns.Count);
        Assert.All(patterns, p => Assert.Equal("OWASP/CoreRuleSet", p.Source));
    }

    [Fact]
    public async Task GetSecurityToolPatternsAsync_ReturnsFallback_WhenAllSourcesFail()
    {
        // Arrange
        var (fetcher, handlerMock) = CreateFetcher();

        SetupHttpResponse(handlerMock, _options.DataSources.ScannerUserAgents.Url!, HttpStatusCode.InternalServerError);
        SetupHttpResponse(handlerMock, _options.DataSources.CoreRuleSetScanners.Url!, HttpStatusCode.InternalServerError);

        // Act
        var patterns = await fetcher.GetSecurityToolPatternsAsync();

        // Assert - Should have fallback patterns
        Assert.NotEmpty(patterns);
        Assert.All(patterns, p => Assert.Equal("fallback", p.Source));
        Assert.Contains(patterns, p => p.Pattern == "sqlmap");
        Assert.Contains(patterns, p => p.Pattern == "nikto");
    }

    [Fact]
    public async Task GetSecurityToolPatternsAsync_HandlesInvalidJson()
    {
        // Arrange
        var (fetcher, handlerMock) = CreateFetcher();

        SetupHttpResponse(handlerMock, _options.DataSources.ScannerUserAgents.Url!, "not valid json{{{");
        SetupHttpResponse(handlerMock, _options.DataSources.CoreRuleSetScanners.Url!, "gobuster");

        // Act
        var patterns = await fetcher.GetSecurityToolPatternsAsync();

        // Assert - Should have patterns from CRS only
        Assert.Single(patterns);
        Assert.Equal("gobuster", patterns[0].Pattern);
    }

    // ==========================================
    // Configuration Tests
    // ==========================================

    [Fact]
    public async Task GetSecurityToolPatternsAsync_SkipsDisabledSources()
    {
        // Arrange
        _options.DataSources.ScannerUserAgents.Enabled = false;
        var (fetcher, handlerMock) = CreateFetcher();

        // Only CRS should be called
        SetupHttpResponse(handlerMock, _options.DataSources.CoreRuleSetScanners.Url!, "gobuster");

        // Act
        var patterns = await fetcher.GetSecurityToolPatternsAsync();

        // Assert
        Assert.Single(patterns);
        Assert.Equal("OWASP/CoreRuleSet", patterns[0].Source);

        // Scanner user agents should not be called
        handlerMock.Protected().Verify(
            "SendAsync",
            Times.Never(),
            ItExpr.Is<HttpRequestMessage>(r => r.RequestUri!.ToString() == _options.DataSources.ScannerUserAgents.Url),
            ItExpr.IsAny<CancellationToken>());
    }

    [Fact]
    public async Task GetSecurityToolPatternsAsync_SkipsSourcesWithEmptyUrl()
    {
        // Arrange
        _options.DataSources.ScannerUserAgents.Url = "";
        var (fetcher, handlerMock) = CreateFetcher();

        SetupHttpResponse(handlerMock, _options.DataSources.CoreRuleSetScanners.Url!, "gobuster");

        // Act
        var patterns = await fetcher.GetSecurityToolPatternsAsync();

        // Assert
        Assert.Single(patterns);
        Assert.Equal("OWASP/CoreRuleSet", patterns[0].Source);
    }

    [Fact]
    public async Task GetSecurityToolPatternsAsync_ReturnsFallback_WhenBothSourcesDisabled()
    {
        // Arrange
        _options.DataSources.ScannerUserAgents.Enabled = false;
        _options.DataSources.CoreRuleSetScanners.Enabled = false;
        var (fetcher, handlerMock) = CreateFetcher();

        // Act
        var patterns = await fetcher.GetSecurityToolPatternsAsync();

        // Assert - Should return fallback patterns
        Assert.NotEmpty(patterns);
        Assert.All(patterns, p => Assert.Equal("fallback", p.Source));
    }

    // ==========================================
    // Pattern Content Tests
    // ==========================================

    [Fact]
    public async Task GetSecurityToolPatternsAsync_TrimsWhitespace()
    {
        // Arrange
        var (fetcher, handlerMock) = CreateFetcher();
        var jsonContent = JsonSerializer.Serialize(new[]
        {
            new { pattern = "  sqlmap  ", name = "SQLMap", category = "SqlInjection" }
        });

        SetupHttpResponse(handlerMock, _options.DataSources.ScannerUserAgents.Url!, jsonContent);
        SetupHttpResponse(handlerMock, _options.DataSources.CoreRuleSetScanners.Url!, "  gobuster  ");

        // Act
        var patterns = await fetcher.GetSecurityToolPatternsAsync();

        // Assert - Patterns should be trimmed
        Assert.Equal(2, patterns.Count);
        Assert.Contains(patterns, p => p.Pattern == "sqlmap");
        Assert.Contains(patterns, p => p.Pattern == "gobuster");
    }

    [Fact]
    public async Task GetSecurityToolPatternsAsync_SkipsEmptyPatterns()
    {
        // Arrange
        var (fetcher, handlerMock) = CreateFetcher();
        var jsonContent = JsonSerializer.Serialize(new object[]
        {
            new { pattern = "", name = "Empty", category = "None" },
            new { pattern = "sqlmap", name = "SQLMap", category = "SqlInjection" },
            new { pattern = (string?)null, name = "Null", category = "None" }
        });

        SetupHttpResponse(handlerMock, _options.DataSources.ScannerUserAgents.Url!, jsonContent);
        SetupHttpResponse(handlerMock, _options.DataSources.CoreRuleSetScanners.Url!, "");

        // Act
        var patterns = await fetcher.GetSecurityToolPatternsAsync();

        // Assert - Should only have the non-empty pattern
        Assert.Single(patterns);
        Assert.Equal("sqlmap", patterns[0].Pattern);
    }

    // ==========================================
    // Category Inference Tests
    // ==========================================

    [Theory]
    [InlineData("sqlmap", "Sqlmap")]
    [InlineData("nikto/2.1.6", "Nikto")]
    [InlineData("nmap scripting engine", "Nmap")]
    [InlineData("gobuster/3.1", "Gobuster")]
    [InlineData("hydra-http", "Hydra")]
    [InlineData("metasploit framework", "Metasploit")]
    [InlineData("wpscan v3.8", "Wpscan")]
    [InlineData("unknown-tool", "unknown")] // Split on '-' returns first word
    public async Task GetSecurityToolPatternsAsync_InfersToolNameCorrectly(string pattern, string expectedName)
    {
        // Arrange
        var (fetcher, handlerMock) = CreateFetcher();
        var jsonContent = JsonSerializer.Serialize(new[]
        {
            new { pattern, name = (string?)null, category = (string?)null }
        });

        SetupHttpResponse(handlerMock, _options.DataSources.ScannerUserAgents.Url!, jsonContent);
        SetupHttpResponse(handlerMock, _options.DataSources.CoreRuleSetScanners.Url!, "");

        // Act
        var patterns = await fetcher.GetSecurityToolPatternsAsync();

        // Assert
        Assert.Single(patterns);
        Assert.Equal(expectedName, patterns[0].Name);
    }

    // ==========================================
    // Helper Methods
    // ==========================================

    private static void SetupHttpResponse(Mock<HttpMessageHandler> handlerMock, string url, string content)
    {
        handlerMock.Protected()
            .Setup<Task<HttpResponseMessage>>(
                "SendAsync",
                ItExpr.Is<HttpRequestMessage>(r => r.RequestUri!.ToString() == url),
                ItExpr.IsAny<CancellationToken>())
            .ReturnsAsync(new HttpResponseMessage
            {
                StatusCode = HttpStatusCode.OK,
                Content = new StringContent(content)
            });
    }

    private static void SetupHttpResponse(Mock<HttpMessageHandler> handlerMock, string url, HttpStatusCode statusCode)
    {
        handlerMock.Protected()
            .Setup<Task<HttpResponseMessage>>(
                "SendAsync",
                ItExpr.Is<HttpRequestMessage>(r => r.RequestUri!.ToString() == url),
                ItExpr.IsAny<CancellationToken>())
            .ReturnsAsync(new HttpResponseMessage
            {
                StatusCode = statusCode
            });
    }
}
