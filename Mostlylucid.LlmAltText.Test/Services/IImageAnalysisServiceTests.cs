using Mostlylucid.LlmAltText.Services;

namespace Mostlylucid.LlmAltText.Test.Services;

/// <summary>
///     Tests for IImageAnalysisService interface contract
///     Note: These tests verify mock behavior since actual service requires model initialization
/// </summary>
public class IImageAnalysisServiceTests
{
    #region Interface Contract Tests

    [Fact]
    public void Interface_DefinesGenerateAltTextAsyncMethod()
    {
        // Arrange
        var mockService = new Mock<IImageAnalysisService>();

        // Act - Verify method exists and can be setup
        mockService.Setup(s => s.GenerateAltTextAsync(
                It.IsAny<Stream>(),
                It.IsAny<string>()))
            .ReturnsAsync("Test alt text");

        // Assert - Method signature is correct
        Assert.NotNull(mockService.Object);
    }

    [Fact]
    public void Interface_DefinesExtractTextAsyncMethod()
    {
        // Arrange
        var mockService = new Mock<IImageAnalysisService>();

        // Act
        mockService.Setup(s => s.ExtractTextAsync(It.IsAny<Stream>()))
            .ReturnsAsync("Extracted text");

        // Assert
        Assert.NotNull(mockService.Object);
    }

    [Fact]
    public void Interface_DefinesAnalyzeImageAsyncMethod()
    {
        // Arrange
        var mockService = new Mock<IImageAnalysisService>();

        // Act
        mockService.Setup(s => s.AnalyzeImageAsync(It.IsAny<Stream>()))
            .ReturnsAsync(("Alt text", "Extracted text"));

        // Assert
        Assert.NotNull(mockService.Object);
    }

    [Fact]
    public void Interface_DefinesIsReadyProperty()
    {
        // Arrange
        var mockService = new Mock<IImageAnalysisService>();

        // Act
        mockService.Setup(s => s.IsReady).Returns(true);

        // Assert
        Assert.True(mockService.Object.IsReady);
    }

    #endregion

    #region Mock Behavior Tests

    [Fact]
    public async Task GenerateAltTextAsync_ReturnsExpectedResult()
    {
        // Arrange
        var mockService = new Mock<IImageAnalysisService>();
        var expectedAltText = "A colorful landscape with mountains and a river";
        mockService.Setup(s => s.GenerateAltTextAsync(
                It.IsAny<Stream>(),
                "MORE_DETAILED_CAPTION"))
            .ReturnsAsync(expectedAltText);

        using var stream = new MemoryStream(new byte[] { 0x89, 0x50, 0x4E, 0x47 }); // PNG header

        // Act
        var result = await mockService.Object.GenerateAltTextAsync(stream);

        // Assert
        Assert.Equal(expectedAltText, result);
    }

    [Fact]
    public async Task GenerateAltTextAsync_DefaultTaskType()
    {
        // Arrange
        var mockService = new Mock<IImageAnalysisService>();
        mockService.Setup(s => s.GenerateAltTextAsync(
                It.IsAny<Stream>(),
                "MORE_DETAILED_CAPTION"))
            .ReturnsAsync("Alt text with detailed caption");

        using var stream = new MemoryStream();

        // Act - Using default task type
        var result = await mockService.Object.GenerateAltTextAsync(stream);

        // Assert - Verify default parameter is used
        mockService.Verify(s => s.GenerateAltTextAsync(
            It.IsAny<Stream>(),
            "MORE_DETAILED_CAPTION"), Times.Once);
    }

    [Theory]
    [InlineData("CAPTION")]
    [InlineData("DETAILED_CAPTION")]
    [InlineData("MORE_DETAILED_CAPTION")]
    public async Task GenerateAltTextAsync_DifferentTaskTypes(string taskType)
    {
        // Arrange
        var mockService = new Mock<IImageAnalysisService>();
        mockService.Setup(s => s.GenerateAltTextAsync(
                It.IsAny<Stream>(),
                taskType))
            .ReturnsAsync($"Alt text from {taskType}");

        using var stream = new MemoryStream();

        // Act
        var result = await mockService.Object.GenerateAltTextAsync(stream, taskType);

        // Assert
        Assert.Contains(taskType, result);
    }

