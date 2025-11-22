using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Mostlylucid.LlmPiiRedactor.Models;
using Mostlylucid.LlmPiiRedactor.Services;
using Serilog;
using Serilog.Configuration;

namespace Mostlylucid.LlmPiiRedactor.Serilog;

/// <summary>
/// Serilog extension methods for PII redaction.
/// </summary>
public static class SerilogExtensions
{
    /// <summary>
    /// Adds PII redacting enricher to Serilog configuration.
    /// </summary>
    public static LoggerConfiguration WithPiiRedaction(
        this LoggerEnrichmentConfiguration enrichmentConfiguration,
        IServiceProvider serviceProvider)
    {
        if (enrichmentConfiguration == null)
            throw new ArgumentNullException(nameof(enrichmentConfiguration));

        var redactionService = serviceProvider.GetRequiredService<IPiiRedactionService>();
        return enrichmentConfiguration.With(new PiiRedactingEnricher(redactionService));
    }

    /// <summary>
    /// Adds PII destructuring policy to Serilog configuration.
    /// </summary>
    public static LoggerConfiguration WithPiiDestructuring(
        this LoggerDestructuringConfiguration destructuringConfiguration,
        IServiceProvider serviceProvider)
    {
        if (destructuringConfiguration == null)
            throw new ArgumentNullException(nameof(destructuringConfiguration));

        var redactionService = serviceProvider.GetRequiredService<IPiiRedactionService>();
        var options = serviceProvider.GetRequiredService<IOptions<PiiLoggingOptions>>();

        return destructuringConfiguration.With(new PiiDestructuringPolicy(redactionService, options));
    }

    /// <summary>
    /// Configures Serilog to redact PII from all log messages.
    /// </summary>
    public static LoggerConfiguration RedactPii(
        this LoggerConfiguration loggerConfiguration,
        IServiceProvider serviceProvider)
    {
        if (loggerConfiguration == null)
            throw new ArgumentNullException(nameof(loggerConfiguration));

        return loggerConfiguration
            .Enrich.WithPiiRedaction(serviceProvider)
            .Destructure.WithPiiDestructuring(serviceProvider);
    }
}
