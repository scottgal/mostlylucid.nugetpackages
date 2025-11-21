using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using mostlylucid.llmslidetranslator.Models;
using mostlylucid.llmslidetranslator.Services;

namespace mostlylucid.llmslidetranslator.Extensions;

/// <summary>
///     Extension methods for configuring LLM Slide Translator services
/// </summary>
public static class ServiceCollectionExtensions
{
    /// <summary>
    ///     Add LLM Slide Translator services to the DI container
    /// </summary>
    /// <param name="services">Service collection</param>
    /// <param name="configuration">Configuration</param>
    /// <param name="sectionName">Configuration section name (default: "LlmSlideTranslator")</param>
    /// <returns>Service collection for chaining</returns>
    public static IServiceCollection AddLlmSlideTranslator(
        this IServiceCollection services,
        IConfiguration configuration,
        string sectionName = "LlmSlideTranslator")
    {
        // Bind configuration
        services.Configure<LlmSlideTranslatorConfig>(configuration.GetSection(sectionName));

        // Register core services
        services.AddSingleton<IMarkdownChunker, MarkdownChunker>();
        services.AddSingleton<IEmbeddingGenerator, EmbeddingGenerator>();
        services.AddSingleton<ITranslationComparer, TranslationComparer>();

        // Register vector store based on configuration
        var config = configuration.GetSection(sectionName).Get<LlmSlideTranslatorConfig>();
        if (config?.VectorStoreProvider?.Equals("Qdrant", StringComparison.OrdinalIgnoreCase) == true)
            services.AddSingleton<IVectorStore, QdrantVectorStore>();
        else
            services.AddSingleton<IVectorStore, FileVectorStore>();

        // Register HTTP clients
        services.AddHttpClient<IOllamaClient, OllamaClient>();
        services.AddHttpClient<INmtClient, NmtClient>();

        // Register main translator
        services.AddScoped<ILlmSlideTranslator, LlmSlideTranslator>();

        return services;
    }

    /// <summary>
    ///     Add LLM Slide Translator services with custom configuration
    /// </summary>
    /// <param name="services">Service collection</param>
    /// <param name="configureOptions">Configuration action</param>
    /// <returns>Service collection for chaining</returns>
    public static IServiceCollection AddLlmSlideTranslator(
        this IServiceCollection services,
        Action<LlmSlideTranslatorConfig> configureOptions)
    {
        services.Configure(configureOptions);

        // Register core services
        services.AddSingleton<IMarkdownChunker, MarkdownChunker>();
        services.AddSingleton<IEmbeddingGenerator, EmbeddingGenerator>();
        services.AddSingleton<ITranslationComparer, TranslationComparer>();

        // Determine vector store provider from configuration
        var config = new LlmSlideTranslatorConfig();
        configureOptions(config);

        if (config.VectorStoreProvider?.Equals("Qdrant", StringComparison.OrdinalIgnoreCase) == true)
            services.AddSingleton<IVectorStore, QdrantVectorStore>();
        else
            services.AddSingleton<IVectorStore, FileVectorStore>();

        // Register HTTP clients
        services.AddHttpClient<IOllamaClient, OllamaClient>();
        services.AddHttpClient<INmtClient, NmtClient>();

        // Register main translator
        services.AddScoped<ILlmSlideTranslator, LlmSlideTranslator>();

        return services;
    }
}