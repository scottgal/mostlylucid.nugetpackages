using Microsoft.AspNetCore.Razor.TagHelpers;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Mostlylucid.LlmAltText.Data;
using Mostlylucid.LlmAltText.Models;
using Mostlylucid.LlmAltText.Services;
using Mostlylucid.LlmAltText.TagHelpers;
using RichardSzalay.MockHttp;

namespace Mostlylucid.LlmAltText.Test.TagHelpers;

/// <summary>
///     Tests for AutoAltTextTagHelper
/// </summary>
public class AutoAltTextTagHelperTests
{
    private readonly Mock<IImageAnalysisService> _mockImageService;
    private readonly Mock<IAltTextRepository> _mockRepository;
    private readonly IMemoryCache _memoryCache;
    private readonly MockHttpMessageHandler _mockHttp;
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly ILogger<AutoAltTextTagHelper> _logger;

    public AutoAltTextTagHelperTests()
    {
        _mockImageService = new Mock<IImageAnalysisService>();
        _mockRepository = new Mock<IAltTextRepository>();
        _memoryCache = new MemoryCache(new MemoryCacheOptions());
        _mockHttp = new MockHttpMessageHandler();
        _logger = NullLogger<AutoAltTextTagHelper>.Instance;

        // Create HttpClientFactory that returns our mock handler
        var mockFactory = new Mock<IHttpClientFactory>();
        mockFactory.Setup(f => f.CreateClient(It.IsAny<string>()))
            .Returns(() => _mockHttp.ToHttpClient());
        _httpClientFactory = mockFactory.Object;
    }

    private AutoAltTextTagHelper CreateTagHelper(AltTextOptions? options = null)
    {
        options ??= new AltTextOptions { EnableTagHelper = true };
        var optionsWrapper = Options.Create(options);

        return new AutoAltTextTagHelper(
            _mockImageService.Object,
            _memoryCache,
            _httpClientFactory,
            _logger,
            optionsWrapper,
            _mockRepository.Object);
    }

    private static TagHelperContext CreateTagHelperContext(TagHelperAttributeList? attributes = null)
    {
        attributes ??= new TagHelperAttributeList();
        return new TagHelperContext(
            tagName: "img",
            allAttributes: attributes,
            items: new Dictionary<object, object>(),
            uniqueId: Guid.NewGuid().ToString());
    }

    private static TagHelperOutput CreateTagHelperOutput(TagHelperAttributeList? attributes = null)
    {
        attributes ??= new TagHelperAttributeList();
        return new TagHelperOutput(
            "img",
            attributes,
            (useCachedResult, encoder) => Task.FromResult<TagHelperContent>(new DefaultTagHelperContent()));
    }

    #region Skip Behavior Tests

    [Fact]
    public async Task ProcessAsync_SkipsWhenTagHelperDisabled()
    {
        // Arrange
        var options = new AltTextOptions { EnableTagHelper = false };
        var tagHelper = CreateTagHelper(options);
        var context = CreateTagHelperContext();
        var output = CreateTagHelperOutput(new TagHelperAttributeList { { "src", "https://example.com/image.jpg" } });

        // Act
        await tagHelper.ProcessAsync(context, output);

        // Assert - No alt attribute should be added
        Assert.Null(output.Attributes["alt"]);
        _mockImageService.Verify(s => s.AnalyzeWithClassificationAsync(It.IsAny<Stream>()), Times.Never);
    }

    [Fact]
    public async Task ProcessAsync_SkipsWhenDataSkipAltIsTrue()
    {
        // Arrange
        var tagHelper = CreateTagHelper();
        tagHelper.SkipAlt = true;
        var context = CreateTagHelperContext();
        var output = CreateTagHelperOutput(new TagHelperAttributeList { { "src", "https://example.com/image.jpg" } });

        // Act
        await tagHelper.ProcessAsync(context, output);

        // Assert
        Assert.Null(output.Attributes["alt"]);
        _mockImageService.Verify(s => s.AnalyzeWithClassificationAsync(It.IsAny<Stream>()), Times.Never);
    }

