using Microsoft.Extensions.DependencyInjection;
using Mostlylucid.Common.Caching;

namespace Mostlylucid.Common.Extensions;

/// <summary>
///     Extension methods for common service registration
/// </summary>
public static class ServiceCollectionExtensions
{
    /// <summary>
    ///     Add memory caching service with default options
    /// </summary>
    public static IServiceCollection AddMemoryCachingService<T>(
        this IServiceCollection services,
        Action<MemoryCachingOptions>? configure = null,
        string? keyPrefix = null) where T : class
    {
        services.AddMemoryCache();

        if (configure != null)
        {
            services.Configure(configure);
        }
        else
        {
            services.Configure<MemoryCachingOptions>(opt => { });
        }

        services.AddSingleton<ICachingService<T>>(sp =>
        {
            var cache = sp.GetRequiredService<Microsoft.Extensions.Caching.Memory.IMemoryCache>();
            var options = sp.GetRequiredService<Microsoft.Extensions.Options.IOptions<MemoryCachingOptions>>();
            return new MemoryCachingService<T>(cache, options, keyPrefix ?? typeof(T).Name);
        });

        return services;
    }

    /// <summary>
    ///     Configure options with a default if no configuration provided
    /// </summary>
    public static IServiceCollection ConfigureWithDefault<TOptions>(
        this IServiceCollection services,
        Action<TOptions>? configure = null) where TOptions : class
    {
        if (configure != null)
        {
            services.Configure(configure);
        }
        else
        {
            services.Configure<TOptions>(opt => { });
        }

        return services;
    }
}
