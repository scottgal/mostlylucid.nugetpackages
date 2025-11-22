using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Mostlylucid.LlmSeoMetadata.Data;
using Mostlylucid.LlmSeoMetadata.Models;
using Mostlylucid.LlmSeoMetadata.Services;

namespace Mostlylucid.LlmSeoMetadata.Extensions;

/// <summary>
///     Extension methods for configuring SEO metadata services
/// </summary>
public static class ServiceCollectionExtensions
{
    /// <summary>
    ///     Add SEO metadata generation services with Ollama LLM
    /// </summary>
    /// <param name="services">Service collection</param>
    /// <param name="configure">Configure SEO metadata options</param>
    /// <param name="configureCache">Configure database caching (disabled by default)</param>
    public static IServiceCollection AddSeoMetadata(
        this IServiceCollection services,
        Action<SeoMetadataOptions>? configure = null,
        Action<SeoCacheOptions>? configureCache = null)
    {
        // Configure SEO metadata options
        if (configure != null)
            services.Configure(configure);
        else
            services.Configure<SeoMetadataOptions>(options => { });

        // Configure cache options (default: disabled)
        if (configureCache != null)
            services.Configure(configureCache);
        else
            services.Configure<SeoCacheOptions>(options => options.Enabled = false);

        // Add memory cache
        services.AddMemoryCache();

        // Register the main service
        services.AddSingleton<OllamaSeoMetadataService>();

        // Register interface - with or without database caching
        services.AddSingleton<ISeoMetadataService>(sp =>
        {
            var cacheOptions = sp.GetRequiredService<IOptions<SeoCacheOptions>>().Value;
            var baseService = sp.GetRequiredService<OllamaSeoMetadataService>();

            // If database cache is disabled, return base service directly
            if (!cacheOptions.Enabled)
            {
                return baseService;
            }

            // Wrap with database caching
            var dbContext = sp.GetService<SeoMetadataDbContext>();
            if (dbContext == null)
            {
                var logger = sp.GetRequiredService<ILogger<OllamaSeoMetadataService>>();
                logger.LogWarning("Database caching enabled but SeoMetadataDbContext not registered. Using memory-only cache.");
                return baseService;
            }

            return new CachedSeoMetadataService(
                baseService,
                dbContext,
                sp.GetRequiredService<IOptions<SeoCacheOptions>>(),
                sp.GetRequiredService<ILogger<CachedSeoMetadataService>>());
        });

        return services;
    }

    /// <summary>
    ///     Add SEO metadata with SQLite database caching
    /// </summary>
    /// <param name="services">Service collection</param>
    /// <param name="connectionString">SQLite connection string (default: Data Source=data/seometadata.db)</param>
    public static IServiceCollection AddSeoMetadataDatabase(
        this IServiceCollection services,
        string? connectionString = null)
    {
        services.AddDbContext<SeoMetadataDbContext>(options =>
        {
            var connStr = connectionString ?? "Data Source=data/seometadata.db";
            options.UseSqlite(connStr);
        });

        // Ensure database is created
        services.AddHostedService<SeoMetadataDatabaseInitializer>();

        return services;
    }

    /// <summary>
    ///     Add SEO metadata with PostgreSQL database caching
    /// </summary>
    /// <param name="services">Service collection</param>
    /// <param name="connectionString">PostgreSQL connection string</param>
    public static IServiceCollection AddSeoMetadataPostgresDatabase(
        this IServiceCollection services,
        string connectionString)
    {
        services.AddDbContext<SeoMetadataDbContext>(options =>
        {
            options.UseNpgsql(connectionString);
        });

        // Ensure database is created
        services.AddHostedService<SeoMetadataDatabaseInitializer>();

        return services;
    }

    /// <summary>
    ///     Configure SEO metadata for blog content
    /// </summary>
    public static IServiceCollection AddSeoMetadataForBlog(
        this IServiceCollection services,
        string siteName,
        string? twitterHandle = null)
    {
        return services.AddSeoMetadata(options =>
        {
            options.SiteName = siteName;
            options.TwitterSite = twitterHandle;
            options.TwitterCardType = "summary_large_image";
            options.EnableDesignTimeGeneration = true;
            options.EnableRuntimeSuggestions = true;
        });
    }

    /// <summary>
    ///     Configure SEO metadata for e-commerce
    /// </summary>
    public static IServiceCollection AddSeoMetadataForEcommerce(
        this IServiceCollection services,
        string siteName,
        string defaultCurrency = "USD")
    {
        return services.AddSeoMetadata(options =>
        {
            options.SiteName = siteName;
            options.TwitterCardType = "summary";
            options.EnableDesignTimeGeneration = false;
            options.EnableRuntimeSuggestions = true;
            options.CacheDuration = TimeSpan.FromHours(1); // Shorter cache for products
        });
    }

    /// <summary>
    ///     Post-configure SEO metadata options
    /// </summary>
    public static IServiceCollection ConfigureSeoMetadata(
        this IServiceCollection services,
        Action<SeoMetadataOptions> configure)
    {
        services.Configure(configure);
        return services;
    }
}

/// <summary>
///     Background service to initialize the SEO metadata database
/// </summary>
internal class SeoMetadataDatabaseInitializer : Microsoft.Extensions.Hosting.IHostedService
{
    private readonly IServiceProvider _serviceProvider;
    private readonly ILogger<SeoMetadataDatabaseInitializer> _logger;

    public SeoMetadataDatabaseInitializer(
        IServiceProvider serviceProvider,
        ILogger<SeoMetadataDatabaseInitializer> logger)
    {
        _serviceProvider = serviceProvider;
        _logger = logger;
    }

    public async Task StartAsync(CancellationToken cancellationToken)
    {
        using var scope = _serviceProvider.CreateScope();
        var dbContext = scope.ServiceProvider.GetService<SeoMetadataDbContext>();

        if (dbContext != null)
        {
            try
            {
                await dbContext.Database.EnsureCreatedAsync(cancellationToken);
                _logger.LogInformation("SEO metadata database initialized");
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to initialize SEO metadata database");
            }
        }
    }

    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;
}
