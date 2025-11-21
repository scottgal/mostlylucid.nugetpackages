using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Mostlylucid.LlmAltText.Extensions;
using Mostlylucid.LlmAltText.Models;
using Mostlylucid.LlmAltText.Services;

namespace Mostlylucid.LlmAltText.Test.Extensions;

/// <summary>
///     Comprehensive tests for LlmAltText ServiceCollectionExtensions
/// </summary>
public class ServiceCollectionExtensionsTests
{
    #region Chaining Tests

    [Fact]
    public void AddAltTextGeneration_CanBeChained()
    {
        // Arrange
        var services = new ServiceCollection();

        // Act - Chain multiple registrations
        services
            .AddLogging()
            .AddAltTextGeneration(options => options.MaxWords = 100)
            .ConfigureAltTextGeneration(options => options.EnableDiagnosticLogging = false);

        // Assert
        var provider = services.BuildServiceProvider();
        var options = provider.GetService<IOptions<AltTextOptions>>();
        Assert.Equal(100, options!.Value.MaxWords);
        Assert.False(options.Value.EnableDiagnosticLogging);
    }

    #endregion

    #region Multiple Registration Tests

    [Fact]
    public void AddAltTextGeneration_CalledMultipleTimes_DoesNotThrow()
    {
        // Arrange
        var services = new ServiceCollection();
        services.AddLogging();

        // Act - Should not throw
        services.AddAltTextGeneration();
        var exception = Record.Exception(() => services.AddAltTextGeneration());

        // Assert
        Assert.Null(exception);
    }

    #endregion

    #region AddAltTextGeneration Tests

    [Fact]
    public void AddAltTextGeneration_RegistersServices()
    {
        // Arrange
        var services = new ServiceCollection();
        services.AddLogging();

        // Act
        services.AddAltTextGeneration();

        // Assert
        var provider = services.BuildServiceProvider();
        Assert.NotNull(provider);
    }

    [Fact]
    public void AddAltTextGeneration_RegistersOptions()
    {
        // Arrange
        var services = new ServiceCollection();
        services.AddLogging();

        // Act
        services.AddAltTextGeneration();

        // Assert
        var provider = services.BuildServiceProvider();
        var options = provider.GetService<IOptions<AltTextOptions>>();
        Assert.NotNull(options);
    }

    [Fact]
    public void AddAltTextGeneration_WithOptions_ConfiguresOptions()
    {
        // Arrange
        var services = new ServiceCollection();
        services.AddLogging();

        // Act
        services.AddAltTextGeneration(options =>
        {
            options.ModelPath = "/custom/path";
            options.MaxWords = 150;
            options.EnableDiagnosticLogging = false;
        });

        // Assert
        var provider = services.BuildServiceProvider();
        var options = provider.GetService<IOptions<AltTextOptions>>();
        Assert.Equal("/custom/path", options!.Value.ModelPath);
        Assert.Equal(150, options.Value.MaxWords);
        Assert.False(options.Value.EnableDiagnosticLogging);
    }

    [Fact]
    public void AddAltTextGeneration_RegistersImageAnalysisService()
    {
        // Arrange
        var services = new ServiceCollection();
        services.AddLogging();

        // Act
        services.AddAltTextGeneration();

        // Assert
        var descriptor = services.FirstOrDefault(d =>
            d.ServiceType == typeof(IImageAnalysisService));
        Assert.NotNull(descriptor);
        Assert.Equal(ServiceLifetime.Singleton, descriptor.Lifetime);
    }

    [Fact]
    public void AddAltTextGeneration_ReturnsServiceCollection()
    {
        // Arrange
        var services = new ServiceCollection();

        // Act
        var result = services.AddAltTextGeneration();

        // Assert
        Assert.Same(services, result);
    }

    [Fact]
    public void AddAltTextGeneration_NullOptions_UsesDefaults()
    {
        // Arrange
        var services = new ServiceCollection();
        services.AddLogging();

        // Act
        services.AddAltTextGeneration();

        // Assert
        var provider = services.BuildServiceProvider();
        var options = provider.GetService<IOptions<AltTextOptions>>();
        Assert.Equal("./models", options!.Value.ModelPath); // Default
        Assert.Equal(90, options.Value.MaxWords); // Default
        Assert.True(options.Value.EnableDiagnosticLogging); // Default
    }

    [Fact]
    public void AddAltTextGeneration_ServiceIsSingleton()
    {
        // Arrange
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddAltTextGeneration();

        // Assert
        var descriptor = services.Single(d =>
            d.ServiceType == typeof(IImageAnalysisService));
        Assert.Equal(ServiceLifetime.Singleton, descriptor.Lifetime);
    }

    #endregion

    #region ConfigureAltTextGeneration Tests

    [Fact]
    public void ConfigureAltTextGeneration_UpdatesOptions()
    {
        // Arrange
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddAltTextGeneration(options => options.MaxWords = 50);

        // Act
        services.ConfigureAltTextGeneration(options => options.MaxWords = 200);

        // Assert
        var provider = services.BuildServiceProvider();
        var options = provider.GetService<IOptions<AltTextOptions>>();
        Assert.Equal(200, options!.Value.MaxWords);
    }

    [Fact]
    public void ConfigureAltTextGeneration_ReturnsServiceCollection()
    {
        // Arrange
        var services = new ServiceCollection();
        services.AddAltTextGeneration();

        // Act
        var result = services.ConfigureAltTextGeneration(options => { });

        // Assert
        Assert.Same(services, result);
    }

    [Fact]
    public void ConfigureAltTextGeneration_CanUpdateMultipleProperties()
    {
        // Arrange
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddAltTextGeneration();

        // Act
        services.ConfigureAltTextGeneration(options =>
        {
            options.ModelPath = "/new/path";
            options.DefaultTaskType = "CAPTION";
            options.EnableDiagnosticLogging = false;
            options.AltTextPrompt = "Custom prompt";
        });

        // Assert
        var provider = services.BuildServiceProvider();
        var options = provider.GetService<IOptions<AltTextOptions>>();
        Assert.Equal("/new/path", options!.Value.ModelPath);
        Assert.Equal("CAPTION", options.Value.DefaultTaskType);
        Assert.False(options.Value.EnableDiagnosticLogging);
        Assert.Equal("Custom prompt", options.Value.AltTextPrompt);
    }

    #endregion
}