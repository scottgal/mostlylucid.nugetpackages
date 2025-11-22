using Microsoft.Extensions.DependencyInjection;
using Mostlylucid.DocumentConversion.Interfaces;
using Mostlylucid.DocumentConversion.Services;

namespace Mostlylucid.DocumentConversion.Extensions;

/// <summary>
/// Extension methods for setting up document conversion services
/// </summary>
public static class ServiceCollectionExtensions
{
    /// <summary>
    /// Adds document conversion services to the service collection
    /// </summary>
    /// <param name="services">The service collection</param>
    /// <returns>The service collection for chaining</returns>
    public static IServiceCollection AddDocumentConversion(this IServiceCollection services)
    {
        // Register individual conversion services
        services.AddScoped<IWordDocumentService, WordDocumentService>();
        services.AddScoped<IMarkdownConversionService, MarkdownConversionService>();
        services.AddScoped<IPdfConversionService, PdfConversionService>();

        // Register the main orchestration service
        services.AddScoped<IDocumentConversionService, DocumentConversionService>();

        return services;
    }

    /// <summary>
    /// Adds document conversion services as singletons (for better performance in high-throughput scenarios)
    /// </summary>
    /// <param name="services">The service collection</param>
    /// <returns>The service collection for chaining</returns>
    public static IServiceCollection AddDocumentConversionSingleton(this IServiceCollection services)
    {
        // Register individual conversion services
        services.AddSingleton<IWordDocumentService, WordDocumentService>();
        services.AddSingleton<IMarkdownConversionService, MarkdownConversionService>();
        services.AddSingleton<IPdfConversionService, PdfConversionService>();

        // Register the main orchestration service
        services.AddSingleton<IDocumentConversionService, DocumentConversionService>();

        return services;
    }
}
