using System.Runtime.CompilerServices;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Mostlylucid.LlmLogSummarizer.Clustering;
using Mostlylucid.LlmLogSummarizer.Models;
using Mostlylucid.LlmLogSummarizer.Outputs;
using Mostlylucid.LlmLogSummarizer.Services;
using Mostlylucid.LlmLogSummarizer.Sources;

namespace Mostlylucid.LlmLogSummarizer.Extensions;

/// <summary>
///     Extension methods for registering log summarizer services.
/// </summary>
public static class ServiceCollectionExtensions
{
    /// <summary>
    ///     Adds the LLM log summarizer services with configuration from appsettings.
    /// </summary>
    /// <param name="services">The service collection.</param>
    /// <param name="configuration">The configuration.</param>
    /// <returns>The service collection for chaining.</returns>
    public static IServiceCollection AddLlmLogSummarizer(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        services.Configure<LogSummarizerOptions>(
            configuration.GetSection(LogSummarizerOptions.SectionName));

        return services.AddLlmLogSummarizerCore();
    }

    /// <summary>
    ///     Adds the LLM log summarizer services with programmatic configuration.
    /// </summary>
    /// <param name="services">The service collection.</param>
    /// <param name="configure">The configuration action.</param>
    /// <returns>The service collection for chaining.</returns>
    public static IServiceCollection AddLlmLogSummarizer(
        this IServiceCollection services,
        Action<LogSummarizerOptions> configure)
    {
        services.Configure(configure);
        return services.AddLlmLogSummarizerCore();
    }

    /// <summary>
    ///     Adds the LLM log summarizer with default options for quick setup.
    ///     Just point to your Serilog JSON files and go!
    /// </summary>
    /// <param name="services">The service collection.</param>
    /// <param name="serilogPath">Path to Serilog JSON log files (glob pattern supported).</param>
    /// <param name="outputDirectory">Directory for markdown output files.</param>
    /// <returns>The service collection for chaining.</returns>
    public static IServiceCollection AddLlmLogSummarizer(
        this IServiceCollection services,
        string serilogPath,
        string outputDirectory = "./logs/summaries")
    {
        return services.AddLlmLogSummarizer(options =>
        {
            options.Sources.SerilogFiles.Add(new SerilogSourceConfig
            {
                Name = "Serilog",
                Path = serilogPath
            });
            options.Output.Markdown = new MarkdownOutputConfig
            {
                Enabled = true,
                OutputDirectory = outputDirectory
            };
        });
    }

    private static IServiceCollection AddLlmLogSummarizerCore(this IServiceCollection services)
    {
        // Register log sources
        services.AddSingleton<ILogSource>(sp =>
        {
            var options = sp.GetRequiredService<IOptions<LogSummarizerOptions>>();
            var logger = sp.GetRequiredService<ILogger<SerilogJsonLogSource>>();

            // Create sources for each configured Serilog path
            return new CompositeLogSource(
                options.Value.Sources.SerilogFiles
                    .Select(c => new SerilogJsonLogSource(c, logger))
                    .ToList());
        });

        services.AddSingleton<ILogSource>(sp =>
        {
            var options = sp.GetRequiredService<IOptions<LogSummarizerOptions>>();
            var logger = sp.GetRequiredService<ILogger<TextLogSource>>();

            return new CompositeLogSource(
                options.Value.Sources.TextFiles
                    .Select(c => new TextLogSource(c, logger))
                    .ToList());
        });

        // Register App Insights source if configured
        services.AddHttpClient<AppInsightsLogSource>();
        services.AddSingleton<ILogSource>(sp =>
        {
            var options = sp.GetRequiredService<IOptions<LogSummarizerOptions>>();
            if (options.Value.Sources.AppInsights == null)
                return new NullLogSource();

            var httpClient = sp.GetRequiredService<IHttpClientFactory>().CreateClient(nameof(AppInsightsLogSource));
            var logger = sp.GetRequiredService<ILogger<AppInsightsLogSource>>();
            return new AppInsightsLogSource(options.Value.Sources.AppInsights, httpClient, logger);
        });

        // Register aggregator
        services.AddSingleton<ILogSourceAggregator, LogSourceAggregator>();

        // Register clustering
        services.AddSingleton<IExceptionClusterer, ExceptionClusterer>();

        // Register LLM summarizer
        services.AddHttpClient<ILogSummarizer, OllamaLogSummarizer>();

        // Register output providers
        services.AddSingleton<IOutputProvider, MarkdownOutputProvider>();
        services.AddSingleton<IOutputProvider, EmailOutputProvider>();
        services.AddHttpClient<SlackOutputProvider>();
        services.AddSingleton<IOutputProvider>(sp =>
        {
            var options = sp.GetRequiredService<IOptions<LogSummarizerOptions>>();
            var httpClient = sp.GetRequiredService<IHttpClientFactory>().CreateClient(nameof(SlackOutputProvider));
            var logger = sp.GetRequiredService<ILogger<SlackOutputProvider>>();
            return new SlackOutputProvider(options, httpClient, logger);
        });
        services.AddHttpClient<WebhookOutputProvider>();
        services.AddSingleton<IOutputProvider>(sp =>
        {
            var options = sp.GetRequiredService<IOptions<LogSummarizerOptions>>();
            var httpClient = sp.GetRequiredService<IHttpClientFactory>().CreateClient(nameof(WebhookOutputProvider));
            var logger = sp.GetRequiredService<ILogger<WebhookOutputProvider>>();
            return new WebhookOutputProvider(options, httpClient, logger);
        });

        // Register orchestrator
        services.AddSingleton<ILogSummarizationOrchestrator, LogSummarizationOrchestrator>();

        // Register background service
        services.AddHostedService<LogSummarizationBackgroundService>();

        return services;
    }

