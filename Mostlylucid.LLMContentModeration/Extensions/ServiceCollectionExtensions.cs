using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Mostlylucid.LLMContentModeration.Models;
using Mostlylucid.LLMContentModeration.Services;

namespace Mostlylucid.LLMContentModeration.Extensions;

/// <summary>
///     Extension methods for configuring LLM Content Moderation services
/// </summary>
public static class ServiceCollectionExtensions
{
    /// <summary>
    ///     Add LLM Content Moderation services to the DI container
    /// </summary>
    /// <param name="services">Service collection</param>
    /// <param name="configuration">Configuration</param>
    /// <param name="sectionName">Configuration section name (default: "LLMContentModeration")</param>
    /// <returns>Service collection for chaining</returns>
    public static IServiceCollection AddLLMContentModeration(
        this IServiceCollection services,
        IConfiguration configuration,
        string sectionName = "LLMContentModeration")
    {
        // Bind configuration
        services.Configure<ModerationOptions>(configuration.GetSection(sectionName));

        // Register services
        RegisterServices(services);

        return services;
    }

    /// <summary>
    ///     Add LLM Content Moderation services with custom configuration
    /// </summary>
    /// <param name="services">Service collection</param>
    /// <param name="configureOptions">Configuration action</param>
    /// <returns>Service collection for chaining</returns>
    public static IServiceCollection AddLLMContentModeration(
        this IServiceCollection services,
        Action<ModerationOptions> configureOptions)
    {
        services.Configure(configureOptions);

        // Register services
        RegisterServices(services);

        return services;
    }

    /// <summary>
    ///     Add LLM Content Moderation with default configuration
    /// </summary>
    /// <param name="services">Service collection</param>
    /// <returns>Service collection for chaining</returns>
    public static IServiceCollection AddLLMContentModeration(this IServiceCollection services)
    {
        services.Configure<ModerationOptions>(options => { });

        // Register services
        RegisterServices(services);

        return services;
    }

    private static void RegisterServices(IServiceCollection services)
    {
        // Register PII detector
        services.AddSingleton<IPiiDetector, PiiDetector>();

        // Register Ollama client with HttpClient
        services.AddHttpClient<IModerationOllamaClient, ModerationOllamaClient>();

        // Register main moderation service
        services.AddScoped<IContentModerationService, ContentModerationService>();
    }
}