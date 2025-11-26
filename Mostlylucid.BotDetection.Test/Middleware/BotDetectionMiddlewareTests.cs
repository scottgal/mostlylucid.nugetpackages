using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Mostlylucid.BotDetection.Middleware;
using Mostlylucid.BotDetection.Models;
using Mostlylucid.BotDetection.Services;
using Mostlylucid.BotDetection.Test.Helpers;

namespace Mostlylucid.BotDetection.Test.Middleware;

/// <summary>
///     Comprehensive tests for BotDetectionMiddleware
/// </summary>
public class BotDetectionMiddlewareTests
{
    private readonly ILogger<BotDetectionMiddleware> _logger;

    public BotDetectionMiddlewareTests()
    {
        _logger = new Mock<ILogger<BotDetectionMiddleware>>().Object;
    }

    private BotDetectionMiddleware CreateMiddleware(
        RequestDelegate next,
        BotDetectionOptions? options = null)
    {
        return new BotDetectionMiddleware(
            next,
            _logger,
            Options.Create(options ?? new BotDetectionOptions()));
    }

    #region Normal Detection Flow Tests

    [Fact]
    public async Task InvokeAsync_NormalRequest_CallsDetectionService()
    {
        // Arrange
        var nextCalled = false;
        RequestDelegate next = _ =>
        {
            nextCalled = true;
            return Task.CompletedTask;
        };

        var mockService = new Mock<IBotDetectionService>();
        mockService.Setup(s => s.DetectAsync(It.IsAny<HttpContext>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new BotDetectionResult { IsBot = false, ConfidenceScore = 0.1 });

        var middleware = CreateMiddleware(next);
        var context = MockHttpContext.CreateRealisticBrowser();

        // Act
        await middleware.InvokeAsync(context, mockService.Object);

        // Assert
        Assert.True(nextCalled, "Next middleware should be called");
        mockService.Verify(s => s.DetectAsync(context, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task InvokeAsync_BotDetected_AddsHeadersAndResult()
    {
        // Arrange
        RequestDelegate next = _ => Task.CompletedTask;

        var mockService = new Mock<IBotDetectionService>();
        mockService.Setup(s => s.DetectAsync(It.IsAny<HttpContext>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new BotDetectionResult
            {
                IsBot = true,
                ConfidenceScore = 0.95,
                BotType = BotType.Scraper,
                BotName = "TestBot"
            });

        var middleware = CreateMiddleware(next);
        var context = MockHttpContext.CreateWithUserAgent("TestBot/1.0");

        // Act
        await middleware.InvokeAsync(context, mockService.Object);

        // Assert
        Assert.True(context.Response.Headers.ContainsKey("X-Bot-Detected"));
        Assert.Equal("true", context.Response.Headers["X-Bot-Detected"]);
        Assert.True(context.Response.Headers.ContainsKey("X-Bot-Confidence"));
        Assert.True(context.Items.ContainsKey(BotDetectionMiddleware.BotDetectionResultKey));
    }

    [Fact]
    public async Task InvokeAsync_HumanDetected_NosBotHeaders()
    {
        // Arrange
        RequestDelegate next = _ => Task.CompletedTask;

        var mockService = new Mock<IBotDetectionService>();
        mockService.Setup(s => s.DetectAsync(It.IsAny<HttpContext>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new BotDetectionResult { IsBot = false, ConfidenceScore = 0.1 });

        var middleware = CreateMiddleware(next);
        var context = MockHttpContext.CreateRealisticBrowser();

        // Act
        await middleware.InvokeAsync(context, mockService.Object);

        // Assert
        Assert.False(context.Response.Headers.ContainsKey("X-Bot-Detected"));
        Assert.True(context.Items.ContainsKey(BotDetectionMiddleware.BotDetectionResultKey));
    }

    [Fact]
    public async Task InvokeAsync_StoresResultInHttpContext()
    {
        // Arrange
        RequestDelegate next = _ => Task.CompletedTask;
        var expectedResult = new BotDetectionResult
        {
            IsBot = true,
            ConfidenceScore = 0.8,
            BotType = BotType.SearchEngine,
            BotName = "Googlebot"
        };

        var mockService = new Mock<IBotDetectionService>();
        mockService.Setup(s => s.DetectAsync(It.IsAny<HttpContext>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(expectedResult);

        var middleware = CreateMiddleware(next);
        var context = MockHttpContext.CreateGooglebot();

        // Act
        await middleware.InvokeAsync(context, mockService.Object);

        // Assert
        var storedResult = context.Items[BotDetectionMiddleware.BotDetectionResultKey] as BotDetectionResult;
        Assert.NotNull(storedResult);
        Assert.Equal(expectedResult.IsBot, storedResult.IsBot);
        Assert.Equal(expectedResult.ConfidenceScore, storedResult.ConfidenceScore);
        Assert.Equal(expectedResult.BotName, storedResult.BotName);
    }

    #endregion

    #region Test Mode Tests

    [Fact]
    public async Task InvokeAsync_TestModeDisabled_IgnoresTestHeader()
    {
        // Arrange
        RequestDelegate next = _ => Task.CompletedTask;

        var mockService = new Mock<IBotDetectionService>();
        mockService.Setup(s => s.DetectAsync(It.IsAny<HttpContext>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new BotDetectionResult { IsBot = false, ConfidenceScore = 0.1 });

        var options = new BotDetectionOptions { EnableTestMode = false };
        var middleware = CreateMiddleware(next, options);
        var context = MockHttpContext.CreateWithHeaders(new Dictionary<string, string>
        {
            { "ml-bot-test-mode", "bot" }
        });

        // Act
        await middleware.InvokeAsync(context, mockService.Object);

        // Assert - detection service should be called (test header ignored)
        mockService.Verify(s => s.DetectAsync(context, It.IsAny<CancellationToken>()), Times.Once);
        Assert.False(context.Response.Headers.ContainsKey("X-Test-Mode"));
    }

    [Fact]
    public async Task InvokeAsync_TestModeEnabled_DisableBypassesDetection()
    {
        // Arrange
        var nextCalled = false;
        RequestDelegate next = _ =>
        {
            nextCalled = true;
            return Task.CompletedTask;
        };

        var mockService = new Mock<IBotDetectionService>();
        var options = new BotDetectionOptions { EnableTestMode = true };
        var middleware = CreateMiddleware(next, options);
        var context = MockHttpContext.CreateWithHeaders(new Dictionary<string, string>
        {
            { "ml-bot-test-mode", "disable" }
        });

        // Act
        await middleware.InvokeAsync(context, mockService.Object);

        // Assert - detection service should NOT be called
        mockService.Verify(s => s.DetectAsync(It.IsAny<HttpContext>(), It.IsAny<CancellationToken>()), Times.Never);
        Assert.True(nextCalled);
        Assert.Equal("disabled", context.Response.Headers["X-Test-Mode"]);

        var result = context.Items[BotDetectionMiddleware.BotDetectionResultKey] as BotDetectionResult;
        Assert.NotNull(result);
        Assert.False(result.IsBot);
    }

    [Theory]
    [InlineData("bot", true, BotType.Unknown, "Test Bot")]
    [InlineData("human", false, null, null)]
    [InlineData("googlebot", true, BotType.SearchEngine, "Googlebot")]
    [InlineData("bingbot", true, BotType.SearchEngine, "Bingbot")]
    [InlineData("scraper", true, BotType.Scraper, "Test Scraper")]
    [InlineData("malicious", true, BotType.MaliciousBot, "Test Malicious Bot")]
    [InlineData("social", true, BotType.SocialMediaBot, "Test Social Bot")]
    [InlineData("monitor", true, BotType.MonitoringBot, "Test Monitoring Bot")]
    public async Task InvokeAsync_TestModeEnabled_SimulatesCorrectBotType(
        string testMode, bool expectedIsBot, BotType? expectedBotType, string? expectedBotName)
    {
        // Arrange
        RequestDelegate next = _ => Task.CompletedTask;
        var mockService = new Mock<IBotDetectionService>();
        var options = new BotDetectionOptions { EnableTestMode = true };
        var middleware = CreateMiddleware(next, options);
        var context = MockHttpContext.CreateWithHeaders(new Dictionary<string, string>
        {
            { "ml-bot-test-mode", testMode }
        });

        // Act
        await middleware.InvokeAsync(context, mockService.Object);

        // Assert
        var result = context.Items[BotDetectionMiddleware.BotDetectionResultKey] as BotDetectionResult;
        Assert.NotNull(result);
        Assert.Equal(expectedIsBot, result.IsBot);
        Assert.Equal(expectedBotType, result.BotType);
        if (expectedBotName != null)
            Assert.Equal(expectedBotName, result.BotName);
    }

    [Fact]
    public async Task InvokeAsync_TestModeEnabled_UnknownModeCreatesGenericBot()
    {
        // Arrange
        RequestDelegate next = _ => Task.CompletedTask;
        var mockService = new Mock<IBotDetectionService>();
        var options = new BotDetectionOptions { EnableTestMode = true };
        var middleware = CreateMiddleware(next, options);
        var context = MockHttpContext.CreateWithHeaders(new Dictionary<string, string>
        {
            { "ml-bot-test-mode", "custom-bot-type" }
        });

        // Act
        await middleware.InvokeAsync(context, mockService.Object);

        // Assert
        var result = context.Items[BotDetectionMiddleware.BotDetectionResultKey] as BotDetectionResult;
        Assert.NotNull(result);
        Assert.True(result.IsBot);
        Assert.Equal(BotType.Unknown, result.BotType);
        Assert.Equal("Test custom-bot-type", result.BotName);
        Assert.Equal(0.7, result.ConfidenceScore);
    }

    [Fact]
    public async Task InvokeAsync_TestModeEnabled_AddsTestModeHeader()
    {
        // Arrange
        RequestDelegate next = _ => Task.CompletedTask;
        var mockService = new Mock<IBotDetectionService>();
        var options = new BotDetectionOptions { EnableTestMode = true };
        var middleware = CreateMiddleware(next, options);
        var context = MockHttpContext.CreateWithHeaders(new Dictionary<string, string>
        {
            { "ml-bot-test-mode", "googlebot" }
        });

        // Act
        await middleware.InvokeAsync(context, mockService.Object);

        // Assert
        Assert.Equal("true", context.Response.Headers["X-Test-Mode"]);
        Assert.True(context.Response.Headers.ContainsKey("X-Bot-Detected"));
        Assert.True(context.Response.Headers.ContainsKey("X-Bot-Confidence"));
    }

    [Fact]
    public async Task InvokeAsync_TestModeEnabled_NoHeader_UsesNormalDetection()
    {
        // Arrange
        RequestDelegate next = _ => Task.CompletedTask;
        var mockService = new Mock<IBotDetectionService>();
        mockService.Setup(s => s.DetectAsync(It.IsAny<HttpContext>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new BotDetectionResult { IsBot = false, ConfidenceScore = 0.2 });

        var options = new BotDetectionOptions { EnableTestMode = true };
        var middleware = CreateMiddleware(next, options);
        var context = MockHttpContext.CreateRealisticBrowser();

        // Act
        await middleware.InvokeAsync(context, mockService.Object);

        // Assert - should use normal detection
        mockService.Verify(s => s.DetectAsync(context, It.IsAny<CancellationToken>()), Times.Once);
        Assert.False(context.Response.Headers.ContainsKey("X-Test-Mode"));
    }

    #endregion

    #region Security Tests

    [Fact]
    public async Task InvokeAsync_TestModeDisabled_DoesNotLeakTestModeInfo()
    {
        // Arrange
        RequestDelegate next = _ => Task.CompletedTask;
        var mockService = new Mock<IBotDetectionService>();
        mockService.Setup(s => s.DetectAsync(It.IsAny<HttpContext>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new BotDetectionResult { IsBot = false, ConfidenceScore = 0.1 });

        var options = new BotDetectionOptions { EnableTestMode = false };
        var middleware = CreateMiddleware(next, options);

        // Try various test mode values
        var testModes = new[] { "disable", "bot", "human", "googlebot", "malicious" };

        foreach (var testMode in testModes)
        {
            var context = MockHttpContext.CreateWithHeaders(new Dictionary<string, string>
            {
                { "ml-bot-test-mode", testMode }
            });

            // Act
            await middleware.InvokeAsync(context, mockService.Object);

            // Assert - no test mode info should be leaked
            Assert.False(context.Response.Headers.ContainsKey("X-Test-Mode"),
                $"Test mode header leaked for mode '{testMode}'");
        }
    }

    #endregion

    #region Pipeline Tests

    [Fact]
    public async Task InvokeAsync_AlwaysCallsNextMiddleware()
    {
        // Arrange
        var nextCallCount = 0;
        RequestDelegate next = _ =>
        {
            nextCallCount++;
            return Task.CompletedTask;
        };

        var mockService = new Mock<IBotDetectionService>();
        mockService.Setup(s => s.DetectAsync(It.IsAny<HttpContext>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new BotDetectionResult { IsBot = true, ConfidenceScore = 1.0 });

        var middleware = CreateMiddleware(next);
        var context = MockHttpContext.CreateSuspiciousBot();

        // Act
        await middleware.InvokeAsync(context, mockService.Object);

        // Assert - next should always be called
        Assert.Equal(1, nextCallCount);
    }

    [Fact]
    public async Task InvokeAsync_PassesCancellationToken()
    {
        // Arrange
        RequestDelegate next = _ => Task.CompletedTask;
        CancellationToken capturedToken = default;

        var mockService = new Mock<IBotDetectionService>();
        mockService.Setup(s => s.DetectAsync(It.IsAny<HttpContext>(), It.IsAny<CancellationToken>()))
            .Callback<HttpContext, CancellationToken>((_, ct) => capturedToken = ct)
            .ReturnsAsync(new BotDetectionResult());

        var middleware = CreateMiddleware(next);
        var context = MockHttpContext.CreateRealisticBrowser();

        using var cts = new CancellationTokenSource();
        context.RequestAborted = cts.Token;

        // Act
        await middleware.InvokeAsync(context, mockService.Object);

        // Assert
        Assert.Equal(cts.Token, capturedToken);
    }

    #endregion
}
