using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Mostlylucid.SentimentAnalysis.Extensions;
using Mostlylucid.SentimentAnalysis.Models;
using Mostlylucid.SentimentAnalysis.Services;

namespace Mostlylucid.SentimentAnalysis.Tests;

public class ServiceCollectionExtensionsTests
{
    [Fact]
    public void AddSentimentAnalysis_RegistersServices()
    {
        // Arrange
        var services = new ServiceCollection();
        services.AddLogging();

        // Act
        services.AddSentimentAnalysis();

        // Assert
        var provider = services.BuildServiceProvider();

        // Verify options are registered
        var options = provider.GetService<IOptions<SentimentOptions>>();
        Assert.NotNull(options);

        // Verify service is registered (but don't resolve it as it requires model)
        var descriptor = services.FirstOrDefault(d => d.ServiceType == typeof(ISentimentAnalysisService));
        Assert.NotNull(descriptor);
        Assert.Equal(ServiceLifetime.Singleton, descriptor.Lifetime);
    }

    [Fact]
    public void AddSentimentAnalysis_WithConfiguration_AppliesOptions()
    {
        // Arrange
        var services = new ServiceCollection();
        services.AddLogging();

        // Act
        services.AddSentimentAnalysis(options =>
        {
            options.ModelPath = "/custom/path";
            options.EnableDiagnosticLogging = true;
            options.InferenceThreads = 8;
            options.MaxChunkLength = 256;
        });

        // Assert
        var provider = services.BuildServiceProvider();
        var options = provider.GetRequiredService<IOptions<SentimentOptions>>().Value;

        Assert.Equal("/custom/path", options.ModelPath);
        Assert.True(options.EnableDiagnosticLogging);
        Assert.Equal(8, options.InferenceThreads);
        Assert.Equal(256, options.MaxChunkLength);
    }

    [Fact]
    public void AddSentimentAnalysis_WithModelPath_SetsPath()
    {
        // Arrange
        var services = new ServiceCollection();
        services.AddLogging();

        // Act
        services.AddSentimentAnalysis("/my/model/path");

        // Assert
        var provider = services.BuildServiceProvider();
        var options = provider.GetRequiredService<IOptions<SentimentOptions>>().Value;

        Assert.Equal("/my/model/path", options.ModelPath);
    }

    [Fact]
    public void AddSentimentAnalysis_RegistersHttpClient()
    {
        // Arrange
        var services = new ServiceCollection();
        services.AddLogging();

        // Act
        services.AddSentimentAnalysis();

        // Assert
        var httpClientFactoryDescriptor = services.FirstOrDefault(d =>
            d.ServiceType == typeof(IHttpClientFactory));

        Assert.NotNull(httpClientFactoryDescriptor);
    }

    [Fact]
    public void AddSentimentAnalysis_CanBeCalled_MultipleTimes()
    {
        // Arrange
        var services = new ServiceCollection();
        services.AddLogging();

        // Act - should not throw
        services.AddSentimentAnalysis(opt => opt.ModelPath = "./path1");
        services.AddSentimentAnalysis(opt => opt.ModelPath = "./path2");

        // Assert
        var provider = services.BuildServiceProvider();
        var options = provider.GetRequiredService<IOptions<SentimentOptions>>().Value;

        // Last one wins
        Assert.Equal("./path2", options.ModelPath);
    }
}
