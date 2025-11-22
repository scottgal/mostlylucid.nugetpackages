using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.DependencyInjection;
using Mostlylucid.LlmPiiRedactor.Detectors;
using Mostlylucid.LlmPiiRedactor.Filters;
using Mostlylucid.LlmPiiRedactor.Middleware;
using Mostlylucid.LlmPiiRedactor.Models;
using Mostlylucid.LlmPiiRedactor.Services;

namespace Mostlylucid.LlmPiiRedactor.Extensions;

/// <summary>
/// Extension methods for registering PII redaction services.
/// </summary>
public static class ServiceCollectionExtensions
{
    /// <summary>
    /// Adds PII redaction services with default configuration.
    /// </summary>
    public static IServiceCollection AddPiiRedaction(this IServiceCollection services)
    {
        return services.AddPiiRedaction(_ => { }, _ => { }, _ => { });
    }

    /// <summary>
    /// Adds PII redaction services with custom configuration.
    /// </summary>
    /// <param name="services">The service collection.</param>
    /// <param name="configureRedaction">Configure redaction options.</param>
    /// <param name="configureMiddleware">Configure middleware options.</param>
    /// <param name="configureLogging">Configure logging options.</param>
    public static IServiceCollection AddPiiRedaction(
        this IServiceCollection services,
        Action<PiiRedactionOptions>? configureRedaction = null,
        Action<PiiMiddlewareOptions>? configureMiddleware = null,
        Action<PiiLoggingOptions>? configureLogging = null)
    {
        // Configure options
        services.Configure<PiiRedactionOptions>(options =>
        {
            configureRedaction?.Invoke(options);
        });

        services.Configure<PiiMiddlewareOptions>(options =>
        {
            configureMiddleware?.Invoke(options);
        });

        services.Configure<PiiLoggingOptions>(options =>
        {
            configureLogging?.Invoke(options);
        });

        // Register detectors
        services.AddSingleton<IPiiDetector, EmailDetector>();
        services.AddSingleton<IPiiDetector, PhoneDetector>();
        services.AddSingleton<IPiiDetector, CreditCardDetector>();
        services.AddSingleton<IPiiDetector, SsnDetector>();
        services.AddSingleton<IPiiDetector, IpAddressDetector>();
        services.AddSingleton<IPiiDetector, NameDetector>();
        services.AddSingleton<IPiiDetector, AddressDetector>();
        services.AddSingleton<IPiiDetector, PostCodeDetector>();
        services.AddSingleton<IPiiDetector, BankAccountDetector>();
        services.AddSingleton<IPiiDetector, ApiKeyDetector>();
        services.AddSingleton<IPiiDetector, NationalInsuranceDetector>();
        services.AddSingleton<IPiiDetector, DateOfBirthDetector>();
        services.AddSingleton<IPiiDetector, AccountIdDetector>();

        // Register redaction strategies
        services.AddSingleton<IRedactionStrategy, FullMaskStrategy>();
        services.AddSingleton<IRedactionStrategy, PartialMaskStrategy>();
        services.AddSingleton<IRedactionStrategy, TokenizedStrategy>();
        services.AddSingleton<IRedactionStrategy, TypeLabelStrategy>();
        services.AddSingleton<IRedactionStrategy, HashedStrategy>();
        services.AddSingleton<IRedactionStrategy, RemoveStrategy>();

        // Register main service
        services.AddSingleton<IPiiRedactionService, PiiRedactionService>();

        // Register filters
        services.AddScoped<PiiExceptionFilter>();

        return services;
    }

    /// <summary>
    /// Adds PII redaction with full mask style as default.
    /// </summary>
    public static IServiceCollection AddPiiRedactionWithFullMask(
        this IServiceCollection services,
        Action<PiiRedactionOptions>? configureRedaction = null)
    {
        return services.AddPiiRedaction(options =>
        {
            options.DefaultStyle = RedactionStyle.FullMask;
            configureRedaction?.Invoke(options);
        });
    }

    /// <summary>
    /// Adds PII redaction with partial mask style as default.
    /// </summary>
    public static IServiceCollection AddPiiRedactionWithPartialMask(
        this IServiceCollection services,
        Action<PiiRedactionOptions>? configureRedaction = null)
    {
        return services.AddPiiRedaction(options =>
        {
            options.DefaultStyle = RedactionStyle.PartialMask;
            configureRedaction?.Invoke(options);
        });
    }

    /// <summary>
    /// Adds PII redaction with tokenized style as default (useful for debugging).
    /// </summary>
    public static IServiceCollection AddPiiRedactionWithTokens(
        this IServiceCollection services,
        Action<PiiRedactionOptions>? configureRedaction = null)
    {
        return services.AddPiiRedaction(options =>
        {
            options.DefaultStyle = RedactionStyle.Tokenized;
            configureRedaction?.Invoke(options);
        });
    }

    /// <summary>
    /// Adds PII redaction for specific PII types only.
    /// </summary>
    public static IServiceCollection AddPiiRedactionForTypes(
        this IServiceCollection services,
        PiiType typesToDetect,
        Action<PiiRedactionOptions>? configureRedaction = null)
    {
        return services.AddPiiRedaction(options =>
        {
            options.DetectionTypes = typesToDetect;
            configureRedaction?.Invoke(options);
        });
    }

    /// <summary>
    /// Adds PII redaction optimized for GDPR compliance.
    /// Focuses on EU-relevant PII types with full redaction.
    /// </summary>
    public static IServiceCollection AddGdprCompliantRedaction(
        this IServiceCollection services,
        Action<PiiRedactionOptions>? configureRedaction = null)
    {
        return services.AddPiiRedaction(options =>
        {
            options.DetectionTypes = PiiType.Email | PiiType.PhoneNumber | PiiType.Name |
                                     PiiType.Address | PiiType.PostCode | PiiType.IpAddress |
                                     PiiType.DateOfBirth | PiiType.NationalInsurance |
                                     PiiType.BankAccount;
            options.DefaultStyle = RedactionStyle.FullMask;
            options.MinConfidenceThreshold = 0.6; // Lower threshold for better coverage
            configureRedaction?.Invoke(options);
        });
    }

    /// <summary>
    /// Adds PII redaction optimized for PCI-DSS compliance.
    /// Focuses on payment card data with strict redaction.
    /// </summary>
    public static IServiceCollection AddPciCompliantRedaction(
        this IServiceCollection services,
        Action<PiiRedactionOptions>? configureRedaction = null)
    {
        return services.AddPiiRedaction(options =>
        {
            options.DetectionTypes = PiiType.CreditCard | PiiType.BankAccount |
                                     PiiType.AccountId;
            options.DefaultStyle = RedactionStyle.PartialMask;
            options.StyleOverrides[PiiType.CreditCard] = RedactionStyle.PartialMask; // Show last 4
            options.MinConfidenceThreshold = 0.8;
            configureRedaction?.Invoke(options);
        });
    }
}

/// <summary>
/// Extension methods for configuring PII redaction middleware.
/// </summary>
public static class ApplicationBuilderExtensions
{
    /// <summary>
    /// Adds PII redaction middleware to the request pipeline.
    /// </summary>
    public static IApplicationBuilder UsePiiRedaction(this IApplicationBuilder builder)
    {
        return builder.UseMiddleware<PiiRedactionMiddleware>();
    }
}