    /// <summary>
    ///     Adds only the log summarizer services without the background service.
    ///     Use this when you want to manually trigger summarization.
    /// </summary>
    public static IServiceCollection AddLlmLogSummarizerWithoutBackgroundService(
        this IServiceCollection services,
        Action<LogSummarizerOptions> configure)
    {
        services.Configure(configure);

        // Same as AddLlmLogSummarizerCore but without the hosted service
        services.AddSingleton<ILogSource>(sp =>
        {
            var options = sp.GetRequiredService<IOptions<LogSummarizerOptions>>();
            var logger = sp.GetRequiredService<ILogger<SerilogJsonLogSource>>();
            return new CompositeLogSource(
                options.Value.Sources.SerilogFiles
                    .Select(c => new SerilogJsonLogSource(c, logger))
                    .ToList());
        });

        services.AddSingleton<ILogSource>(sp =>
        {
            var options = sp.GetRequiredService<IOptions<LogSummarizerOptions>>();
            var logger = sp.GetRequiredService<ILogger<TextLogSource>>();
            return new CompositeLogSource(
                options.Value.Sources.TextFiles
                    .Select(c => new TextLogSource(c, logger))
                    .ToList());
        });

        services.AddSingleton<ILogSourceAggregator, LogSourceAggregator>();
        services.AddSingleton<IExceptionClusterer, ExceptionClusterer>();
        services.AddHttpClient<ILogSummarizer, OllamaLogSummarizer>();
        services.AddSingleton<IOutputProvider, MarkdownOutputProvider>();
        services.AddSingleton<IOutputProvider, EmailOutputProvider>();
        services.AddHttpClient<SlackOutputProvider>();
        services.AddSingleton<IOutputProvider>(sp =>
        {
            var options = sp.GetRequiredService<IOptions<LogSummarizerOptions>>();
            var httpClient = sp.GetRequiredService<IHttpClientFactory>().CreateClient(nameof(SlackOutputProvider));
            var logger = sp.GetRequiredService<ILogger<SlackOutputProvider>>();
            return new SlackOutputProvider(options, httpClient, logger);
        });
        services.AddSingleton<ILogSummarizationOrchestrator, LogSummarizationOrchestrator>();

        return services;
    }
}

/// <summary>
///     A composite log source that aggregates multiple sources of the same type.
/// </summary>
internal class CompositeLogSource : ILogSource
{
    private readonly IReadOnlyList<ILogSource> _sources;

    public CompositeLogSource(IReadOnlyList<ILogSource> sources)
    {
        _sources = sources;
    }

    public string Name => string.Join(", ", _sources.Select(s => s.Name));

    public bool IsAvailable => _sources.Any(s => s.IsAvailable);

    public async IAsyncEnumerable<LogEntry> GetEntriesAsync(
        DateTimeOffset from,
        DateTimeOffset to,
        int maxEntries,
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        var entriesPerSource = maxEntries / Math.Max(1, _sources.Count);

        foreach (var source in _sources.Where(s => s.IsAvailable))
        await foreach (var entry in source.GetEntriesAsync(from, to, entriesPerSource, cancellationToken))
            yield return entry;
    }

    public async Task<bool> TestConnectionAsync(CancellationToken cancellationToken = default)
    {
        foreach (var source in _sources)
            if (await source.TestConnectionAsync(cancellationToken))
                return true;
        return false;
    }
}

/// <summary>
///     A null log source for when no source is configured.
/// </summary>
internal class NullLogSource : ILogSource
{
    public string Name => "None";
    public bool IsAvailable => false;

    public async IAsyncEnumerable<LogEntry> GetEntriesAsync(
        DateTimeOffset from, DateTimeOffset to, int maxEntries,
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        await Task.CompletedTask;
        yield break;
    }

    public Task<bool> TestConnectionAsync(CancellationToken cancellationToken = default)
    {
        return Task.FromResult(false);
    }
}