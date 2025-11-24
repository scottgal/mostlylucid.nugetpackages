using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Mostlylucid.LlmAccessibilityAuditor.Extensions;
using Mostlylucid.LlmAccessibilityAuditor.Models;
using Mostlylucid.LlmAccessibilityAuditor.Services;

namespace Mostlylucid.LlmAccessibilityAuditor.Test;

public class ServiceCollectionExtensionsTests
{
    [Fact]
    public void AddAccessibilityAuditor_RegistersServices()
    {
        // Arrange
        var services = new ServiceCollection();
        services.AddLogging();

        // Act
        services.AddAccessibilityAuditor();

        // Assert
        var provider = services.BuildServiceProvider();
        Assert.NotNull(provider.GetService<IHtmlAccessibilityParser>());
        Assert.NotNull(provider.GetService<IAuditHistoryService>());
    }

    [Fact]
    public void AddAccessibilityAuditor_WithOptions_ConfiguresOptions()
    {
        // Arrange
        var services = new ServiceCollection();
        services.AddLogging();

        // Act
        services.AddAccessibilityAuditor(options =>
        {
            options.Enabled = true;
            options.OnlyInDevelopment = false;
            options.EnableLlmAnalysis = false;
            options.Ollama.Endpoint = "http://custom:11434";
            options.Ollama.Model = "custom-model";
        });

        // Assert
        var provider = services.BuildServiceProvider();
        var options = provider.GetService<IOptions<AccessibilityAuditorOptions>>();

        Assert.NotNull(options);
        Assert.True(options.Value.Enabled);
        Assert.False(options.Value.OnlyInDevelopment);
        Assert.False(options.Value.EnableLlmAnalysis);
        Assert.Equal("http://custom:11434", options.Value.Ollama.Endpoint);
        Assert.Equal("custom-model", options.Value.Ollama.Model);
    }

    [Fact]
    public void AddAccessibilityAuditor_ReturnsServiceCollectionForChaining()
    {
        // Arrange
        var services = new ServiceCollection();

        // Act
        var result = services.AddAccessibilityAuditor();

        // Assert
        Assert.Same(services, result);
    }

    [Fact]
    public void AddAccessibilityAuditor_RegistersHtmlParser_AsSingleton()
    {
        // Arrange
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddAccessibilityAuditor();

        // Act
        var provider = services.BuildServiceProvider();
        var instance1 = provider.GetService<IHtmlAccessibilityParser>();
        var instance2 = provider.GetService<IHtmlAccessibilityParser>();

        // Assert
        Assert.Same(instance1, instance2);
    }

    [Fact]
    public void AddAccessibilityAuditor_RegistersHistoryService_AsSingleton()
    {
        // Arrange
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddAccessibilityAuditor();

        // Act
        var provider = services.BuildServiceProvider();
        var instance1 = provider.GetService<IAuditHistoryService>();
        var instance2 = provider.GetService<IAuditHistoryService>();

        // Assert
        Assert.Same(instance1, instance2);
    }
}