    [Fact]
    public async Task ProcessAsync_SkipsWhenAltAttributeExists()
    {
        // Arrange
        var tagHelper = CreateTagHelper();
        var context = CreateTagHelperContext(new TagHelperAttributeList
        {
            { "src", "https://example.com/image.jpg" },
            { "alt", "Existing alt text" }
        });
        var output = CreateTagHelperOutput(new TagHelperAttributeList { { "src", "https://example.com/image.jpg" } });

        // Act
        await tagHelper.ProcessAsync(context, output);

        // Assert
        _mockImageService.Verify(s => s.AnalyzeWithClassificationAsync(It.IsAny<Stream>()), Times.Never);
    }

    [Fact]
    public async Task ProcessAsync_SkipsWhenAltAttributeIsEmptyString()
    {
        // Arrange - Empty alt is intentional for decorative images per accessibility standards
        var tagHelper = CreateTagHelper();
        var context = CreateTagHelperContext(new TagHelperAttributeList
        {
            { "src", "https://example.com/image.jpg" },
            { "alt", "" }
        });
        var output = CreateTagHelperOutput(new TagHelperAttributeList { { "src", "https://example.com/image.jpg" } });

        // Act
        await tagHelper.ProcessAsync(context, output);

        // Assert - Should not generate alt text for intentionally empty alt
        _mockImageService.Verify(s => s.AnalyzeWithClassificationAsync(It.IsAny<Stream>()), Times.Never);
    }

    [Fact]
    public async Task ProcessAsync_SkipsWhenSrcIsEmpty()
    {
        // Arrange
        var tagHelper = CreateTagHelper();
        var context = CreateTagHelperContext();
        var output = CreateTagHelperOutput(new TagHelperAttributeList { { "src", "" } });

        // Act
        await tagHelper.ProcessAsync(context, output);

        // Assert
        Assert.Null(output.Attributes["alt"]);
    }

    [Fact]
    public async Task ProcessAsync_SkipsWhenSrcIsMissing()
    {
        // Arrange
        var tagHelper = CreateTagHelper();
        var context = CreateTagHelperContext();
        var output = CreateTagHelperOutput();

        // Act
        await tagHelper.ProcessAsync(context, output);

        // Assert
        Assert.Null(output.Attributes["alt"]);
    }

    [Theory]
    [InlineData("data:image/png;base64,iVBORw0KGgo=")]
    [InlineData("blob:https://example.com/12345")]
    public async Task ProcessAsync_SkipsDataAndBlobUrls(string src)
    {
        // Arrange
        var options = new AltTextOptions
        {
            EnableTagHelper = true,
            SkipSrcPrefixes = new List<string> { "data:", "blob:" }
        };
        var tagHelper = CreateTagHelper(options);
        var context = CreateTagHelperContext();
        var output = CreateTagHelperOutput(new TagHelperAttributeList { { "src", src } });

        // Act
        await tagHelper.ProcessAsync(context, output);

        // Assert
        Assert.Null(output.Attributes["alt"]);
    }

    [Fact]
    public async Task ProcessAsync_SkipsDisallowedDomains()
    {
        // Arrange
        var options = new AltTextOptions
        {
            EnableTagHelper = true,
            AllowedImageDomains = new List<string> { "trusted.com", "example.org" }
        };
        var tagHelper = CreateTagHelper(options);
        var context = CreateTagHelperContext();
        var output = CreateTagHelperOutput(new TagHelperAttributeList { { "src", "https://untrusted.com/image.jpg" } });

        // Act
        await tagHelper.ProcessAsync(context, output);

        // Assert
        Assert.Null(output.Attributes["alt"]);
    }

