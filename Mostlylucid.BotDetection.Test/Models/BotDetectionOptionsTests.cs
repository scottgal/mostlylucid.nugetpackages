using Mostlylucid.BotDetection.Models;

namespace Mostlylucid.BotDetection.Test.Models;

/// <summary>
///     Tests for BotDetectionOptions default values and validation
/// </summary>
public class BotDetectionOptionsTests
{
    #region Default Values Tests

    [Fact]
    public void Constructor_SetsDefaultBotThreshold()
    {
        // Act
        var options = new BotDetectionOptions();

        // Assert
        Assert.Equal(0.7, options.BotThreshold);
    }

    [Fact]
    public void Constructor_SetsDefaultTestModeDisabled()
    {
        // Act
        var options = new BotDetectionOptions();

        // Assert
        Assert.False(options.EnableTestMode);
    }

    [Fact]
    public void Constructor_SetsDefaultUserAgentDetectionEnabled()
    {
        // Act
        var options = new BotDetectionOptions();

        // Assert
        Assert.True(options.EnableUserAgentDetection);
    }

    [Fact]
    public void Constructor_SetsDefaultHeaderAnalysisEnabled()
    {
        // Act
        var options = new BotDetectionOptions();

        // Assert
        Assert.True(options.EnableHeaderAnalysis);
    }

    [Fact]
    public void Constructor_SetsDefaultIpDetectionEnabled()
    {
        // Act
        var options = new BotDetectionOptions();

        // Assert
        Assert.True(options.EnableIpDetection);
    }

    [Fact]
    public void Constructor_SetsDefaultBehavioralAnalysisEnabled()
    {
        // Act
        var options = new BotDetectionOptions();

        // Assert
        Assert.True(options.EnableBehavioralAnalysis);
    }

    [Fact]
    public void Constructor_SetsDefaultLlmDetectionDisabled()
    {
        // Act
        var options = new BotDetectionOptions();

        // Assert
        Assert.False(options.EnableLlmDetection);
    }

    [Fact]
    public void Constructor_SetsDefaultOllamaEndpoint()
    {
        // Act
        var options = new BotDetectionOptions();

        // Assert
        Assert.Equal("http://localhost:11434", options.OllamaEndpoint);
    }

    [Fact]
    public void Constructor_SetsDefaultOllamaModel()
    {
        // Act
        var options = new BotDetectionOptions();

        // Assert
        Assert.Equal("qwen2.5:1.5b", options.OllamaModel);
    }

    [Fact]
    public void Constructor_SetsDefaultLlmTimeout()
    {
        // Act
        var options = new BotDetectionOptions();

        // Assert
        Assert.Equal(2000, options.LlmTimeoutMs);
    }

    [Fact]
    public void Constructor_SetsDefaultMaxRequestsPerMinute()
    {
        // Act
        var options = new BotDetectionOptions();

        // Assert
        Assert.Equal(60, options.MaxRequestsPerMinute);
    }

    [Fact]
    public void Constructor_SetsDefaultCacheDuration()
    {
        // Act
        var options = new BotDetectionOptions();

        // Assert
        Assert.Equal(300, options.CacheDurationSeconds);
    }

    #endregion

    #region Whitelisted Bot Patterns Tests

    [Fact]
    public void Constructor_InitializesWhitelistedBotPatterns()
    {
        // Act
        var options = new BotDetectionOptions();

        // Assert
        Assert.NotNull(options.WhitelistedBotPatterns);
        Assert.NotEmpty(options.WhitelistedBotPatterns);
    }

    [Theory]
    [InlineData("Googlebot")]
    [InlineData("Bingbot")]
    [InlineData("DuckDuckBot")]
    [InlineData("Slackbot")]
    [InlineData("Baiduspider")]
    [InlineData("YandexBot")]
    public void Constructor_ContainsCommonGoodBots(string botName)
    {
        // Act
        var options = new BotDetectionOptions();

        // Assert
        Assert.Contains(botName, options.WhitelistedBotPatterns);
    }

    [Fact]
    public void WhitelistedBotPatterns_CanBeModified()
    {
        // Arrange
        var options = new BotDetectionOptions();
        var customBot = "MyCustomBot";

        // Act
        options.WhitelistedBotPatterns.Add(customBot);

        // Assert
        Assert.Contains(customBot, options.WhitelistedBotPatterns);
    }

    [Fact]
    public void WhitelistedBotPatterns_CanBeCleared()
    {
        // Arrange
        var options = new BotDetectionOptions();

        // Act
        options.WhitelistedBotPatterns.Clear();

        // Assert
        Assert.Empty(options.WhitelistedBotPatterns);
    }

    [Fact]
    public void WhitelistedBotPatterns_CanBeReplaced()
    {
        // Arrange
        var options = new BotDetectionOptions();
        var customBots = new List<string> { "CustomBot1", "CustomBot2" };

        // Act
        options.WhitelistedBotPatterns = customBots;

        // Assert
        Assert.Equal(2, options.WhitelistedBotPatterns.Count);
        Assert.Contains("CustomBot1", options.WhitelistedBotPatterns);
        Assert.Contains("CustomBot2", options.WhitelistedBotPatterns);
    }

    #endregion

    #region Datacenter IP Prefixes Tests

    [Fact]
    public void Constructor_InitializesDatacenterIpPrefixes()
    {
        // Act
        var options = new BotDetectionOptions();

        // Assert
        Assert.NotNull(options.DatacenterIpPrefixes);
        Assert.NotEmpty(options.DatacenterIpPrefixes);
    }

