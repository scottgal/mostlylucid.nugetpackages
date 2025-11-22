using Mostlylucid.LlmSeoMetadata.Models;

namespace Mostlylucid.LlmSeoMetadata.Test.Models;

public class SeoMetadataOptionsTests
{
    [Fact]
    public void SeoMetadataOptions_DefaultValues_AreCorrect()
    {
        var options = new SeoMetadataOptions();

        Assert.True(options.Enabled);
        Assert.Equal("http://localhost:11434", options.OllamaEndpoint);
        Assert.Equal("llama3.2:3b", options.Model);
        Assert.Equal(0.3f, options.Temperature);
        Assert.Equal(512, options.MaxTokens);
        Assert.Equal(60, options.TimeoutSeconds);
        Assert.Equal(160, options.MaxMetaDescriptionLength);
        Assert.Equal(300, options.MaxOgDescriptionLength);
        Assert.Equal(TimeSpan.FromHours(24), options.CacheDuration);
        Assert.Equal("en", options.DefaultLanguage);
        Assert.Equal("summary_large_image", options.TwitterCardType);
        Assert.True(options.EnableDesignTimeGeneration);
        Assert.True(options.EnableRuntimeSuggestions);
        Assert.False(options.EnableDiagnosticLogging);
    }

    [Fact]
    public void SeoCacheOptions_DefaultValues_AreCorrect()
    {
        var options = new SeoCacheOptions();

        Assert.False(options.Enabled);
        Assert.Equal("Data Source=data/seometadata.db", options.ConnectionString);
        Assert.Equal(TimeSpan.FromDays(30), options.CacheExpiration);
        Assert.True(options.EnableCleanup);
        Assert.Equal(TimeSpan.FromDays(1), options.CleanupInterval);
    }

    [Theory]
    [InlineData(SeoContentType.Article)]
    [InlineData(SeoContentType.BlogPosting)]
    [InlineData(SeoContentType.Product)]
    [InlineData(SeoContentType.NewsArticle)]
    [InlineData(SeoContentType.Event)]
    [InlineData(SeoContentType.Recipe)]
    [InlineData(SeoContentType.FAQPage)]
    [InlineData(SeoContentType.HowTo)]
    public void SeoContentType_AllTypesAreDefined(SeoContentType contentType)
    {
        Assert.True(Enum.IsDefined(typeof(SeoContentType), contentType));
    }
}