    [Fact]
    public async Task ProcessAsync_AllowsAllowedDomains()
    {
        // Arrange
        var options = new AltTextOptions
        {
            EnableTagHelper = true,
            EnableDatabase = false,
            AllowedImageDomains = new List<string> { "trusted.com" }
        };
        var tagHelper = CreateTagHelper(options);
        var context = CreateTagHelperContext();
        var output = CreateTagHelperOutput(new TagHelperAttributeList { { "src", "https://trusted.com/image.jpg" } });

        // Setup mock HTTP response
        var imageBytes = CreateValidPngBytes();
        _mockHttp.When("https://trusted.com/image.jpg")
            .Respond("image/png", new MemoryStream(imageBytes));

        _mockImageService.Setup(s => s.AnalyzeWithClassificationAsync(It.IsAny<Stream>()))
            .ReturnsAsync(new ImageAnalysisResult
            {
                AltText = "A beautiful landscape",
                ExtractedText = "",
                ContentType = ImageContentType.Photograph,
                ContentTypeConfidence = 0.9
            });

        // Act
        await tagHelper.ProcessAsync(context, output);

        // Assert
        var altAttr = output.Attributes["alt"];
        Assert.NotNull(altAttr);
        Assert.Equal("A beautiful landscape", altAttr.Value);
    }

    #endregion

    #region Cache Behavior Tests

    [Fact]
    public async Task ProcessAsync_UsesCachedValueFromMemory()
    {
        // Arrange
        var options = new AltTextOptions { EnableTagHelper = true, CacheDurationMinutes = 60 };
        var tagHelper = CreateTagHelper(options);
        var context = CreateTagHelperContext();
        var output = CreateTagHelperOutput(new TagHelperAttributeList { { "src", "https://example.com/cached.jpg" } });

        // Pre-populate cache
        var cacheKey = $"alttext_{AltTextRepository.ComputeHash("https://example.com/cached.jpg")}";
        _memoryCache.Set(cacheKey, "Cached alt text");

        // Act
        await tagHelper.ProcessAsync(context, output);

        // Assert - Should use cached value without calling service
        var altAttr = output.Attributes["alt"];
        Assert.NotNull(altAttr);
        Assert.Equal("Cached alt text", altAttr.Value);
        _mockImageService.Verify(s => s.AnalyzeWithClassificationAsync(It.IsAny<Stream>()), Times.Never);
    }

    [Fact]
    public async Task ProcessAsync_UsesDatabaseCacheWhenMemoryCacheMisses()
    {
        // Arrange
        var options = new AltTextOptions
        {
            EnableTagHelper = true,
            EnableDatabase = true
        };
        var tagHelper = CreateTagHelper(options);
        var context = CreateTagHelperContext();
        var output = CreateTagHelperOutput(new TagHelperAttributeList { { "src", "https://example.com/db-cached.jpg" } });

        // Setup database to return cached entry
        var dbEntry = new ImageAltTextEntry
        {
            Id = 1,
            SourceHash = AltTextRepository.ComputeHash("https://example.com/db-cached.jpg"),
            ImageSource = "https://example.com/db-cached.jpg",
            AltText = "Database cached alt text",
            ExtractedText = "",
            ContentType = "Photograph",
            ContentTypeConfidence = 0.85
        };
        _mockRepository.Setup(r => r.GetBySourceAsync("https://example.com/db-cached.jpg"))
            .ReturnsAsync(dbEntry);

        // Act
        await tagHelper.ProcessAsync(context, output);

        // Assert
        var altAttr = output.Attributes["alt"];
        Assert.NotNull(altAttr);
        Assert.Equal("Database cached alt text", altAttr.Value);
        _mockRepository.Verify(r => r.IncrementUsageAsync(1), Times.Once);
        _mockImageService.Verify(s => s.AnalyzeWithClassificationAsync(It.IsAny<Stream>()), Times.Never);
    }

    #endregion

    #region Image Fetching Tests

