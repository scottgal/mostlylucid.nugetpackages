namespace Mostlylucid.LlmAccessibilityAuditor.Models;

/// <summary>
///     Configuration options for the accessibility auditor
/// </summary>
public class AccessibilityAuditorOptions
{
    /// <summary>
    ///     Enable the auditor (default: true)
    /// </summary>
    public bool Enabled { get; set; } = true;

    /// <summary>
    ///     Only run in development/debug environments (default: true)
    /// </summary>
    public bool OnlyInDevelopment { get; set; } = true;

    /// <summary>
    ///     HTTP header that enables auditing when present (default: "X-Accessibility-Audit")
    ///     Set to null to disable header-based activation
    /// </summary>
    public string? EnableHeader { get; set; } = "X-Accessibility-Audit";

    /// <summary>
    ///     Header value required to enable auditing (default: "true")
    /// </summary>
    public string EnableHeaderValue { get; set; } = "true";

    /// <summary>
    ///     Enable LLM-based analysis in addition to rule-based checks
    /// </summary>
    public bool EnableLlmAnalysis { get; set; } = true;

    /// <summary>
    ///     Ollama configuration
    /// </summary>
    public OllamaOptions Ollama { get; set; } = new();

    /// <summary>
    ///     Content types to audit (default: text/html only)
    /// </summary>
    public List<string> ContentTypesToAudit { get; set; } = new() { "text/html" };

    /// <summary>
    ///     URL paths to exclude from auditing (supports wildcards)
    /// </summary>
    public List<string> ExcludePaths { get; set; } = new()
    {
        "/api/*",
        "/_*",
        "/health",
        "/metrics"
    };

    /// <summary>
    ///     URL paths to include (if set, only these paths are audited)
    /// </summary>
    public List<string>? IncludePaths { get; set; }

    /// <summary>
    ///     Maximum HTML size to audit in bytes (default: 1MB)
    /// </summary>
    public int MaxHtmlSizeBytes { get; set; } = 1024 * 1024;

    /// <summary>
    ///     Maximum HTML size to send to LLM (default: 32KB - will truncate/summarize)
    /// </summary>
    public int MaxLlmHtmlSizeBytes { get; set; } = 32 * 1024;

    /// <summary>
    ///     Minimum severity to include in reports
    /// </summary>
    public IssueSeverity MinimumSeverity { get; set; } = IssueSeverity.Info;

    /// <summary>
    ///     Enable diagnostic endpoint at /_accessibility (default: true in dev)
    /// </summary>
    public bool EnableDiagnosticEndpoint { get; set; } = true;

    /// <summary>
    ///     Path for the diagnostic endpoint
    /// </summary>
    public string DiagnosticEndpointPath { get; set; } = "/_accessibility";

    /// <summary>
    ///     Store audit history for the diagnostic endpoint
    /// </summary>
    public bool StoreAuditHistory { get; set; } = true;

    /// <summary>
    ///     Maximum number of audit reports to keep in history
    /// </summary>
    public int MaxHistoryCount { get; set; } = 50;

    /// <summary>
    ///     Enable inline HTML report injection (adds warnings to page bottom)
    /// </summary>
    public bool EnableInlineReport { get; set; } = true;

    /// <summary>
    ///     Custom system prompt for LLM analysis
    /// </summary>
    public string? CustomLlmPrompt { get; set; }

    /// <summary>
    ///     Issue types to check (if null, all types are checked)
    /// </summary>
    public List<AccessibilityIssueType>? EnabledChecks { get; set; }

    /// <summary>
    ///     Enable diagnostic logging
    /// </summary>
    public bool EnableDiagnosticLogging { get; set; } = false;
}

/// <summary>
///     Ollama LLM configuration
/// </summary>
public class OllamaOptions
{
    /// <summary>
    ///     Ollama API endpoint (default: http://localhost:11434)
    /// </summary>
    public string Endpoint { get; set; } = "http://localhost:11434";

    /// <summary>
    ///     Model to use for analysis (default: llama3.2:3b)
    /// </summary>
    public string Model { get; set; } = "llama3.2:3b";

    /// <summary>
    ///     Temperature for generation (0.0 - 1.0, default: 0.1 for consistency)
    /// </summary>
    public float Temperature { get; set; } = 0.1f;

    /// <summary>
    ///     Maximum tokens to generate
    /// </summary>
    public int MaxTokens { get; set; } = 4096;

    /// <summary>
    ///     Request timeout in seconds
    /// </summary>
    public int TimeoutSeconds { get; set; } = 60;
}