using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Mostlylucid.LLMContentModeration.Models;
using Mostlylucid.LLMContentModeration.Services;

namespace Mostlylucid.LLMContentModeration.Test;

public class ContentModerationServiceTests
{
    private readonly ModerationOptions _defaultOptions;
    private readonly Mock<ILogger<ContentModerationService>> _loggerMock;
    private readonly Mock<IModerationOllamaClient> _ollamaClientMock;
    private readonly Mock<IPiiDetector> _piiDetectorMock;

    public ContentModerationServiceTests()
    {
        _loggerMock = new Mock<ILogger<ContentModerationService>>();
        _ollamaClientMock = new Mock<IModerationOllamaClient>();
        _piiDetectorMock = new Mock<IPiiDetector>();
        _defaultOptions = new ModerationOptions
        {
            Enabled = true,
            DefaultMode = ModerationMode.Block
        };
    }

    private ContentModerationService CreateService(ModerationOptions? options = null)
    {
        var opts = Options.Create(options ?? _defaultOptions);
        return new ContentModerationService(
            _loggerMock.Object,
            _ollamaClientMock.Object,
            _piiDetectorMock.Object,
            opts);
    }

    #region Processing Time Tests

    [Fact]
    public async Task ModerateAsync_TracksProcessingTime()
    {
        _piiDetectorMock.Setup(x => x.DetectPii(It.IsAny<string>(), It.IsAny<PiiDetectionOptions>()))
            .Returns([]);

        _ollamaClientMock.Setup(x => x.ClassifyContentAsync(
                It.IsAny<string>(),
                It.IsAny<ContentClassificationOptions>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync([]);

        var service = CreateService();

        var result = await service.ModerateAsync("Test content");

        Assert.True(result.ProcessingTimeMs >= 0);
        Assert.NotEqual(default, result.Timestamp);
    }

    #endregion

    #region Basic Moderation Tests

    [Fact]
    public async Task ModerateAsync_CleanContent_NotFlagged()
    {
        // Arrange
        _piiDetectorMock.Setup(x => x.DetectPii(It.IsAny<string>(), It.IsAny<PiiDetectionOptions>()))
            .Returns([]);

        _ollamaClientMock.Setup(x => x.ClassifyContentAsync(
                It.IsAny<string>(),
                It.IsAny<ContentClassificationOptions>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync([]);

        var service = CreateService();

        // Act
        var result = await service.ModerateAsync("This is a clean comment.");

        // Assert
        Assert.False(result.IsFlagged);
        Assert.False(result.IsBlocked);
        Assert.Empty(result.Flags);
        Assert.Empty(result.PiiMatches);
    }

    [Fact]
    public async Task ModerateAsync_ToxicContent_BlockedInBlockMode()
    {
        // Arrange
        _piiDetectorMock.Setup(x => x.DetectPii(It.IsAny<string>(), It.IsAny<PiiDetectionOptions>()))
            .Returns([]);

        _ollamaClientMock.Setup(x => x.ClassifyContentAsync(
                It.IsAny<string>(),
                It.IsAny<ContentClassificationOptions>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync([
                new ContentFlag
                {
                    Category = ContentCategory.Toxicity,
                    Confidence = 0.9f,
                    Threshold = 0.7f,
                    Explanation = "Contains hateful content"
                }
            ]);

        var service = CreateService();

        // Act
        var result = await service.ModerateAsync("Some toxic content");

        // Assert
        Assert.True(result.IsFlagged);
        Assert.True(result.IsBlocked);
        Assert.Single(result.Flags);
        Assert.Equal(ContentCategory.Toxicity, result.Flags[0].Category);
    }

    [Fact]
    public async Task ModerateAsync_ToxicContent_NotBlockedInDetectOnlyMode()
    {
        // Arrange
        _piiDetectorMock.Setup(x => x.DetectPii(It.IsAny<string>(), It.IsAny<PiiDetectionOptions>()))
            .Returns([]);

        _ollamaClientMock.Setup(x => x.ClassifyContentAsync(
                It.IsAny<string>(),
                It.IsAny<ContentClassificationOptions>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync([
                new ContentFlag
                {
                    Category = ContentCategory.Toxicity,
                    Confidence = 0.9f,
                    Threshold = 0.7f
                }
            ]);

        var service = CreateService();

        // Act
        var result = await service.ModerateAsync("Some toxic content", ModerationMode.DetectOnly);

        // Assert
        Assert.True(result.IsFlagged);
        Assert.False(result.IsBlocked); // Not blocked in DetectOnly
        Assert.Equal(ModerationMode.DetectOnly, result.Mode);
    }

    #endregion

    #region PII Handling Tests

    [Fact]
    public async Task ModerateAsync_ContentWithPii_BlockedInBlockMode()
    {
        // Arrange
        var piiMatches = new List<PiiMatch>
        {
            new()
            {
                Type = PiiType.Email,
                OriginalValue = "test@example.com",
                StartIndex = 10,
                EndIndex = 26
            }
        };

        _piiDetectorMock.Setup(x => x.DetectPii(It.IsAny<string>(), It.IsAny<PiiDetectionOptions>()))
            .Returns(piiMatches);

        _ollamaClientMock.Setup(x => x.ClassifyContentAsync(
                It.IsAny<string>(),
                It.IsAny<ContentClassificationOptions>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync([]);

        var service = CreateService();

        // Act
        var result = await service.ModerateAsync("Contact: test@example.com");

        // Assert
        Assert.True(result.IsFlagged);
        Assert.True(result.IsBlocked); // Blocked because of PII
        Assert.Single(result.PiiMatches);
    }

    [Fact]
    public async Task ModerateAsync_ContentWithPii_MaskedInMaskMode()
    {
        // Arrange
        var piiMatches = new List<PiiMatch>
        {
            new()
            {
                Type = PiiType.Email,
                OriginalValue = "test@example.com",
                StartIndex = 10,
                EndIndex = 26
            }
        };

        _piiDetectorMock.Setup(x => x.DetectPii(It.IsAny<string>(), It.IsAny<PiiDetectionOptions>()))
            .Returns(piiMatches);

        _piiDetectorMock.Setup(x =>
                x.MaskPii(It.IsAny<string>(), It.IsAny<List<PiiMatch>>(), It.IsAny<PiiDetectionOptions>()))
            .Returns("Contact: te**********om");

        _ollamaClientMock.Setup(x => x.ClassifyContentAsync(
                It.IsAny<string>(),
                It.IsAny<ContentClassificationOptions>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync([]);

        var service = CreateService();

        // Act
        var result = await service.ModerateAsync("Contact: test@example.com", ModerationMode.MaskAndAllow);

        // Assert
        Assert.True(result.IsFlagged);
        Assert.False(result.IsBlocked); // Not blocked, just masked
        Assert.NotNull(result.ModeratedContent);
        Assert.Contains("**", result.ModeratedContent);
    }

    #endregion

    #region Edge Cases

    [Fact]
    public async Task ModerateAsync_EmptyContent_ReturnsSuccessNotFlagged()
    {
        var service = CreateService();

        var result = await service.ModerateAsync(string.Empty);

        Assert.True(result.Success);
        Assert.False(result.IsFlagged);
        Assert.False(result.IsBlocked);
    }

    [Fact]
    public async Task ModerateAsync_LongContent_Truncated()
    {
        // Arrange
        var options = new ModerationOptions
        {
            Enabled = true,
            MaxContentLength = 100
        };

        _piiDetectorMock.Setup(x => x.DetectPii(It.IsAny<string>(), It.IsAny<PiiDetectionOptions>()))
            .Returns([]);

        _ollamaClientMock.Setup(x => x.ClassifyContentAsync(
                It.IsAny<string>(),
                It.IsAny<ContentClassificationOptions>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync([]);

        var service = CreateService(options);
        var longContent = new string('a', 500);

        // Act
        var result = await service.ModerateAsync(longContent);

        // Assert - verify the service was called (content was processed)
        _ollamaClientMock.Verify(x => x.ClassifyContentAsync(
            It.Is<string>(s => s.Length <= 100),
            It.IsAny<ContentClassificationOptions>(),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task ModerateAsync_OllamaError_ContinuesWithPiiOnly()
    {
        // Arrange
        _piiDetectorMock.Setup(x => x.DetectPii(It.IsAny<string>(), It.IsAny<PiiDetectionOptions>()))
            .Returns([
                new PiiMatch { Type = PiiType.Email, OriginalValue = "test@test.com" }
            ]);

        _ollamaClientMock.Setup(x => x.ClassifyContentAsync(
                It.IsAny<string>(),
                It.IsAny<ContentClassificationOptions>(),
                It.IsAny<CancellationToken>()))
            .ThrowsAsync(new HttpRequestException("Ollama unavailable"));

        var service = CreateService();

        // Act
        var result = await service.ModerateAsync("Contact: test@test.com");

        // Assert - should still have PII detection even if Ollama failed
        Assert.True(result.IsFlagged);
        Assert.Single(result.PiiMatches);
        Assert.Empty(result.Flags); // Classification failed
    }

    #endregion

    #region Service Status Tests

    [Fact]
    public async Task IsAvailableAsync_ReturnsOllamaAvailability()
    {
        _ollamaClientMock.Setup(x => x.IsAvailableAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);

        var service = CreateService();

        var result = await service.IsAvailableAsync();

        Assert.True(result);
    }

    [Fact]
    public async Task GetStatusAsync_ReturnsServiceStatus()
    {
        _ollamaClientMock.Setup(x => x.IsAvailableAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);
        _ollamaClientMock.Setup(x => x.GetModelsAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(["llama3.2:3b", "mistral"]);

        var service = CreateService();

        var status = await service.GetStatusAsync();

        Assert.True(status.IsAvailable);
        Assert.NotNull(status.AvailableModels);
        Assert.Equal(2, status.AvailableModels.Count);
    }

    #endregion
}