    [Fact]
    public async Task ProcessAsync_FetchesImageAndGeneratesAltText()
    {
        // Arrange
        var options = new AltTextOptions { EnableTagHelper = true, EnableDatabase = false };
        var tagHelper = CreateTagHelper(options);
        var context = CreateTagHelperContext();
        var output = CreateTagHelperOutput(new TagHelperAttributeList { { "src", "https://example.com/new-image.jpg" } });

        var imageBytes = CreateValidPngBytes();
        _mockHttp.When("https://example.com/new-image.jpg")
            .Respond("image/jpeg", new MemoryStream(imageBytes));

        _mockImageService.Setup(s => s.AnalyzeWithClassificationAsync(It.IsAny<Stream>()))
            .ReturnsAsync(new ImageAnalysisResult
            {
                AltText = "Generated alt text for image",
                ExtractedText = "Some text in image",
                ContentType = ImageContentType.Document,
                ContentTypeConfidence = 0.8,
                HasSignificantText = true
            });

        // Act
        await tagHelper.ProcessAsync(context, output);

        // Assert
        var altAttr = output.Attributes["alt"];
        Assert.NotNull(altAttr);
        Assert.Equal("Generated alt text for image", altAttr.Value);
    }

    [Fact]
    public async Task ProcessAsync_HandlesHttpErrorGracefully()
    {
        // Arrange
        var options = new AltTextOptions { EnableTagHelper = true };
        var tagHelper = CreateTagHelper(options);
        var context = CreateTagHelperContext();
        var output = CreateTagHelperOutput(new TagHelperAttributeList { { "src", "https://example.com/not-found.jpg" } });

        _mockHttp.When("https://example.com/not-found.jpg")
            .Respond(System.Net.HttpStatusCode.NotFound);

        // Act - Should not throw
        await tagHelper.ProcessAsync(context, output);

        // Assert - No alt attribute added on error
        Assert.Null(output.Attributes["alt"]);
    }

    [Fact]
    public async Task ProcessAsync_SkipsNonImageContentType()
    {
        // Arrange
        var options = new AltTextOptions { EnableTagHelper = true };
        var tagHelper = CreateTagHelper(options);
        var context = CreateTagHelperContext();
        var output = CreateTagHelperOutput(new TagHelperAttributeList { { "src", "https://example.com/document.pdf" } });

        _mockHttp.When("https://example.com/document.pdf")
            .Respond("application/pdf", new MemoryStream(new byte[] { 0x25, 0x50, 0x44, 0x46 }));

        // Act
        await tagHelper.ProcessAsync(context, output);

        // Assert - Should not call image service for non-image content
        Assert.Null(output.Attributes["alt"]);
    }

    [Fact]
    public async Task ProcessAsync_HandlesRelativePathsGracefully()
    {
        // Arrange
        var options = new AltTextOptions { EnableTagHelper = true };
        var tagHelper = CreateTagHelper(options);
        var context = CreateTagHelperContext();
        var output = CreateTagHelperOutput(new TagHelperAttributeList { { "src", "/images/local.jpg" } });

        // Act - Should not throw for relative paths
        await tagHelper.ProcessAsync(context, output);

        // Assert - Currently relative paths are not supported
        Assert.Null(output.Attributes["alt"]);
    }

    #endregion

    #region Database Storage Tests