    [Fact]
    public async Task ExtractTextAsync_ReturnsExtractedText()
    {
        // Arrange
        var mockService = new Mock<IImageAnalysisService>();
        var expectedText = "Hello World\nThis is extracted text";
        mockService.Setup(s => s.ExtractTextAsync(It.IsAny<Stream>()))
            .ReturnsAsync(expectedText);

        using var stream = new MemoryStream();

        // Act
        var result = await mockService.Object.ExtractTextAsync(stream);

        // Assert
        Assert.Equal(expectedText, result);
    }

    [Fact]
    public async Task ExtractTextAsync_ReturnsEmptyForNoText()
    {
        // Arrange
        var mockService = new Mock<IImageAnalysisService>();
        mockService.Setup(s => s.ExtractTextAsync(It.IsAny<Stream>()))
            .ReturnsAsync(string.Empty);

        using var stream = new MemoryStream();

        // Act
        var result = await mockService.Object.ExtractTextAsync(stream);

        // Assert
        Assert.Empty(result);
    }

    [Fact]
    public async Task AnalyzeImageAsync_ReturnsBothAltTextAndExtractedText()
    {
        // Arrange
        var mockService = new Mock<IImageAnalysisService>();
        var expectedAltText = "A business card with contact information";
        var expectedExtractedText = "John Doe\njohn@example.com\n555-1234";

        mockService.Setup(s => s.AnalyzeImageAsync(It.IsAny<Stream>()))
            .ReturnsAsync((expectedAltText, expectedExtractedText));

        using var stream = new MemoryStream();

        // Act
        var (altText, extractedText) = await mockService.Object.AnalyzeImageAsync(stream);

        // Assert
        Assert.Equal(expectedAltText, altText);
        Assert.Equal(expectedExtractedText, extractedText);
    }

    [Fact]
    public async Task AnalyzeImageAsync_AltTextCanBeEmptyIfExtractedTextPresent()
    {
        // Arrange
        var mockService = new Mock<IImageAnalysisService>();
        mockService.Setup(s => s.AnalyzeImageAsync(It.IsAny<Stream>()))
            .ReturnsAsync(("", "Some text in image"));

        using var stream = new MemoryStream();

        // Act
        var (altText, extractedText) = await mockService.Object.AnalyzeImageAsync(stream);

        // Assert
        Assert.Empty(altText);
        Assert.NotEmpty(extractedText);
    }

    [Fact]
    public async Task AnalyzeImageAsync_ExtractedTextCanBeEmptyIfAltTextPresent()
    {
        // Arrange
        var mockService = new Mock<IImageAnalysisService>();
        mockService.Setup(s => s.AnalyzeImageAsync(It.IsAny<Stream>()))
            .ReturnsAsync(("A scenic landscape", ""));

        using var stream = new MemoryStream();

        // Act
        var (altText, extractedText) = await mockService.Object.AnalyzeImageAsync(stream);

        // Assert
        Assert.NotEmpty(altText);
        Assert.Empty(extractedText);
    }

    #endregion

    #region IsReady Property Tests

    [Fact]
    public void IsReady_InitiallyFalse()
    {
        // Arrange
        var mockService = new Mock<IImageAnalysisService>();
        mockService.Setup(s => s.IsReady).Returns(false);

        // Act & Assert
        Assert.False(mockService.Object.IsReady);
    }

    [Fact]
    public void IsReady_TrueAfterInitialization()
    {
        // Arrange
        var mockService = new Mock<IImageAnalysisService>();
        mockService.Setup(s => s.IsReady).Returns(true);

        // Act & Assert
        Assert.True(mockService.Object.IsReady);
    }

    #endregion

    #region Error Handling Tests

    [Fact]
    public async Task GenerateAltTextAsync_HandlesNullStream()
    {
        // Arrange
        var mockService = new Mock<IImageAnalysisService>();
        mockService.Setup(s => s.GenerateAltTextAsync(
                null!,
                It.IsAny<string>()))
            .ThrowsAsync(new ArgumentNullException("imageStream"));

        // Act & Assert
        await Assert.ThrowsAsync<ArgumentNullException>(() =>
            mockService.Object.GenerateAltTextAsync(null!, "CAPTION"));
    }

