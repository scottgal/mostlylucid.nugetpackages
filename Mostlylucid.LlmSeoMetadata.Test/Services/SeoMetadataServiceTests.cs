using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Mostlylucid.LlmSeoMetadata.Models;
using Mostlylucid.LlmSeoMetadata.Services;

namespace Mostlylucid.LlmSeoMetadata.Test.Services;

public class SeoMetadataServiceTests
{
    private readonly SeoMetadataOptions _defaultOptions;
    private readonly IMemoryCache _memoryCache;
    private readonly Mock<ILogger<OllamaSeoMetadataService>> _mockLogger;

    public SeoMetadataServiceTests()
    {
        _mockLogger = new Mock<ILogger<OllamaSeoMetadataService>>();
        _memoryCache = new MemoryCache(new MemoryCacheOptions());
        _defaultOptions = new SeoMetadataOptions
        {
            Enabled = true,
            OllamaEndpoint = "http://localhost:11434",
            Model = "llama3.2:3b"
        };
    }

    private OllamaSeoMetadataService CreateService(SeoMetadataOptions? options = null)
    {
        var opts = Options.Create(options ?? _defaultOptions);
        return new OllamaSeoMetadataService(opts, _memoryCache, _mockLogger.Object);
    }

    [Fact]
    public void IsReady_WhenEnabledAndEndpointSet_ReturnsTrue()
    {
        var service = CreateService();

        Assert.True(service.IsReady);
    }

    [Fact]
    public void IsReady_WhenDisabled_ReturnsFalse()
    {
        var options = new SeoMetadataOptions
        {
            Enabled = false,
            OllamaEndpoint = "http://localhost:11434"
        };
        var service = CreateService(options);

        Assert.False(service.IsReady);
    }

    [Fact]
    public void IsReady_WhenNoEndpoint_ReturnsFalse()
    {
        var options = new SeoMetadataOptions
        {
            Enabled = true,
            OllamaEndpoint = ""
        };
        var service = CreateService(options);

        Assert.False(service.IsReady);
    }

    [Fact]
    public void GetStatistics_InitialValues_AreZero()
    {
        var service = CreateService();

        var stats = service.GetStatistics();

        Assert.Equal(0, stats.TotalRequests);
        Assert.Equal(0, stats.SuccessfulGenerations);
        Assert.Equal(0, stats.FailedGenerations);
        Assert.Equal(0, stats.CacheHits);
        Assert.Equal("llama3.2:3b", stats.Model);
    }

    [Fact]
    public async Task GetCachedMetadataAsync_NotInCache_ReturnsNull()
    {
        var service = CreateService();

        var result = await service.GetCachedMetadataAsync("non-existent-key");

        Assert.Null(result);
    }

    [Fact]
    public async Task CacheMetadataAsync_StoresInCache()
    {
        var service = CreateService();
        var metadata = new SeoMetadata
        {
            MetaDescription = "Test description"
        };

        await service.CacheMetadataAsync("test-key", metadata);
        var result = await service.GetCachedMetadataAsync("test-key");

        Assert.NotNull(result);
        Assert.Equal("Test description", result.MetaDescription);
    }

    [Fact]
    public async Task ClearCacheAsync_RemovesFromCache()
    {
        var service = CreateService();
        var metadata = new SeoMetadata { MetaDescription = "Test" };

        await service.CacheMetadataAsync("test-key", metadata);
        await service.ClearCacheAsync("test-key");
        var result = await service.GetCachedMetadataAsync("test-key");

        Assert.Null(result);
    }

    [Fact]
    public async Task GenerateMetadataAsync_WithCachedResult_ReturnsCachedData()
    {
        var service = CreateService();
        var metadata = new SeoMetadata
        {
            MetaDescription = "Cached description",
            GeneratedAt = DateTime.UtcNow.AddHours(-1)
        };

        var content = new ContentInput
        {
            Title = "Test",
            Content = "Test content",
            CacheKey = "cached-test"
        };

        await service.CacheMetadataAsync("cached-test", metadata);

        var request = new GenerationRequest
        {
            Content = content,
            UseCache = true
        };

        var result = await service.GenerateMetadataAsync(request);

        Assert.True(result.Success);
        Assert.True(result.FromCache);
        Assert.Equal("Cached description", result.Metadata?.MetaDescription);
    }

    [Fact]
    public async Task GenerateMetadataAsync_ForceRegenerate_SkipsCache()
    {
        var service = CreateService();
        var metadata = new SeoMetadata
        {
            MetaDescription = "Old cached description"
        };

        var content = new ContentInput
        {
            Title = "Test",
            Content = "Test content",
            CacheKey = "force-test"
        };

        await service.CacheMetadataAsync("force-test", metadata);

        var request = new GenerationRequest
        {
            Content = content,
            UseCache = true,
            ForceRegenerate = true
        };

        // This will fail because Ollama isn't running, but it won't use cache
        var result = await service.GenerateMetadataAsync(request);

        // The result won't be from cache (even though generation may fail)
        Assert.False(result.FromCache);
    }

    [Fact]
    public async Task GenerateMetadataAsync_IncrementsStatistics()
    {
        var service = CreateService();

        var request = new GenerationRequest
        {
            Content = new ContentInput
            {
                Title = "Test",
                Content = "Test content"
            }
        };

        // This will increment total requests even if it fails
        await service.GenerateMetadataAsync(request);

        var stats = service.GetStatistics();

        Assert.Equal(1, stats.TotalRequests);
    }

    [Fact]
    public async Task GenerateKeywordsAsync_WithNullResponse_ReturnsExistingTags()
    {
        // Create service that won't connect to Ollama
        var options = new SeoMetadataOptions
        {
            Enabled = false
        };
        var service = CreateService(options);

        var content = new ContentInput
        {
            Title = "Test",
            Content = "Test content",
            Tags = ["tag1", "tag2"]
        };

        var result = await service.GenerateKeywordsAsync(content);

        // Should return existing tags when service is not ready
        Assert.Equal(["tag1", "tag2"], result);
    }
}