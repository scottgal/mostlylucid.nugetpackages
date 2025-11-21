using Microsoft.Extensions.DependencyInjection;
using Mostlylucid.GeoDetection.Models;
using Mostlylucid.GeoDetection.Services;

namespace Mostlylucid.GeoDetection.Extensions;

/// <summary>
///     Extension methods for configuring geo-routing services
/// </summary>
public static class ServiceCollectionExtensions
{
    /// <summary>
    ///     Add geo-routing services
    /// </summary>
    public static IServiceCollection AddGeoRouting(
        this IServiceCollection services,
        Action<GeoRoutingOptions>? configure = null)
    {
        if (configure != null)
            services.Configure(configure);
        else
            services.Configure<GeoRoutingOptions>(options => { });

        // Register geo location service
        services.AddSingleton<IGeoLocationService, SimpleGeoLocationService>();

        return services;
    }

    /// <summary>
    ///     Configure site to only allow specific countries
    /// </summary>
    public static IServiceCollection RestrictSiteToCountries(
        this IServiceCollection services,
        params string[] countryCodes)
    {
        return services.AddGeoRouting(options =>
        {
            options.AllowedCountries = countryCodes;
            options.Enabled = true;
        });
    }

    /// <summary>
    ///     Configure site to block specific countries
    /// </summary>
    public static IServiceCollection BlockCountries(
        this IServiceCollection services,
        params string[] countryCodes)
    {
        return services.AddGeoRouting(options =>
        {
            options.BlockedCountries = countryCodes;
            options.Enabled = true;
        });
    }
}