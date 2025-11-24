using Mostlylucid.LlmSeoMetadata.Models;

namespace Mostlylucid.LlmSeoMetadata.Test.Models;

public class ContentInputTests
{
    [Fact]
    public void GetCacheKey_WithExplicitCacheKey_ReturnsExplicitKey()
    {
        var content = new ContentInput
        {
            Title = "Test Title",
            Content = "Test Content",
            CacheKey = "my-custom-key"
        };

        var result = content.GetCacheKey();

        Assert.Equal("my-custom-key", result);
    }

    [Fact]
    public void GetCacheKey_WithoutExplicitCacheKey_GeneratesHashBasedKey()
    {
        var content = new ContentInput
        {
            Title = "Test Title",
            Content = "Test Content"
        };

        var result = content.GetCacheKey();

        Assert.NotNull(result);
        Assert.Equal(16, result.Length);
    }

    [Fact]
    public void GetCacheKey_SameContent_GeneratesSameKey()
    {
        var content1 = new ContentInput
        {
            Title = "Test Title",
            Content = "Test Content",
            ContentType = SeoContentType.Article
        };

        var content2 = new ContentInput
        {
            Title = "Test Title",
            Content = "Test Content",
            ContentType = SeoContentType.Article
        };

        var key1 = content1.GetCacheKey();
        var key2 = content2.GetCacheKey();

        Assert.Equal(key1, key2);
    }

    [Fact]
    public void GetCacheKey_DifferentContent_GeneratesDifferentKey()
    {
        var content1 = new ContentInput
        {
            Title = "Test Title 1",
            Content = "Test Content"
        };

        var content2 = new ContentInput
        {
            Title = "Test Title 2",
            Content = "Test Content"
        };

        var key1 = content1.GetCacheKey();
        var key2 = content2.GetCacheKey();

        Assert.NotEqual(key1, key2);
    }

    [Fact]
    public void ContentInput_DefaultContentType_IsArticle()
    {
        var content = new ContentInput
        {
            Title = "Test",
            Content = "Test"
        };

        Assert.Equal(SeoContentType.Article, content.ContentType);
    }

    [Fact]
    public void ContentInput_DefaultLanguage_IsEnglish()
    {
        var content = new ContentInput
        {
            Title = "Test",
            Content = "Test"
        };

        Assert.Equal("en", content.Language);
    }
}