    [Fact]
    public async Task ProcessAsync_SavesResultToDatabase()
    {
        // Arrange
        var options = new AltTextOptions
        {
            EnableTagHelper = true,
            EnableDatabase = true
        };
        var tagHelper = CreateTagHelper(options);
        var context = CreateTagHelperContext();
        var output = CreateTagHelperOutput(new TagHelperAttributeList { { "src", "https://example.com/save-me.jpg" } });

        // No cached entry
        _mockRepository.Setup(r => r.GetBySourceAsync(It.IsAny<string>()))
            .ReturnsAsync((ImageAltTextEntry?)null);

        var imageBytes = CreateValidPngBytes();
        _mockHttp.When("https://example.com/save-me.jpg")
            .Respond("image/jpeg", new MemoryStream(imageBytes));

        _mockImageService.Setup(s => s.AnalyzeWithClassificationAsync(It.IsAny<Stream>()))
            .ReturnsAsync(new ImageAnalysisResult
            {
                AltText = "New generated alt text",
                ExtractedText = "",
                ContentType = ImageContentType.Photograph,
                ContentTypeConfidence = 0.95,
                HasSignificantText = false
            });

        // Act
        await tagHelper.ProcessAsync(context, output);

        // Assert
        _mockRepository.Verify(r => r.SaveAsync(It.Is<ImageAltTextEntry>(e =>
            e.AltText == "New generated alt text" &&
            e.ImageSource == "https://example.com/save-me.jpg" &&
            e.ContentType == "Photograph"
        )), Times.Once);
    }

    #endregion

    #region Error Handling Tests

    [Fact]
    public async Task ProcessAsync_HandlesServiceExceptionGracefully()
    {
        // Arrange
        var options = new AltTextOptions { EnableTagHelper = true, EnableDatabase = false };
        var tagHelper = CreateTagHelper(options);
        var context = CreateTagHelperContext();
        var output = CreateTagHelperOutput(new TagHelperAttributeList { { "src", "https://example.com/error.jpg" } });

        var imageBytes = CreateValidPngBytes();
        _mockHttp.When("https://example.com/error.jpg")
            .Respond("image/jpeg", new MemoryStream(imageBytes));

        _mockImageService.Setup(s => s.AnalyzeWithClassificationAsync(It.IsAny<Stream>()))
            .ThrowsAsync(new InvalidOperationException("Service not ready"));

        // Act - Should not throw
        await tagHelper.ProcessAsync(context, output);

        // Assert - No alt attribute added on error, page still renders
        Assert.Null(output.Attributes["alt"]);
    }

    [Fact]
    public async Task ProcessAsync_HandlesTimeoutGracefully()
    {
        // Arrange
        var options = new AltTextOptions { EnableTagHelper = true };
        var tagHelper = CreateTagHelper(options);
        var context = CreateTagHelperContext();
        var output = CreateTagHelperOutput(new TagHelperAttributeList { { "src", "https://slow.example.com/image.jpg" } });

        _mockHttp.When("https://slow.example.com/image.jpg")
            .Throw(new TaskCanceledException("Request timed out"));

        // Act - Should not throw
        await tagHelper.ProcessAsync(context, output);

        // Assert
        Assert.Null(output.Attributes["alt"]);
    }

    #endregion

    #region Integration Tests

    [Fact]
    public async Task ProcessAsync_CompletesFullWorkflow()
    {
        // Arrange - Full workflow: fetch -> analyze -> cache -> save
        var options = new AltTextOptions
        {
            EnableTagHelper = true,
            EnableDatabase = true,
            CacheDurationMinutes = 30
        };
        var tagHelper = CreateTagHelper(options);
        var context = CreateTagHelperContext();
        var output = CreateTagHelperOutput(new TagHelperAttributeList { { "src", "https://example.com/full-workflow.jpg" } });

        _mockRepository.Setup(r => r.GetBySourceAsync(It.IsAny<string>()))
            .ReturnsAsync((ImageAltTextEntry?)null);

        var imageBytes = CreateValidPngBytes();
        _mockHttp.When("https://example.com/full-workflow.jpg")
            .Respond("image/jpeg", new MemoryStream(imageBytes));

        var analysisResult = new ImageAnalysisResult
        {
            AltText = "A professional headshot of a person wearing business attire",
            ExtractedText = "",
            ContentType = ImageContentType.Photograph,
            ContentTypeConfidence = 0.92,
            HasSignificantText = false
        };
        _mockImageService.Setup(s => s.AnalyzeWithClassificationAsync(It.IsAny<Stream>()))
            .ReturnsAsync(analysisResult);

        // Act
        await tagHelper.ProcessAsync(context, output);

        // Assert - Alt text was set
        var altAttr = output.Attributes["alt"];
        Assert.NotNull(altAttr);
        Assert.Equal("A professional headshot of a person wearing business attire", altAttr.Value);

        // Assert - Result was saved to database
        _mockRepository.Verify(r => r.SaveAsync(It.IsAny<ImageAltTextEntry>()), Times.Once);

        // Assert - Result is now in memory cache
        var cacheKey = $"alttext_{AltTextRepository.ComputeHash("https://example.com/full-workflow.jpg")}";
        Assert.True(_memoryCache.TryGetValue(cacheKey, out string? cachedValue));
        Assert.Equal("A professional headshot of a person wearing business attire", cachedValue);
    }

