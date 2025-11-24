using Microsoft.Extensions.Logging;

namespace Mostlylucid.LlmPiiRedactor.Models;

/// <summary>
///     Configuration options for PII redaction in logging.
/// </summary>
public class PiiLoggingOptions
{
    /// <summary>
    ///     Whether logging redaction is enabled.
    /// </summary>
    public bool Enabled { get; set; } = true;

    /// <summary>
    ///     Whether to redact log message templates.
    /// </summary>
    public bool RedactMessageTemplates { get; set; } = true;

    /// <summary>
    ///     Whether to redact structured log properties.
    /// </summary>
    public bool RedactStructuredProperties { get; set; } = true;

    /// <summary>
    ///     Whether to redact exception messages.
    /// </summary>
    public bool RedactExceptions { get; set; } = true;

    /// <summary>
    ///     Whether to redact exception stack traces.
    /// </summary>
    public bool RedactStackTraces { get; set; } = true;

    /// <summary>
    ///     Property names to always redact (case-insensitive).
    /// </summary>
    public HashSet<string> SensitivePropertyNames { get; set; } = new(StringComparer.OrdinalIgnoreCase)
    {
        "Password",
        "Secret",
        "Token",
        "ApiKey",
        "ConnectionString",
        "Credentials",
        "PrivateKey",
        "AccessKey",
        "SecretKey"
    };

    /// <summary>
    ///     Property names to exclude from redaction.
    /// </summary>
    public HashSet<string> ExcludedPropertyNames { get; set; } = new(StringComparer.OrdinalIgnoreCase);

    /// <summary>
    ///     Log categories to exclude from redaction.
    /// </summary>
    public HashSet<string> ExcludedCategories { get; set; } = new(StringComparer.OrdinalIgnoreCase)
    {
        "Microsoft.AspNetCore.Hosting.Diagnostics",
        "Microsoft.AspNetCore.Routing.EndpointMiddleware"
    };

    /// <summary>
    ///     Minimum log level to apply redaction.
    /// </summary>
    public LogLevel MinLogLevel { get; set; } =
        LogLevel.Trace;
}