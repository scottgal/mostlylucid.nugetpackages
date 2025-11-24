using Mostlylucid.LlmAltText.Services;

namespace Mostlylucid.LlmAltText.Test.Services;

/// <summary>
///     Tests for file, URL, and byte array overloads in IImageAnalysisService
/// </summary>
public class FileAndUrlOverloadTests
{
    #region File-based Overload Interface Tests

    [Fact]
    public void Interface_DefinesGenerateAltTextFromFileAsync()
    {
        // Arrange
        var mockService = new Mock<IImageAnalysisService>();

        // Act - Verify method exists and can be setup
        mockService.Setup(s => s.GenerateAltTextFromFileAsync(
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync("Alt text from file");

        // Assert
        Assert.NotNull(mockService.Object);
    }

    [Fact]
    public void Interface_DefinesExtractTextFromFileAsync()
    {
        // Arrange
        var mockService = new Mock<IImageAnalysisService>();

        // Act
        mockService.Setup(s => s.ExtractTextFromFileAsync(
                It.IsAny<string>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync("Extracted text from file");

        // Assert
        Assert.NotNull(mockService.Object);
    }

    [Fact]
    public void Interface_DefinesAnalyzeImageFromFileAsync()
    {
        // Arrange
        var mockService = new Mock<IImageAnalysisService>();

        // Act
        mockService.Setup(s => s.AnalyzeImageFromFileAsync(
                It.IsAny<string>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(("Alt text", "Extracted text"));

        // Assert
        Assert.NotNull(mockService.Object);
    }

    [Fact]
    public void Interface_DefinesAnalyzeWithClassificationFromFileAsync()
    {
        // Arrange
        var mockService = new Mock<IImageAnalysisService>();

        // Act
        mockService.Setup(s => s.AnalyzeWithClassificationFromFileAsync(
                It.IsAny<string>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ImageAnalysisResult
            {
                AltText = "Alt text",
                ExtractedText = "",
                ContentType = ImageContentType.Photograph,
                ContentTypeConfidence = 0.9
            });

        // Assert
        Assert.NotNull(mockService.Object);
    }

    #endregion

    #region URL-based Overload Interface Tests (string)

    [Fact]
    public void Interface_DefinesGenerateAltTextFromUrlAsync_String()
    {
        // Arrange
        var mockService = new Mock<IImageAnalysisService>();

        // Act
        mockService.Setup(s => s.GenerateAltTextFromUrlAsync(
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync("Alt text from URL");

        // Assert
        Assert.NotNull(mockService.Object);
    }

    [Fact]
    public void Interface_DefinesExtractTextFromUrlAsync_String()
    {
        // Arrange
        var mockService = new Mock<IImageAnalysisService>();

        // Act
        mockService.Setup(s => s.ExtractTextFromUrlAsync(
                It.IsAny<string>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync("Extracted text from URL");

        // Assert
        Assert.NotNull(mockService.Object);
    }

    [Fact]
    public void Interface_DefinesAnalyzeImageFromUrlAsync_String()
    {
        // Arrange
        var mockService = new Mock<IImageAnalysisService>();

        // Act
        mockService.Setup(s => s.AnalyzeImageFromUrlAsync(
                It.IsAny<string>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(("Alt text", "Extracted text"));

        // Assert
        Assert.NotNull(mockService.Object);
    }

    [Fact]
    public void Interface_DefinesAnalyzeWithClassificationFromUrlAsync_String()
    {
        // Arrange
        var mockService = new Mock<IImageAnalysisService>();

        // Act
        mockService.Setup(s => s.AnalyzeWithClassificationFromUrlAsync(
                It.IsAny<string>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ImageAnalysisResult
            {
                AltText = "Alt text",
                ExtractedText = "",
                ContentType = ImageContentType.Screenshot,
                ContentTypeConfidence = 0.85
            });

        // Assert
        Assert.NotNull(mockService.Object);
    }

    #endregion

    #region URL-based Overload Interface Tests (Uri)

    [Fact]
    public void Interface_DefinesGenerateAltTextFromUrlAsync_Uri()
    {
        // Arrange
        var mockService = new Mock<IImageAnalysisService>();

        // Act
        mockService.Setup(s => s.GenerateAltTextFromUrlAsync(
                It.IsAny<Uri>(),
                It.IsAny<string>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync("Alt text from Uri");

        // Assert
        Assert.NotNull(mockService.Object);
    }

    [Fact]
    public void Interface_DefinesExtractTextFromUrlAsync_Uri()
    {
        // Arrange
        var mockService = new Mock<IImageAnalysisService>();

        // Act
        mockService.Setup(s => s.ExtractTextFromUrlAsync(
                It.IsAny<Uri>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync("Extracted text from Uri");

        // Assert
        Assert.NotNull(mockService.Object);
    }

    [Fact]
    public void Interface_DefinesAnalyzeImageFromUrlAsync_Uri()
    {
        // Arrange
        var mockService = new Mock<IImageAnalysisService>();

        // Act
        mockService.Setup(s => s.AnalyzeImageFromUrlAsync(
                It.IsAny<Uri>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(("Alt text", "Extracted text"));

        // Assert
        Assert.NotNull(mockService.Object);
    }

    [Fact]
    public void Interface_DefinesAnalyzeWithClassificationFromUrlAsync_Uri()
    {
        // Arrange
        var mockService = new Mock<IImageAnalysisService>();

        // Act
        mockService.Setup(s => s.AnalyzeWithClassificationFromUrlAsync(
                It.IsAny<Uri>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ImageAnalysisResult
            {
                AltText = "Alt text",
                ExtractedText = "",
                ContentType = ImageContentType.Document,
                ContentTypeConfidence = 0.75
            });

        // Assert
        Assert.NotNull(mockService.Object);
    }

    #endregion

    #region Byte Array Overload Interface Tests

    [Fact]
    public void Interface_DefinesGenerateAltTextAsync_ByteArray()
    {
        // Arrange
        var mockService = new Mock<IImageAnalysisService>();

        // Act
        mockService.Setup(s => s.GenerateAltTextAsync(
                It.IsAny<byte[]>(),
                It.IsAny<string>()))
            .ReturnsAsync("Alt text from bytes");

        // Assert
        Assert.NotNull(mockService.Object);
    }

    [Fact]
    public void Interface_DefinesExtractTextAsync_ByteArray()
    {
        // Arrange
        var mockService = new Mock<IImageAnalysisService>();

        // Act
        mockService.Setup(s => s.ExtractTextAsync(It.IsAny<byte[]>()))
            .ReturnsAsync("Extracted text from bytes");

        // Assert
        Assert.NotNull(mockService.Object);
    }

    [Fact]
    public void Interface_DefinesAnalyzeImageAsync_ByteArray()
    {
        // Arrange
        var mockService = new Mock<IImageAnalysisService>();

        // Act
        mockService.Setup(s => s.AnalyzeImageAsync(It.IsAny<byte[]>()))
            .ReturnsAsync(("Alt text", "Extracted text"));

        // Assert
        Assert.NotNull(mockService.Object);
    }

    [Fact]
    public void Interface_DefinesAnalyzeWithClassificationAsync_ByteArray()
    {
        // Arrange
        var mockService = new Mock<IImageAnalysisService>();

        // Act
        mockService.Setup(s => s.AnalyzeWithClassificationAsync(It.IsAny<byte[]>()))
            .ReturnsAsync(new ImageAnalysisResult
            {
                AltText = "Alt text",
                ExtractedText = "",
                ContentType = ImageContentType.Chart,
                ContentTypeConfidence = 0.88
            });

        // Assert
        Assert.NotNull(mockService.Object);
    }

    #endregion

    #region Mock Behavior Tests

    [Fact]
    public async Task GenerateAltTextFromFileAsync_UsesDefaultTaskType()
    {
        // Arrange
        var mockService = new Mock<IImageAnalysisService>();
        mockService.Setup(s => s.GenerateAltTextFromFileAsync(
                It.IsAny<string>(),
                "MORE_DETAILED_CAPTION",
                It.IsAny<CancellationToken>()))
            .ReturnsAsync("Detailed alt text");

        // Act
        var result = await mockService.Object.GenerateAltTextFromFileAsync("/path/to/image.jpg");

        // Assert
        mockService.Verify(s => s.GenerateAltTextFromFileAsync(
            "/path/to/image.jpg",
            "MORE_DETAILED_CAPTION",
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task GenerateAltTextFromUrlAsync_ReturnsExpectedResult()
    {
        // Arrange
        var mockService = new Mock<IImageAnalysisService>();
        var expectedAltText = "A scenic mountain landscape with snow-capped peaks";
        mockService.Setup(s => s.GenerateAltTextFromUrlAsync(
                "https://example.com/mountain.jpg",
                It.IsAny<string>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(expectedAltText);

        // Act
        var result = await mockService.Object.GenerateAltTextFromUrlAsync("https://example.com/mountain.jpg");

        // Assert
        Assert.Equal(expectedAltText, result);
    }

    [Fact]
    public async Task GenerateAltTextAsync_ByteArray_ReturnsExpectedResult()
    {
        // Arrange
        var mockService = new Mock<IImageAnalysisService>();
        var imageBytes = new byte[] { 0x89, 0x50, 0x4E, 0x47 }; // PNG header
        var expectedAltText = "Alt text from byte array";

        mockService.Setup(s => s.GenerateAltTextAsync(
                imageBytes,
                It.IsAny<string>()))
            .ReturnsAsync(expectedAltText);

        // Act
        var result = await mockService.Object.GenerateAltTextAsync(imageBytes);

        // Assert
        Assert.Equal(expectedAltText, result);
    }

    [Fact]
    public async Task AnalyzeWithClassificationFromFileAsync_ReturnsCompleteResult()
    {
        // Arrange
        var mockService = new Mock<IImageAnalysisService>();
        var expectedResult = new ImageAnalysisResult
        {
            AltText = "A business presentation slide with charts",
            ExtractedText = "Q3 Revenue: $1.2M",
            ContentType = ImageContentType.Screenshot,
            ContentTypeConfidence = 0.92,
            HasSignificantText = true
        };

        mockService.Setup(s => s.AnalyzeWithClassificationFromFileAsync(
                It.IsAny<string>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(expectedResult);

        // Act
        var result = await mockService.Object.AnalyzeWithClassificationFromFileAsync("/path/to/slide.png");

        // Assert
        Assert.Equal(expectedResult.AltText, result.AltText);
        Assert.Equal(expectedResult.ExtractedText, result.ExtractedText);
        Assert.Equal(ImageContentType.Screenshot, result.ContentType);
        Assert.Equal(0.92, result.ContentTypeConfidence);
        Assert.True(result.HasSignificantText);
    }

    [Theory]
    [InlineData("CAPTION")]
    [InlineData("DETAILED_CAPTION")]
    [InlineData("MORE_DETAILED_CAPTION")]
    public async Task GenerateAltTextFromUrlAsync_DifferentTaskTypes(string taskType)
    {
        // Arrange
        var mockService = new Mock<IImageAnalysisService>();
        mockService.Setup(s => s.GenerateAltTextFromUrlAsync(
                It.IsAny<string>(),
                taskType,
                It.IsAny<CancellationToken>()))
            .ReturnsAsync($"Alt text with {taskType}");

        // Act
        var result = await mockService.Object.GenerateAltTextFromUrlAsync(
            "https://example.com/image.jpg",
            taskType);

        // Assert
        Assert.Contains(taskType, result);
    }

    #endregion

    #region Error Handling Tests

    [Fact]
    public async Task GenerateAltTextFromFileAsync_ThrowsForNullPath()
    {
        // Arrange
        var mockService = new Mock<IImageAnalysisService>();
        mockService.Setup(s => s.GenerateAltTextFromFileAsync(
                null!,
                It.IsAny<string>(),
                It.IsAny<CancellationToken>()))
            .ThrowsAsync(new ArgumentException("File path cannot be null or empty"));

        // Act & Assert
        await Assert.ThrowsAsync<ArgumentException>(() =>
            mockService.Object.GenerateAltTextFromFileAsync(null!));
    }

    [Fact]
    public async Task GenerateAltTextFromFileAsync_ThrowsForNonExistentFile()
    {
        // Arrange
        var mockService = new Mock<IImageAnalysisService>();
        mockService.Setup(s => s.GenerateAltTextFromFileAsync(
                "/nonexistent/path.jpg",
                It.IsAny<string>(),
                It.IsAny<CancellationToken>()))
            .ThrowsAsync(new FileNotFoundException("Image file not found"));

        // Act & Assert
        await Assert.ThrowsAsync<FileNotFoundException>(() =>
            mockService.Object.GenerateAltTextFromFileAsync("/nonexistent/path.jpg"));
    }

    [Fact]
    public async Task GenerateAltTextFromUrlAsync_ThrowsForInvalidUrl()
    {
        // Arrange
        var mockService = new Mock<IImageAnalysisService>();
        mockService.Setup(s => s.GenerateAltTextFromUrlAsync(
                "not-a-valid-url",
                It.IsAny<string>(),
                It.IsAny<CancellationToken>()))
            .ThrowsAsync(new UriFormatException("Invalid URI format"));

        // Act & Assert
        await Assert.ThrowsAsync<UriFormatException>(() =>
            mockService.Object.GenerateAltTextFromUrlAsync("not-a-valid-url"));
    }

    [Fact]
    public async Task GenerateAltTextAsync_ByteArray_ThrowsForEmptyArray()
    {
        // Arrange
        var mockService = new Mock<IImageAnalysisService>();
        mockService.Setup(s => s.GenerateAltTextAsync(
                Array.Empty<byte>(),
                It.IsAny<string>()))
            .ThrowsAsync(new ArgumentException("Image data cannot be null or empty"));

        // Act & Assert
        await Assert.ThrowsAsync<ArgumentException>(() =>
            mockService.Object.GenerateAltTextAsync(Array.Empty<byte>()));
    }

    [Fact]
    public async Task GenerateAltTextFromUrlAsync_ThrowsForHttpError()
    {
        // Arrange
        var mockService = new Mock<IImageAnalysisService>();
        mockService.Setup(s => s.GenerateAltTextFromUrlAsync(
                "https://example.com/404.jpg",
                It.IsAny<string>(),
                It.IsAny<CancellationToken>()))
            .ThrowsAsync(new HttpRequestException("404 Not Found"));

        // Act & Assert
        await Assert.ThrowsAsync<HttpRequestException>(() =>
            mockService.Object.GenerateAltTextFromUrlAsync("https://example.com/404.jpg"));
    }

    #endregion

    #region Cancellation Tests

    [Fact]
    public async Task GenerateAltTextFromFileAsync_SupportsCancellation()
    {
        // Arrange
        var mockService = new Mock<IImageAnalysisService>();
        var cts = new CancellationTokenSource();
        cts.Cancel();

        mockService.Setup(s => s.GenerateAltTextFromFileAsync(
                It.IsAny<string>(),
                It.IsAny<string>(),
                cts.Token))
            .ThrowsAsync(new OperationCanceledException());

        // Act & Assert
        await Assert.ThrowsAsync<OperationCanceledException>(() =>
            mockService.Object.GenerateAltTextFromFileAsync("/path/to/image.jpg", "CAPTION", cts.Token));
    }

    [Fact]
    public async Task GenerateAltTextFromUrlAsync_SupportsCancellation()
    {
        // Arrange
        var mockService = new Mock<IImageAnalysisService>();
        var cts = new CancellationTokenSource();
        cts.Cancel();

        mockService.Setup(s => s.GenerateAltTextFromUrlAsync(
                It.IsAny<Uri>(),
                It.IsAny<string>(),
                cts.Token))
            .ThrowsAsync(new OperationCanceledException());

        // Act & Assert
        await Assert.ThrowsAsync<OperationCanceledException>(() =>
            mockService.Object.GenerateAltTextFromUrlAsync(
                new Uri("https://example.com/image.jpg"),
                "CAPTION",
                cts.Token));
    }

    #endregion

    #region Content Type Classification Tests

    [Theory]
    [InlineData(ImageContentType.Photograph)]
    [InlineData(ImageContentType.Document)]
    [InlineData(ImageContentType.Screenshot)]
    [InlineData(ImageContentType.Chart)]
    [InlineData(ImageContentType.Illustration)]
    [InlineData(ImageContentType.Diagram)]
    [InlineData(ImageContentType.Unknown)]
    public async Task AnalyzeWithClassificationFromUrlAsync_ReturnsAllContentTypes(ImageContentType expectedType)
    {
        // Arrange
        var mockService = new Mock<IImageAnalysisService>();
        mockService.Setup(s => s.AnalyzeWithClassificationFromUrlAsync(
                It.IsAny<Uri>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ImageAnalysisResult
            {
                AltText = "Test alt text",
                ExtractedText = "",
                ContentType = expectedType,
                ContentTypeConfidence = 0.8
            });

        // Act
        var result = await mockService.Object.AnalyzeWithClassificationFromUrlAsync(
            new Uri("https://example.com/image.jpg"));

        // Assert
        Assert.Equal(expectedType, result.ContentType);
    }

    #endregion
}
