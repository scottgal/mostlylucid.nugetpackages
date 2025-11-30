using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Mostlylucid.BotDetection.Data;
using Mostlylucid.BotDetection.Detectors;
using Mostlylucid.BotDetection.Models;
using Mostlylucid.BotDetection.Services;

namespace Mostlylucid.BotDetection.Extensions;

/// <summary>
///     Extension methods for configuring bot detection services
/// </summary>
public static class ServiceCollectionExtensions
{
    /// <summary>
    ///     Add bot detection services to the service collection.
    ///     Supports multiple detection strategies from simple (static patterns) to advanced (LLM).
    /// </summary>
    /// <param name="services">The service collection</param>
    /// <param name="configure">Optional configuration action</param>
    /// <returns>The service collection for chaining</returns>
    public static IServiceCollection AddBotDetection(
        this IServiceCollection services,
        Action<BotDetectionOptions>? configure = null)
    {
        // Configure options
        if (configure != null)
            services.Configure(configure);
        else
            services.Configure<BotDetectionOptions>(_ => { });

        // Register options validator
        services.AddSingleton<IValidateOptions<BotDetectionOptions>, BotDetectionOptionsValidator>();

        // Add HttpClient factory for bot list fetching
        services.AddHttpClient();

        // Add memory cache if not already registered
        services.AddMemoryCache();

        // Register bot list fetcher and database
        services.AddSingleton<IBotListFetcher, BotListFetcher>();
        services.AddSingleton<IBotListDatabase, BotListDatabase>();

        // Register core bot detection service
        services.AddSingleton<IBotDetectionService, BotDetectionService>();

        // Register bot list update service as hosted service
        services.AddHostedService<BotListUpdateService>();

        // Register individual detectors
        services.AddSingleton<IDetector, UserAgentDetector>();
        services.AddSingleton<IDetector, HeaderDetector>();
        services.AddSingleton<IDetector, BehavioralDetector>();
        services.AddSingleton<IDetector, IpDetector>();
        services.AddSingleton<IDetector, LlmDetector>();

        return services;
    }

    /// <summary>
    ///     Add simple bot detection (user-agent only, no database, no LLM).
    ///     Fastest and simplest option for basic protection.
    /// </summary>
    /// <param name="services">The service collection</param>
    /// <param name="configure">Optional configuration action</param>
    /// <returns>The service collection for chaining</returns>
    public static IServiceCollection AddSimpleBotDetection(
        this IServiceCollection services,
        Action<BotDetectionOptions>? configure = null)
    {
        return services.AddBotDetection(options =>
        {
            options.EnableUserAgentDetection = true;
            options.EnableHeaderAnalysis = false;
            options.EnableIpDetection = false;
            options.EnableBehavioralAnalysis = false;
            options.EnableLlmDetection = false;

            configure?.Invoke(options);
        });
    }

    /// <summary>
    ///     Add comprehensive bot detection (all heuristics, no LLM).
    ///     Good balance of accuracy and performance.
    /// </summary>
    /// <param name="services">The service collection</param>
    /// <param name="configure">Optional configuration action</param>
    /// <returns>The service collection for chaining</returns>
    public static IServiceCollection AddComprehensiveBotDetection(
        this IServiceCollection services,
        Action<BotDetectionOptions>? configure = null)
    {
        return services.AddBotDetection(options =>
        {
            options.EnableUserAgentDetection = true;
            options.EnableHeaderAnalysis = true;
            options.EnableIpDetection = true;
            options.EnableBehavioralAnalysis = true;
            options.EnableLlmDetection = false;

            configure?.Invoke(options);
        });
    }

    /// <summary>
    ///     Add advanced bot detection with LLM (requires Ollama).
    ///     Most accurate but requires more resources.
    /// </summary>
    /// <param name="services">The service collection</param>
    /// <param name="ollamaEndpoint">Ollama endpoint URL</param>
    /// <param name="model">Ollama model name</param>
    /// <param name="configure">Optional configuration action</param>
    /// <returns>The service collection for chaining</returns>
    public static IServiceCollection AddAdvancedBotDetection(
        this IServiceCollection services,
        string ollamaEndpoint = "http://localhost:11434",
        string model = "qwen2.5:1.5b",
        Action<BotDetectionOptions>? configure = null)
    {
        return services.AddBotDetection(options =>
        {
            options.EnableUserAgentDetection = true;
            options.EnableHeaderAnalysis = true;
            options.EnableIpDetection = true;
            options.EnableBehavioralAnalysis = true;
            options.EnableLlmDetection = true;
            options.OllamaEndpoint = ollamaEndpoint;
            options.OllamaModel = model;

            configure?.Invoke(options);
        });
    }

    /// <summary>
    ///     Configure bot detection with custom options
    /// </summary>
    public static IServiceCollection ConfigureBotDetection(
        this IServiceCollection services,
        Action<BotDetectionOptions> configure)
    {
        services.Configure(configure);
        return services;
    }
}
