using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Mostlylucid.BotDetection.Middleware;
using Mostlylucid.BotDetection.Models;
using Mostlylucid.BotDetection.Orchestration;
using Mostlylucid.BotDetection.Policies;
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

    private static AggregatedEvidence CreateEvidence(
        double botProbability = 0.1,
        double confidence = 0.9,
        RiskBand riskBand = RiskBand.Low,
        BotType? botType = null,
        string? botName = null,
        PolicyAction? policyAction = null)
    {
        return new AggregatedEvidence
        {
            BotProbability = botProbability,
            Confidence = confidence,
            RiskBand = riskBand,
            PrimaryBotType = botType,
            PrimaryBotName = botName,
            PolicyAction = policyAction,
            Contributions = [],
            Signals = new Dictionary<string, object>(),
            CategoryBreakdown = new Dictionary<string, CategoryScore>(),
            ContributingDetectors = new HashSet<string>()
        };
    }

    private static Mock<BlackboardOrchestrator> CreateMockOrchestrator(AggregatedEvidence? result = null)
    {
        // Constructor: logger, options, detectors, learningBus?, policyRegistry?, policyEvaluator?
        var mock = new Mock<BlackboardOrchestrator>(
            Mock.Of<ILogger<BlackboardOrchestrator>>(),
            Options.Create(new BotDetectionOptions()),
            Enumerable.Empty<IContributingDetector>(),
            null, // learningBus
            null, // policyRegistry
            null  // policyEvaluator
        );

        var evidence = result ?? CreateEvidence();

        mock.Setup(o => o.DetectWithPolicyAsync(
                It.IsAny<HttpContext>(),
                It.IsAny<DetectionPolicy>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(evidence);

        return mock;
    }

    private static Mock<IPolicyRegistry> CreateMockPolicyRegistry()
    {
        var mock = new Mock<IPolicyRegistry>();
        mock.Setup(p => p.GetPolicyForPath(It.IsAny<string>()))
            .Returns(DetectionPolicy.Default);
        mock.Setup(p => p.GetPolicy(It.IsAny<string>()))
            .Returns(DetectionPolicy.Default);
        return mock;
    }

    #region Normal Detection Flow Tests

    [Fact]
    public async Task InvokeAsync_NormalRequest_CallsOrchestrator()
    {
        // Arrange
        var nextCalled = false;
        RequestDelegate next = _ =>
        {
            nextCalled = true;
            return Task.CompletedTask;
        };

        var mockOrchestrator = CreateMockOrchestrator(CreateEvidence(
            botProbability: 0.1,
            confidence: 0.9,
            riskBand: RiskBand.Low));
        var mockPolicyRegistry = CreateMockPolicyRegistry();

        var middleware = CreateMiddleware(next);
        var context = MockHttpContext.CreateRealisticBrowser();

        // Act
        await middleware.InvokeAsync(context, mockOrchestrator.Object, mockPolicyRegistry.Object);

        // Assert
        Assert.True(nextCalled, "Next middleware should be called");
        mockOrchestrator.Verify(o => o.DetectWithPolicyAsync(context, It.IsAny<DetectionPolicy>(), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task InvokeAsync_BotDetected_AddsHeadersAndResult()
    {
        // Arrange
        RequestDelegate next = _ => Task.CompletedTask;

        var mockOrchestrator = CreateMockOrchestrator(CreateEvidence(
            botProbability: 0.95,
            confidence: 0.9,
            riskBand: RiskBand.High,
            botType: BotType.Scraper,
            botName: "TestBot"));
        var mockPolicyRegistry = CreateMockPolicyRegistry();

        // Enable response headers to verify they are added
        var options = new BotDetectionOptions
        {
            ResponseHeaders = new ResponseHeadersOptions { Enabled = true }
        };
        var middleware = CreateMiddleware(next, options);
        var context = MockHttpContext.CreateWithUserAgent("TestBot/1.0");

        // Act
        await middleware.InvokeAsync(context, mockOrchestrator.Object, mockPolicyRegistry.Object);

        // Assert - Response headers should be added when ResponseHeaders.Enabled = true
        Assert.True(context.Response.Headers.ContainsKey("X-Bot-Risk-Score"));
        Assert.True(context.Response.Headers.ContainsKey("X-Bot-Risk-Band"));
        Assert.True(context.Items.ContainsKey(BotDetectionMiddleware.BotDetectionResultKey));
        Assert.True(context.Items.ContainsKey(BotDetectionMiddleware.AggregatedEvidenceKey));
    }

    [Fact]
    public async Task InvokeAsync_HumanDetected_NoBlockingHeaders()
    {
        // Arrange
        RequestDelegate next = _ => Task.CompletedTask;

        var mockOrchestrator = CreateMockOrchestrator(CreateEvidence(
            botProbability: 0.1,
            confidence: 0.9,
            riskBand: RiskBand.Low));
        var mockPolicyRegistry = CreateMockPolicyRegistry();

        var middleware = CreateMiddleware(next);
        var context = MockHttpContext.CreateRealisticBrowser();

        // Act
        await middleware.InvokeAsync(context, mockOrchestrator.Object, mockPolicyRegistry.Object);

        // Assert - no blocking headers, but result should be stored
        Assert.False(context.Response.Headers.ContainsKey("X-Bot-Detected"));
        Assert.True(context.Items.ContainsKey(BotDetectionMiddleware.BotDetectionResultKey));
        Assert.True(context.Items.ContainsKey(BotDetectionMiddleware.AggregatedEvidenceKey));
    }

    [Fact]
    public async Task InvokeAsync_StoresResultInHttpContext()
    {
        // Arrange
        RequestDelegate next = _ => Task.CompletedTask;
        var expectedEvidence = CreateEvidence(
            botProbability: 0.8,
            confidence: 0.85,
            riskBand: RiskBand.Medium,
            botType: BotType.SearchEngine,
            botName: "Googlebot");

        var mockOrchestrator = CreateMockOrchestrator(expectedEvidence);
        var mockPolicyRegistry = CreateMockPolicyRegistry();

        var middleware = CreateMiddleware(next);
        var context = MockHttpContext.CreateGooglebot();

        // Act
        await middleware.InvokeAsync(context, mockOrchestrator.Object, mockPolicyRegistry.Object);

        // Assert - legacy result created from aggregated evidence
        var storedResult = context.Items[BotDetectionMiddleware.BotDetectionResultKey] as BotDetectionResult;
        Assert.NotNull(storedResult);
        Assert.True(storedResult.IsBot); // 0.8 >= 0.5
        Assert.Equal(expectedEvidence.BotProbability, storedResult.ConfidenceScore);
        Assert.Equal(expectedEvidence.PrimaryBotName, storedResult.BotName);

        // Assert - aggregated evidence also stored
        var storedEvidence = context.Items[BotDetectionMiddleware.AggregatedEvidenceKey] as AggregatedEvidence;
        Assert.NotNull(storedEvidence);
        Assert.Equal(expectedEvidence.BotProbability, storedEvidence.BotProbability);
    }

    #endregion

    #region Test Mode Tests

    [Fact]
    public async Task InvokeAsync_TestModeDisabled_IgnoresTestHeader()
    {
        // Arrange
        RequestDelegate next = _ => Task.CompletedTask;

        var mockOrchestrator = CreateMockOrchestrator(CreateEvidence(
            botProbability: 0.1,
            confidence: 0.9,
            riskBand: RiskBand.Low));
        var mockPolicyRegistry = CreateMockPolicyRegistry();

        var options = new BotDetectionOptions { EnableTestMode = false };
        var middleware = CreateMiddleware(next, options);
        var context = MockHttpContext.CreateWithHeaders(new Dictionary<string, string>
        {
            { "ml-bot-test-mode", "bot" }
        });

        // Act
        await middleware.InvokeAsync(context, mockOrchestrator.Object, mockPolicyRegistry.Object);

        // Assert - orchestrator should be called (test header ignored)
        mockOrchestrator.Verify(o => o.DetectWithPolicyAsync(context, It.IsAny<DetectionPolicy>(), It.IsAny<CancellationToken>()), Times.Once);
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

        var mockOrchestrator = CreateMockOrchestrator();
        var mockPolicyRegistry = CreateMockPolicyRegistry();
        var options = new BotDetectionOptions { EnableTestMode = true };
        var middleware = CreateMiddleware(next, options);
        var context = MockHttpContext.CreateWithHeaders(new Dictionary<string, string>
        {
            { "ml-bot-test-mode", "disable" }
        });

        // Act
        await middleware.InvokeAsync(context, mockOrchestrator.Object, mockPolicyRegistry.Object);

        // Assert - orchestrator should NOT be called
        mockOrchestrator.Verify(o => o.DetectWithPolicyAsync(It.IsAny<HttpContext>(), It.IsAny<DetectionPolicy>(), It.IsAny<CancellationToken>()), Times.Never);
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
        var mockOrchestrator = CreateMockOrchestrator();
        var mockPolicyRegistry = CreateMockPolicyRegistry();
        var options = new BotDetectionOptions { EnableTestMode = true };
        var middleware = CreateMiddleware(next, options);
        var context = MockHttpContext.CreateWithHeaders(new Dictionary<string, string>
        {
            { "ml-bot-test-mode", testMode }
        });

        // Act
        await middleware.InvokeAsync(context, mockOrchestrator.Object, mockPolicyRegistry.Object);

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
        var mockOrchestrator = CreateMockOrchestrator();
        var mockPolicyRegistry = CreateMockPolicyRegistry();
        var options = new BotDetectionOptions { EnableTestMode = true };
        var middleware = CreateMiddleware(next, options);
        var context = MockHttpContext.CreateWithHeaders(new Dictionary<string, string>
        {
            { "ml-bot-test-mode", "custom-bot-type" }
        });

        // Act
        await middleware.InvokeAsync(context, mockOrchestrator.Object, mockPolicyRegistry.Object);

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
        var mockOrchestrator = CreateMockOrchestrator();
        var mockPolicyRegistry = CreateMockPolicyRegistry();
        var options = new BotDetectionOptions { EnableTestMode = true };
        var middleware = CreateMiddleware(next, options);
        var context = MockHttpContext.CreateWithHeaders(new Dictionary<string, string>
        {
            { "ml-bot-test-mode", "googlebot" }
        });

        // Act
        await middleware.InvokeAsync(context, mockOrchestrator.Object, mockPolicyRegistry.Object);

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
        var mockOrchestrator = CreateMockOrchestrator(CreateEvidence(
            botProbability: 0.2,
            confidence: 0.8,
            riskBand: RiskBand.Low));
        var mockPolicyRegistry = CreateMockPolicyRegistry();

        var options = new BotDetectionOptions { EnableTestMode = true };
        var middleware = CreateMiddleware(next, options);
        var context = MockHttpContext.CreateRealisticBrowser();

        // Act
        await middleware.InvokeAsync(context, mockOrchestrator.Object, mockPolicyRegistry.Object);

        // Assert - should use normal detection via orchestrator
        mockOrchestrator.Verify(o => o.DetectWithPolicyAsync(context, It.IsAny<DetectionPolicy>(), It.IsAny<CancellationToken>()), Times.Once);
        Assert.False(context.Response.Headers.ContainsKey("X-Test-Mode"));
    }

    #endregion

    #region Security Tests

    [Fact]
    public async Task InvokeAsync_TestModeDisabled_DoesNotLeakTestModeInfo()
    {
        // Arrange
        RequestDelegate next = _ => Task.CompletedTask;
        var mockOrchestrator = CreateMockOrchestrator(CreateEvidence(
            botProbability: 0.1,
            confidence: 0.9,
            riskBand: RiskBand.Low));
        var mockPolicyRegistry = CreateMockPolicyRegistry();

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
            await middleware.InvokeAsync(context, mockOrchestrator.Object, mockPolicyRegistry.Object);

            // Assert - no test mode info should be leaked
            Assert.False(context.Response.Headers.ContainsKey("X-Test-Mode"),
                $"Test mode header leaked for mode '{testMode}'");
        }
    }

    #endregion

    #region Pipeline Tests

    [Fact]
    public async Task InvokeAsync_BotBelowThreshold_CallsNextMiddleware()
    {
        // Arrange
        var nextCallCount = 0;
        RequestDelegate next = _ =>
        {
            nextCallCount++;
            return Task.CompletedTask;
        };

        // Low bot probability - should NOT block
        var mockOrchestrator = CreateMockOrchestrator(CreateEvidence(
            botProbability: 0.3,
            confidence: 0.8,
            riskBand: RiskBand.Low));
        var mockPolicyRegistry = CreateMockPolicyRegistry();

        var middleware = CreateMiddleware(next);
        var context = MockHttpContext.CreateSuspiciousBot();

        // Act
        await middleware.InvokeAsync(context, mockOrchestrator.Object, mockPolicyRegistry.Object);

        // Assert - next should be called when bot probability is low
        Assert.Equal(1, nextCallCount);
    }

    [Fact]
    public async Task InvokeAsync_BotAboveThreshold_BlocksAndDoesNotCallNext()
    {
        // Arrange
        var nextCallCount = 0;
        RequestDelegate next = _ =>
        {
            nextCallCount++;
            return Task.CompletedTask;
        };

        // High bot probability with Block action - should block
        var mockOrchestrator = CreateMockOrchestrator(CreateEvidence(
            botProbability: 1.0,
            confidence: 1.0,
            riskBand: RiskBand.VeryHigh,
            policyAction: PolicyAction.Block));
        var mockPolicyRegistry = CreateMockPolicyRegistry();

        var middleware = CreateMiddleware(next);
        var context = MockHttpContext.CreateSuspiciousBot();

        // Act
        await middleware.InvokeAsync(context, mockOrchestrator.Object, mockPolicyRegistry.Object);

        // Assert - next should NOT be called when bot is blocked
        Assert.Equal(0, nextCallCount);
        Assert.Equal(403, context.Response.StatusCode);
    }

    [Fact]
    public async Task InvokeAsync_PassesCancellationToken()
    {
        // Arrange
        RequestDelegate next = _ => Task.CompletedTask;
        CancellationToken capturedToken = default;

        // Constructor: logger, options, detectors, learningBus?, policyRegistry?, policyEvaluator?
        var mockOrchestrator = new Mock<BlackboardOrchestrator>(
            Mock.Of<ILogger<BlackboardOrchestrator>>(),
            Options.Create(new BotDetectionOptions()),
            Enumerable.Empty<IContributingDetector>(),
            null, // learningBus
            null, // policyRegistry
            null  // policyEvaluator
        );

        mockOrchestrator.Setup(o => o.DetectWithPolicyAsync(
                It.IsAny<HttpContext>(),
                It.IsAny<DetectionPolicy>(),
                It.IsAny<CancellationToken>()))
            .Callback<HttpContext, DetectionPolicy, CancellationToken>((_, _, ct) => capturedToken = ct)
            .ReturnsAsync(CreateEvidence(
                botProbability: 0.1,
                confidence: 0.9,
                riskBand: RiskBand.Low));

        var mockPolicyRegistry = CreateMockPolicyRegistry();

        var middleware = CreateMiddleware(next);
        var context = MockHttpContext.CreateRealisticBrowser();

        using var cts = new CancellationTokenSource();
        context.RequestAborted = cts.Token;

        // Act
        await middleware.InvokeAsync(context, mockOrchestrator.Object, mockPolicyRegistry.Object);

        // Assert
        Assert.Equal(cts.Token, capturedToken);
    }

    #endregion
}
