using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Mostlylucid.LlmI18nAssistant.Models;
using Mostlylucid.LlmI18nAssistant.Services;

namespace Mostlylucid.LlmI18nAssistant.Extensions;

/// <summary>
///     Extension methods for registering LlmI18nAssistant services
/// </summary>
public static class ServiceCollectionExtensions
{
    /// <summary>
    ///     Adds LLM I18n Assistant services to the service collection
    /// </summary>
    /// <param name="services">The service collection</param>
    /// <param name="configuration">The configuration</param>
    /// <param name="sectionName">The configuration section name (default: "LlmI18nAssistant")</param>
    /// <returns>The service collection</returns>
    public static IServiceCollection AddLlmI18nAssistant(
        this IServiceCollection services,
        IConfiguration configuration,
        string sectionName = "LlmI18nAssistant")
    {
        // Bind configuration
        services.Configure<LlmI18nAssistantConfig>(configuration.GetSection(sectionName));

        // Register core services (singleton - expensive initialization)
        services.AddSingleton<IResourceFileParser, ResourceFileParser>();
        services.AddSingleton<IValueTransformer, ValueTransformer>();
        services.AddSingleton<IEmbeddingGenerator, EmbeddingGenerator>();
        services.AddSingleton<IConsistencyModeService, ConsistencyModeService>();

        // Register HTTP clients (typed clients)
        services.AddHttpClient<IOllamaClient, OllamaClient>();
        services.AddHttpClient<INmtClient, NmtClient>();

        // Add HttpClient for embedding generator (shares Ollama endpoint)
        services.AddHttpClient<EmbeddingGenerator>();

        // Register main service (scoped - per-request in web context)
        services.AddScoped<ILlmI18nAssistant, LlmI18nAssistant>();

        return services;
    }

    /// <summary>
    ///     Adds LLM I18n Assistant services with custom configuration
    /// </summary>
    /// <param name="services">The service collection</param>
    /// <param name="configureOptions">Action to configure options</param>
    /// <returns>The service collection</returns>
    public static IServiceCollection AddLlmI18nAssistant(
        this IServiceCollection services,
        Action<LlmI18nAssistantConfig> configureOptions)
    {
        // Configure options with action
        services.Configure(configureOptions);

        // Register core services (singleton - expensive initialization)
        services.AddSingleton<IResourceFileParser, ResourceFileParser>();
        services.AddSingleton<IValueTransformer, ValueTransformer>();
        services.AddSingleton<IEmbeddingGenerator, EmbeddingGenerator>();
        services.AddSingleton<IConsistencyModeService, ConsistencyModeService>();

        // Register HTTP clients (typed clients)
        services.AddHttpClient<IOllamaClient, OllamaClient>();
        services.AddHttpClient<INmtClient, NmtClient>();

        // Add HttpClient for embedding generator
        services.AddHttpClient<EmbeddingGenerator>();

        // Register main service (scoped - per-request in web context)
        services.AddScoped<ILlmI18nAssistant, LlmI18nAssistant>();

        return services;
    }
}
