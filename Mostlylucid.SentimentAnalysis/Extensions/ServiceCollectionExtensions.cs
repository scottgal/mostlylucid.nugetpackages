using Microsoft.Extensions.DependencyInjection;
using Mostlylucid.SentimentAnalysis.Models;
using Mostlylucid.SentimentAnalysis.Services;

namespace Mostlylucid.SentimentAnalysis.Extensions;

/// <summary>
/// Extension methods for configuring sentiment analysis services.
/// </summary>
public static class ServiceCollectionExtensions
{
    /// <summary>
    /// Adds sentiment analysis services to the service collection.
    /// </summary>
    /// <param name="services">The service collection.</param>
    /// <param name="configure">Optional configuration action for SentimentOptions.</param>
    /// <returns>The service collection for chaining.</returns>
    public static IServiceCollection AddSentimentAnalysis(
        this IServiceCollection services,
        Action<SentimentOptions>? configure = null)
    {
        // Configure options
        if (configure != null)
        {
            services.Configure(configure);
        }
        else
        {
            services.Configure<SentimentOptions>(_ => { });
        }

        // Register HttpClient for model downloads
        services.AddHttpClient("SentimentModelDownloader", client =>
        {
            client.DefaultRequestHeaders.Add("User-Agent", "MostlylucidSentimentAnalysis/1.0");
        });

        // Register service as singleton (model should be loaded once)
        services.AddSingleton<ISentimentAnalysisService, SentimentAnalysisService>();

        return services;
    }

    /// <summary>
    /// Adds sentiment analysis services with a custom model path.
    /// </summary>
    /// <param name="services">The service collection.</param>
    /// <param name="modelPath">Path where models will be stored.</param>
    /// <returns>The service collection for chaining.</returns>
    public static IServiceCollection AddSentimentAnalysis(
        this IServiceCollection services,
        string modelPath)
    {
        return services.AddSentimentAnalysis(options =>
        {
            options.ModelPath = modelPath;
        });
    }
}
