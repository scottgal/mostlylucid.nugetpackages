namespace Mostlylucid.LlmPiiRedactor.Models;

/// <summary>
/// Configuration options for PII redaction middleware.
/// </summary>
public class PiiMiddlewareOptions
{
    /// <summary>
    /// Whether middleware is enabled.
    /// </summary>
    public bool Enabled { get; set; } = true;

    /// <summary>
    /// Whether to redact request bodies.
    /// </summary>
    public bool RedactRequestBody { get; set; } = true;

    /// <summary>
    /// Whether to redact response bodies.
    /// </summary>
    public bool RedactResponseBody { get; set; } = true;

    /// <summary>
    /// Whether to redact request headers.
    /// </summary>
    public bool RedactRequestHeaders { get; set; } = true;

    /// <summary>
    /// Whether to redact response headers.
    /// </summary>
    public bool RedactResponseHeaders { get; set; } = false;

    /// <summary>
    /// Whether to redact query strings.
    /// </summary>
    public bool RedactQueryStrings { get; set; } = true;

    /// <summary>
    /// Request paths to exclude from redaction (supports wildcards).
    /// </summary>
    public HashSet<string> ExcludedPaths { get; set; } = new(StringComparer.OrdinalIgnoreCase)
    {
        "/health",
        "/healthz",
        "/ready",
        "/metrics",
        "/swagger/*",
        "/_framework/*"
    };

    /// <summary>
    /// Content types to process for body redaction.
    /// </summary>
    public HashSet<string> ProcessableContentTypes { get; set; } = new(StringComparer.OrdinalIgnoreCase)
    {
        "application/json",
        "application/xml",
        "text/plain",
        "text/html",
        "text/xml",
        "application/x-www-form-urlencoded"
    };

    /// <summary>
    /// Headers to always redact (sensitive headers).
    /// </summary>
    public HashSet<string> SensitiveHeaders { get; set; } = new(StringComparer.OrdinalIgnoreCase)
    {
        "Authorization",
        "X-Api-Key",
        "Cookie",
        "Set-Cookie",
        "X-Auth-Token",
        "X-Access-Token"
    };

    /// <summary>
    /// Maximum request/response body size to process (bytes).
    /// Default: 1MB
    /// </summary>
    public int MaxBodySize { get; set; } = 1024 * 1024;

    /// <summary>
    /// Whether to add a response header indicating redaction occurred.
    /// </summary>
    public bool AddRedactionHeader { get; set; } = false;

    /// <summary>
    /// Name of the header to add when redaction occurs.
    /// </summary>
    public string RedactionHeaderName { get; set; } = "X-Pii-Redacted";
}
