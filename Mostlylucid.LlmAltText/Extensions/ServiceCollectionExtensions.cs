using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Mostlylucid.LlmAltText.Data;
using Mostlylucid.LlmAltText.Models;
using Mostlylucid.LlmAltText.Services;

namespace Mostlylucid.LlmAltText.Extensions;

/// <summary>
///     Extension methods for registering alt text services
/// </summary>
public static class ServiceCollectionExtensions
{
    /// <summary>
    ///     Add AI-powered alt text generation services to the service collection
    /// </summary>
    /// <param name="services">Service collection</param>
    /// <param name="configure">Optional configuration action</param>
    /// <returns>Service collection for chaining</returns>
    /// <example>
    ///     <code>
    /// // Basic usage with defaults
    /// services.AddAltTextGeneration();
    /// 
    /// // With custom configuration
    /// services.AddAltTextGeneration(options =>
    /// {
    ///     options.ModelPath = "./my-models";
    ///     options.EnableDiagnosticLogging = true;
    ///     options.MaxWords = 100;
    /// });
    /// 
    /// // Enable TagHelper with SQLite caching
    /// services.AddAltTextGeneration(options =>
    /// {
    ///     options.EnableTagHelper = true;
    ///     options.EnableDatabase = true;
    ///     options.DbProvider = AltTextDbProvider.Sqlite;
    ///     options.SqliteDbPath = "./alttext.db";
    /// });
    /// 
    /// // Enable TagHelper with PostgreSQL
    /// services.AddAltTextGeneration(options =>
    /// {
    ///     options.EnableTagHelper = true;
    ///     options.EnableDatabase = true;
    ///     options.DbProvider = AltTextDbProvider.PostgreSql;
    ///     options.ConnectionString = "Host=localhost;Database=alttext;Username=user;Password=pass";
    /// });
    /// </code>
    /// </example>
    public static IServiceCollection AddAltTextGeneration(
        this IServiceCollection services,
        Action<AltTextOptions>? configure = null)
    {
        // Build options to check configuration
        var options = new AltTextOptions();
        configure?.Invoke(options);

        // Configure options
        if (configure != null)
            services.Configure(configure);
        else
            services.Configure<AltTextOptions>(opt => { });

        // Register the image analysis service as a singleton
        // (model initialization is expensive, so we share one instance)
        services.AddSingleton<IImageAnalysisService, Florence2ImageAnalysisService>();

        // Register database services if enabled
        if (options.EnableDatabase)
        {
            services.AddDbContext<AltTextDbContext>((sp, dbOptions) =>
            {
                var altTextOptions = sp.GetRequiredService<IOptions<AltTextOptions>>().Value;
                ConfigureDbContext(dbOptions, altTextOptions);
            });

            services.AddScoped<IAltTextRepository, AltTextRepository>();
        }

        // Register HttpClient for image fetching (needed by TagHelper)
        if (options.EnableTagHelper)
            services.AddHttpClient("AltTextImageFetcher",
                client => { client.DefaultRequestHeaders.Add("User-Agent", "MostlylucidAltTextBot/1.0"); });

        return services;
    }

    /// <summary>
    ///     Configure the DbContext based on provider settings
    /// </summary>
    private static void ConfigureDbContext(DbContextOptionsBuilder options, AltTextOptions altTextOptions)
    {
        var connectionString = altTextOptions.ConnectionString;

        switch (altTextOptions.DbProvider)
        {
            case AltTextDbProvider.Sqlite:
                connectionString ??= $"Data Source={altTextOptions.SqliteDbPath}";
                options.UseSqlite(connectionString);
                break;

            case AltTextDbProvider.PostgreSql:
                if (string.IsNullOrEmpty(connectionString))
                    throw new InvalidOperationException(
                        "PostgreSQL connection string is required. Set AltTextOptions.ConnectionString.");
                options.UseNpgsql(connectionString);
                break;

            default:
                throw new ArgumentOutOfRangeException(nameof(altTextOptions.DbProvider));
        }
    }

    /// <summary>
    ///     Migrate the alt text database (call during application startup)
    /// </summary>
    /// <param name="serviceProvider">Service provider</param>
    /// <returns>Task</returns>
    public static async Task MigrateAltTextDatabaseAsync(this IServiceProvider serviceProvider)
    {
        var options = serviceProvider.GetRequiredService<IOptions<AltTextOptions>>().Value;

        if (!options.EnableDatabase || !options.AutoMigrateDatabase) return;

        using var scope = serviceProvider.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<AltTextDbContext>();
        await context.Database.EnsureCreatedAsync();
    }

    /// <summary>
    ///     Update alt text generation options after initial registration
    /// </summary>
    /// <param name="services">Service collection</param>
    /// <param name="configure">Configuration action</param>
    /// <returns>Service collection for chaining</returns>
    public static IServiceCollection ConfigureAltTextGeneration(
        this IServiceCollection services,
        Action<AltTextOptions> configure)
    {
        services.Configure(configure);
        return services;
    }
}