    [Fact]
    public async Task ProcessAsync_SecondCallUsesCache()
    {
        // Arrange
        var options = new AltTextOptions
        {
            EnableTagHelper = true,
            EnableDatabase = false,
            CacheDurationMinutes = 60
        };
        var tagHelper1 = CreateTagHelper(options);
        var tagHelper2 = CreateTagHelper(options);
        var src = "https://example.com/cached-test.jpg";

        // First call
        var context1 = CreateTagHelperContext();
        var output1 = CreateTagHelperOutput(new TagHelperAttributeList { { "src", src } });

        var imageBytes = CreateValidPngBytes();
        _mockHttp.When(src)
            .Respond("image/jpeg", new MemoryStream(imageBytes));

        _mockImageService.Setup(s => s.AnalyzeWithClassificationAsync(It.IsAny<Stream>()))
            .ReturnsAsync(new ImageAnalysisResult
            {
                AltText = "First call result",
                ExtractedText = "",
                ContentType = ImageContentType.Photograph,
                ContentTypeConfidence = 0.85
            });

        await tagHelper1.ProcessAsync(context1, output1);

        // Second call
        var context2 = CreateTagHelperContext();
        var output2 = CreateTagHelperOutput(new TagHelperAttributeList { { "src", src } });

        await tagHelper2.ProcessAsync(context2, output2);

        // Assert - Service was only called once
        _mockImageService.Verify(s => s.AnalyzeWithClassificationAsync(It.IsAny<Stream>()), Times.Once);

        // Both outputs should have the same alt text
        Assert.Equal("First call result", output1.Attributes["alt"]?.Value);
        Assert.Equal("First call result", output2.Attributes["alt"]?.Value);
    }

    #endregion

    #region Helper Methods

    /// <summary>
    ///     Creates a minimal valid PNG file as bytes
    /// </summary>
    private static byte[] CreateValidPngBytes()
    {
        // Minimal valid 1x1 white PNG
        return new byte[]
        {
            0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A, // PNG signature
            0x00, 0x00, 0x00, 0x0D, // IHDR length
            0x49, 0x48, 0x44, 0x52, // IHDR
            0x00, 0x00, 0x00, 0x01, // width: 1
            0x00, 0x00, 0x00, 0x01, // height: 1
            0x08, 0x02, // bit depth: 8, color type: 2 (RGB)
            0x00, 0x00, 0x00, // compression, filter, interlace
            0x90, 0x77, 0x53, 0xDE, // CRC
            0x00, 0x00, 0x00, 0x0C, // IDAT length
            0x49, 0x44, 0x41, 0x54, // IDAT
            0x08, 0xD7, 0x63, 0xF8, 0xFF, 0xFF, 0xFF, 0x00, // compressed data
            0x05, 0xFE, 0x02, 0xFE, // CRC
            0x00, 0x00, 0x00, 0x00, // IEND length
            0x49, 0x45, 0x4E, 0x44, // IEND
            0xAE, 0x42, 0x60, 0x82  // CRC
        };
    }

    #endregion
}