    [Fact]
    public void Constructor_ContainsAwsIpPrefixes()
    {
        // Act
        var options = new BotDetectionOptions();

        // Assert - Check for AWS prefixes
        Assert.Contains(options.DatacenterIpPrefixes, p => p.StartsWith("3."));
        Assert.Contains(options.DatacenterIpPrefixes, p => p.StartsWith("13."));
        Assert.Contains(options.DatacenterIpPrefixes, p => p.StartsWith("52."));
    }

    [Fact]
    public void Constructor_ContainsAzureIpPrefixes()
    {
        // Act
        var options = new BotDetectionOptions();

        // Assert - Check for Azure prefixes
        Assert.Contains(options.DatacenterIpPrefixes, p => p.StartsWith("20."));
        Assert.Contains(options.DatacenterIpPrefixes, p => p.StartsWith("40."));
    }

    [Fact]
    public void Constructor_ContainsGcpIpPrefixes()
    {
        // Act
        var options = new BotDetectionOptions();

        // Assert - Check for GCP prefixes
        Assert.Contains(options.DatacenterIpPrefixes, p => p.StartsWith("34."));
        Assert.Contains(options.DatacenterIpPrefixes, p => p.StartsWith("35."));
    }

    [Fact]
    public void DatacenterIpPrefixes_CanBeModified()
    {
        // Arrange
        var options = new BotDetectionOptions();
        var customPrefix = "100.0.0.0/8";

        // Act
        options.DatacenterIpPrefixes.Add(customPrefix);

        // Assert
        Assert.Contains(customPrefix, options.DatacenterIpPrefixes);
    }

    #endregion

    #region Property Setting Tests

    [Theory]
    [InlineData(0.5)]
    [InlineData(0.8)]
    [InlineData(0.9)]
    public void BotThreshold_CanBeSet(double threshold)
    {
        // Arrange
        var options = new BotDetectionOptions();

        // Act
        options.BotThreshold = threshold;

        // Assert
        Assert.Equal(threshold, options.BotThreshold);
    }

    [Fact]
    public void EnableTestMode_CanBeEnabled()
    {
        // Arrange
        var options = new BotDetectionOptions();

        // Act
        options.EnableTestMode = true;

        // Assert
        Assert.True(options.EnableTestMode);
    }

    [Fact]
    public void OllamaEndpoint_CanBeSet()
    {
        // Arrange
        var options = new BotDetectionOptions();
        var customEndpoint = "http://custom-ollama:11434";

        // Act
        options.OllamaEndpoint = customEndpoint;

        // Assert
        Assert.Equal(customEndpoint, options.OllamaEndpoint);
    }

    [Fact]
    public void OllamaModel_CanBeSet()
    {
        // Arrange
        var options = new BotDetectionOptions();
        var customModel = "llama3.2:latest";

        // Act
        options.OllamaModel = customModel;

        // Assert
        Assert.Equal(customModel, options.OllamaModel);
    }

    [Theory]
    [InlineData(1000)]
    [InlineData(5000)]
    [InlineData(10000)]
    public void LlmTimeoutMs_CanBeSet(int timeout)
    {
        // Arrange
        var options = new BotDetectionOptions();

        // Act
        options.LlmTimeoutMs = timeout;

        // Assert
        Assert.Equal(timeout, options.LlmTimeoutMs);
    }

    [Theory]
    [InlineData(30)]
    [InlineData(100)]
    [InlineData(1000)]
    public void MaxRequestsPerMinute_CanBeSet(int maxRequests)
    {
        // Arrange
        var options = new BotDetectionOptions();

        // Act
        options.MaxRequestsPerMinute = maxRequests;

        // Assert
        Assert.Equal(maxRequests, options.MaxRequestsPerMinute);
    }

    [Theory]
    [InlineData(60)]
    [InlineData(600)]
    [InlineData(3600)]
    public void CacheDurationSeconds_CanBeSet(int duration)
    {
        // Arrange
        var options = new BotDetectionOptions();

        // Act
        options.CacheDurationSeconds = duration;

        // Assert
        Assert.Equal(duration, options.CacheDurationSeconds);
    }

    #endregion

    #region All Detectors Disabled Tests

    [Fact]
    public void AllDetectors_CanBeDisabled()
    {
        // Arrange & Act
        var options = new BotDetectionOptions
        {
            EnableUserAgentDetection = false,
            EnableHeaderAnalysis = false,
            EnableIpDetection = false,
            EnableBehavioralAnalysis = false,
            EnableLlmDetection = false
        };

        // Assert
        Assert.False(options.EnableUserAgentDetection);
        Assert.False(options.EnableHeaderAnalysis);
        Assert.False(options.EnableIpDetection);
        Assert.False(options.EnableBehavioralAnalysis);
        Assert.False(options.EnableLlmDetection);
    }

    [Fact]
    public void AllDetectors_CanBeEnabled()
    {
        // Arrange & Act
        var options = new BotDetectionOptions
        {
            EnableUserAgentDetection = true,
            EnableHeaderAnalysis = true,
            EnableIpDetection = true,
            EnableBehavioralAnalysis = true,
            EnableLlmDetection = true
        };

        // Assert
        Assert.True(options.EnableUserAgentDetection);
        Assert.True(options.EnableHeaderAnalysis);
        Assert.True(options.EnableIpDetection);
        Assert.True(options.EnableBehavioralAnalysis);
        Assert.True(options.EnableLlmDetection);
    }

    #endregion
}