using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Mostlylucid.LlmSeoMetadata.Extensions;
using Mostlylucid.LlmSeoMetadata.Models;
using Mostlylucid.LlmSeoMetadata.Services;

namespace Mostlylucid.LlmSeoMetadata.Test.Extensions;

public class ServiceCollectionExtensionsTests
{
    [Fact]
    public void AddSeoMetadata_RegistersRequiredServices()
    {
        var services = new ServiceCollection();
        services.AddLogging();

        services.AddSeoMetadata();

        var provider = services.BuildServiceProvider();
        var seoService = provider.GetService<ISeoMetadataService>();

        Assert.NotNull(seoService);
    }

    [Fact]
    public void AddSeoMetadata_ConfiguresOptions()
    {
        var services = new ServiceCollection();
        services.AddLogging();

        services.AddSeoMetadata(options =>
        {
            options.OllamaEndpoint = "http://custom:11434";
            options.Model = "custom-model";
            options.SiteName = "Test Site";
        });

        var provider = services.BuildServiceProvider();
        var options = provider.GetRequiredService<IOptions<SeoMetadataOptions>>().Value;

        Assert.Equal("http://custom:11434", options.OllamaEndpoint);
        Assert.Equal("custom-model", options.Model);
        Assert.Equal("Test Site", options.SiteName);
    }

    [Fact]
    public void AddSeoMetadata_DefaultOptions_AreApplied()
    {
        var services = new ServiceCollection();
        services.AddLogging();

        services.AddSeoMetadata();

        var provider = services.BuildServiceProvider();
        var options = provider.GetRequiredService<IOptions<SeoMetadataOptions>>().Value;

        Assert.Equal("http://localhost:11434", options.OllamaEndpoint);
        Assert.Equal("llama3.2:3b", options.Model);
        Assert.True(options.Enabled);
    }

    [Fact]
    public void AddSeoMetadata_CacheOptions_DefaultDisabled()
    {
        var services = new ServiceCollection();
        services.AddLogging();

        services.AddSeoMetadata();

        var provider = services.BuildServiceProvider();
        var cacheOptions = provider.GetRequiredService<IOptions<SeoCacheOptions>>().Value;

        Assert.False(cacheOptions.Enabled);
    }

    [Fact]
    public void AddSeoMetadata_CacheOptions_CanBeEnabled()
    {
        var services = new ServiceCollection();
        services.AddLogging();

        services.AddSeoMetadata(configureCache: cache =>
        {
            cache.Enabled = true;
            cache.CacheExpiration = TimeSpan.FromDays(7);
        });

        var provider = services.BuildServiceProvider();
        var cacheOptions = provider.GetRequiredService<IOptions<SeoCacheOptions>>().Value;

        Assert.True(cacheOptions.Enabled);
        Assert.Equal(TimeSpan.FromDays(7), cacheOptions.CacheExpiration);
    }

    [Fact]
    public void AddSeoMetadataForBlog_ConfiguresCorrectly()
    {
        var services = new ServiceCollection();
        services.AddLogging();

        services.AddSeoMetadataForBlog("My Blog", "@myblog");

        var provider = services.BuildServiceProvider();
        var options = provider.GetRequiredService<IOptions<SeoMetadataOptions>>().Value;

        Assert.Equal("My Blog", options.SiteName);
        Assert.Equal("@myblog", options.TwitterSite);
        Assert.Equal("summary_large_image", options.TwitterCardType);
    }

    [Fact]
    public void AddSeoMetadataForEcommerce_ConfiguresCorrectly()
    {
        var services = new ServiceCollection();
        services.AddLogging();

        services.AddSeoMetadataForEcommerce("My Store");

        var provider = services.BuildServiceProvider();
        var options = provider.GetRequiredService<IOptions<SeoMetadataOptions>>().Value;

        Assert.Equal("My Store", options.SiteName);
        Assert.Equal("summary", options.TwitterCardType);
        Assert.False(options.EnableDesignTimeGeneration);
        Assert.Equal(TimeSpan.FromHours(1), options.CacheDuration);
    }

    [Fact]
    public void ConfigureSeoMetadata_AllowsPostConfiguration()
    {
        var services = new ServiceCollection();
        services.AddLogging();

        services.AddSeoMetadata(options => { options.SiteName = "Initial"; });

        services.ConfigureSeoMetadata(options => { options.TwitterSite = "@updated"; });

        var provider = services.BuildServiceProvider();
        var options = provider.GetRequiredService<IOptions<SeoMetadataOptions>>().Value;

        Assert.Equal("Initial", options.SiteName);
        Assert.Equal("@updated", options.TwitterSite);
    }

    [Fact]
    public void AddSeoMetadata_ServiceIsReusable()
    {
        var services = new ServiceCollection();
        services.AddLogging();

        services.AddSeoMetadata();

        var provider = services.BuildServiceProvider();
        var service1 = provider.GetRequiredService<ISeoMetadataService>();
        var service2 = provider.GetRequiredService<ISeoMetadataService>();

        // Should be singleton
        Assert.Same(service1, service2);
    }

    [Fact]
    public void AddSeoMetadata_ReturnsServiceCollection_ForChaining()
    {
        var services = new ServiceCollection();
        services.AddLogging();

        var result = services.AddSeoMetadata();

        Assert.Same(services, result);
    }

    [Fact]
    public void ConfigureSeoMetadata_ReturnsServiceCollection_ForChaining()
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddSeoMetadata();

        var result = services.ConfigureSeoMetadata(opt => { });

        Assert.Same(services, result);
    }
}