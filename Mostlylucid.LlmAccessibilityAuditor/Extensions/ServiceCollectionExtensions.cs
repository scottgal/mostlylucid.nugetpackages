using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Mostlylucid.LlmAccessibilityAuditor.Middleware;
using Mostlylucid.LlmAccessibilityAuditor.Models;
using Mostlylucid.LlmAccessibilityAuditor.Services;

namespace Mostlylucid.LlmAccessibilityAuditor.Extensions;

/// <summary>
/// Extension methods for configuring LLM Accessibility Auditor services
/// </summary>
public static class ServiceCollectionExtensions
{
    /// <summary>
    /// Add LLM Accessibility Auditor services to the DI container
    /// </summary>
    /// <param name="services">Service collection</param>
    /// <param name="configure">Configuration action</param>
    /// <returns>Service collection for chaining</returns>
    public static IServiceCollection AddAccessibilityAuditor(
        this IServiceCollection services,
        Action<AccessibilityAuditorOptions>? configure = null)
    {
        // Configure options
        var options = new AccessibilityAuditorOptions();
        configure?.Invoke(options);

        if (configure != null)
        {
            services.Configure(configure);
        }
        else
        {
            services.Configure<AccessibilityAuditorOptions>(opt => { });
        }

        // Register core services
        services.AddSingleton<IHtmlAccessibilityParser, HtmlAccessibilityParser>();
        services.AddSingleton<IAuditHistoryService, AuditHistoryService>();
        services.AddScoped<IAccessibilityAuditor, AccessibilityAuditor>();

        // Register Ollama client with HttpClient factory
        services.AddHttpClient<IAccessibilityOllamaClient, AccessibilityOllamaClient>();

        // Required for TagHelper
        services.AddHttpContextAccessor();

        return services;
    }

    /// <summary>
    /// Add LLM Accessibility Auditor services from configuration
    /// </summary>
    /// <param name="services">Service collection</param>
    /// <param name="configuration">Configuration</param>
    /// <param name="sectionName">Configuration section name (default: "AccessibilityAuditor")</param>
    /// <returns>Service collection for chaining</returns>
    public static IServiceCollection AddAccessibilityAuditor(
        this IServiceCollection services,
        IConfiguration configuration,
        string sectionName = "AccessibilityAuditor")
    {
        services.Configure<AccessibilityAuditorOptions>(configuration.GetSection(sectionName));

        // Register core services
        services.AddSingleton<IHtmlAccessibilityParser, HtmlAccessibilityParser>();
        services.AddSingleton<IAuditHistoryService, AuditHistoryService>();
        services.AddScoped<IAccessibilityAuditor, AccessibilityAuditor>();

        // Register Ollama client with HttpClient factory
        services.AddHttpClient<IAccessibilityOllamaClient, AccessibilityOllamaClient>();

        // Required for TagHelper
        services.AddHttpContextAccessor();

        return services;
    }
}

/// <summary>
/// Extension methods for configuring middleware
/// </summary>
public static class ApplicationBuilderExtensions
{
    /// <summary>
    /// Use the accessibility audit middleware
    /// </summary>
    /// <param name="app">Application builder</param>
    /// <returns>Application builder for chaining</returns>
    public static IApplicationBuilder UseAccessibilityAudit(this IApplicationBuilder app)
    {
        return app.UseMiddleware<AccessibilityAuditMiddleware>();
    }
}
