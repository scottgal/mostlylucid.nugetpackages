using Microsoft.Extensions.DependencyInjection;
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
    ///     Add bot detection services to the service collection
    /// </summary>
    public static IServiceCollection AddBotDetection(
        this IServiceCollection services,
        Action<BotDetectionOptions>? configure = null)
    {
        if (configure != null)
            services.Configure(configure);
        else
            services.Configure<BotDetectionOptions>(options => { });

        // Register core bot detection service
        services.AddSingleton<IBotDetectionService, BotDetectionService>();

        // Register bot list update service as hosted service
        services.AddHostedService<BotListUpdateService>();

        // Register individual detectors
        services.AddSingleton<IDetector, UserAgentDetector>();
        services.AddSingleton<IDetector, HeaderDetector>();
        services.AddSingleton<IDetector, BehavioralDetector>();
        services.AddSingleton<IDetector, IpDetector>();
        // LlmDetector disabled due to OllamaSharp API compatibility issues
        // services.AddSingleton<IDetector, LlmDetector>();

        // Add memory cache if not already registered
        services.AddMemoryCache();

        return services;
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