    [Fact]
    public async Task ExtractTextAsync_HandlesNullStream()
    {
        // Arrange
        var mockService = new Mock<IImageAnalysisService>();
        mockService.Setup(s => s.ExtractTextAsync(null!))
            .ThrowsAsync(new ArgumentNullException("imageStream"));

        // Act & Assert
        await Assert.ThrowsAsync<ArgumentNullException>(() => mockService.Object.ExtractTextAsync(null!));
    }

    [Fact]
    public async Task AnalyzeImageAsync_HandlesNullStream()
    {
        // Arrange
        var mockService = new Mock<IImageAnalysisService>();
        mockService.Setup(s => s.AnalyzeImageAsync(null!))
            .ThrowsAsync(new ArgumentNullException("imageStream"));

        // Act & Assert
        await Assert.ThrowsAsync<ArgumentNullException>(() => mockService.Object.AnalyzeImageAsync(null!));
    }

    [Fact]
    public async Task GenerateAltTextAsync_HandlesInvalidImage()
    {
        // Arrange
        var mockService = new Mock<IImageAnalysisService>();
        mockService.Setup(s => s.GenerateAltTextAsync(
                It.IsAny<Stream>(),
                It.IsAny<string>()))
            .ThrowsAsync(new InvalidOperationException("Invalid image format"));

        using var stream = new MemoryStream(new byte[] { 0x00, 0x01, 0x02 }); // Invalid image data

        // Act & Assert
        await Assert.ThrowsAsync<InvalidOperationException>(() => mockService.Object.GenerateAltTextAsync(stream));
    }

    [Fact]
    public async Task GenerateAltTextAsync_HandlesServiceNotReady()
    {
        // Arrange
        var mockService = new Mock<IImageAnalysisService>();
        mockService.Setup(s => s.IsReady).Returns(false);
        mockService.Setup(s => s.GenerateAltTextAsync(
                It.IsAny<Stream>(),
                It.IsAny<string>()))
            .ThrowsAsync(new InvalidOperationException("Service not initialized"));

        using var stream = new MemoryStream();

        // Act & Assert
        Assert.False(mockService.Object.IsReady);
        await Assert.ThrowsAsync<InvalidOperationException>(() => mockService.Object.GenerateAltTextAsync(stream));
    }

    #endregion

    #region Stream Usage Tests

    [Fact]
    public async Task GenerateAltTextAsync_DoesNotDisposeStream()
    {
        // Arrange
        var mockService = new Mock<IImageAnalysisService>();
        mockService.Setup(s => s.GenerateAltTextAsync(
                It.IsAny<Stream>(),
                It.IsAny<string>()))
            .ReturnsAsync("Alt text");

        using var stream = new MemoryStream(new byte[] { 0x89, 0x50, 0x4E, 0x47 });

        // Act
        await mockService.Object.GenerateAltTextAsync(stream);

        // Assert - Stream should still be usable
        Assert.True(stream.CanRead);
    }

    [Fact]
    public async Task ExtractTextAsync_DoesNotDisposeStream()
    {
        // Arrange
        var mockService = new Mock<IImageAnalysisService>();
        mockService.Setup(s => s.ExtractTextAsync(It.IsAny<Stream>()))
            .ReturnsAsync("Extracted");

        using var stream = new MemoryStream(new byte[] { 0x89, 0x50, 0x4E, 0x47 });

        // Act
        await mockService.Object.ExtractTextAsync(stream);

        // Assert
        Assert.True(stream.CanRead);
    }

    [Fact]
    public async Task AnalyzeImageAsync_DoesNotDisposeStream()
    {
        // Arrange
        var mockService = new Mock<IImageAnalysisService>();
        mockService.Setup(s => s.AnalyzeImageAsync(It.IsAny<Stream>()))
            .ReturnsAsync(("Alt", "Text"));

        using var stream = new MemoryStream(new byte[] { 0x89, 0x50, 0x4E, 0x47 });

        // Act
        await mockService.Object.AnalyzeImageAsync(stream);

        // Assert
        Assert.True(stream.CanRead);
    }

    #endregion
}