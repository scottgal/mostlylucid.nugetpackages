using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Options;
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
    /// Uses the provider specified in options (default: ONNX).
    /// </summary>
    /// <param name="services">The service collection.</param>
    /// <param name="configure">Optional configuration action for SentimentOptions.</param>
    /// <returns>The service collection for chaining.</returns>
    public static IServiceCollection AddSentimentAnalysis(
        this IServiceCollection services,
        Action<SentimentOptions>? configure = null)
    {
        // Configure options
        var options = new SentimentOptions();
        configure?.Invoke(options);

        services.Configure<SentimentOptions>(opt =>
        {
            opt.Provider = options.Provider;
            opt.ModelPath = options.ModelPath;
            opt.ModelUrl = options.ModelUrl;
            opt.ModelFileName = options.ModelFileName;
            opt.MaxChunkLength = options.MaxChunkLength;
            opt.ChunkOverlap = options.ChunkOverlap;
            opt.EnableDiagnosticLogging = options.EnableDiagnosticLogging;
            opt.InferenceThreads = options.InferenceThreads;
            opt.DownloadTimeoutSeconds = options.DownloadTimeoutSeconds;
            opt.AutoDownloadModel = options.AutoDownloadModel;
            opt.ModelLabels = options.ModelLabels;
            opt.OllamaEndpoint = options.OllamaEndpoint;
            opt.OllamaModel = options.OllamaModel;
            opt.OllamaTimeoutMs = options.OllamaTimeoutMs;
            opt.OllamaSystemPrompt = options.OllamaSystemPrompt;
        });

        // Register HttpClient for model downloads (used by ONNX provider)
        services.AddHttpClient("SentimentModelDownloader", client =>
        {
            client.DefaultRequestHeaders.Add("User-Agent", "MostlylucidSentimentAnalysis/1.0");
        });

        // Register the appropriate service based on provider
        services.AddSingleton<ISentimentAnalysisService>(sp =>
        {
            var opts = sp.GetRequiredService<IOptions<SentimentOptions>>().Value;

            return opts.Provider switch
            {
                SentimentProvider.Ollama => ActivatorUtilities.CreateInstance<OllamaSentimentService>(sp),
                _ => ActivatorUtilities.CreateInstance<SentimentAnalysisService>(sp)
            };
        });

        return services;
    }

    /// <summary>
    /// Adds sentiment analysis services using ONNX provider with a custom model path.
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
            options.Provider = SentimentProvider.Onnx;
            options.ModelPath = modelPath;
        });
    }

    /// <summary>
    /// Adds sentiment analysis services using Ollama provider.
    /// </summary>
    /// <param name="services">The service collection.</param>
    /// <param name="ollamaEndpoint">Ollama API endpoint (default: http://localhost:11434).</param>
    /// <param name="model">Ollama model to use (default: llama3.2).</param>
    /// <returns>The service collection for chaining.</returns>
    public static IServiceCollection AddOllamaSentimentAnalysis(
        this IServiceCollection services,
        string? ollamaEndpoint = null,
        string? model = null)
    {
        return services.AddSentimentAnalysis(options =>
        {
            options.Provider = SentimentProvider.Ollama;
            if (ollamaEndpoint != null)
                options.OllamaEndpoint = ollamaEndpoint;
            if (model != null)
                options.OllamaModel = model;
        });
    }
}
