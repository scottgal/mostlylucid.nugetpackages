namespace Mostlylucid.LlmLogSummarizer.Models;

/// <summary>
///     Configuration options for the log summarizer.
/// </summary>
public class LogSummarizerOptions
{
    /// <summary>
    ///     Configuration section name.
    /// </summary>
    public const string SectionName = "LlmLogSummarizer";

    /// <summary>
    ///     Whether the summarizer service is enabled.
    /// </summary>
    public bool Enabled { get; set; } = true;

    /// <summary>
    ///     How often to run summarization (e.g., daily at midnight).
    /// </summary>
    public TimeSpan SummarizationInterval { get; set; } = TimeSpan.FromHours(24);

    /// <summary>
    ///     Time of day to run the summarization (24-hour format, e.g., "02:00" for 2 AM).
    ///     If set, overrides SummarizationInterval for daily runs.
    /// </summary>
    public TimeSpan? DailyRunTime { get; set; }

    /// <summary>
    ///     How far back to look for logs.
    /// </summary>
    public TimeSpan LookbackPeriod { get; set; } = TimeSpan.FromHours(24);

    /// <summary>
    ///     Maximum number of log entries to process per run.
    /// </summary>
    public int MaxEntriesPerRun { get; set; } = 100000;

    /// <summary>
    ///     Number of top error patterns to include in the summary.
    /// </summary>
    public int TopPatternsCount { get; set; } = 10;

    /// <summary>
    ///     Minimum occurrences for an error to be included in the report.
    /// </summary>
    public int MinOccurrencesForReporting { get; set; } = 2;

    /// <summary>
    ///     Enable diagnostic logging.
    /// </summary>
    public bool EnableDiagnosticLogging { get; set; }

    /// <summary>
    ///     Run summarization on service startup.
    /// </summary>
    public bool RunOnStartup { get; set; }

    /// <summary>
    ///     LLM configuration.
    /// </summary>
    public OllamaOptions Ollama { get; set; } = new();

    /// <summary>
    ///     Log source configurations.
    /// </summary>
    public LogSourceOptions Sources { get; set; } = new();

    /// <summary>
    ///     Output configurations.
    /// </summary>
    public OutputOptions Output { get; set; } = new();

    /// <summary>
    ///     Clustering configuration.
    /// </summary>
    public ClusteringOptions Clustering { get; set; } = new();
}

/// <summary>
///     Ollama LLM configuration.
/// </summary>
public class OllamaOptions
{
    /// <summary>
    ///     Ollama API endpoint.
    /// </summary>
    public string Endpoint { get; set; } = "http://localhost:11434";

    /// <summary>
    ///     Model to use for summarization (e.g., llama3.2:3b, mistral, phi3).
    /// </summary>
    public string Model { get; set; } = "llama3.2:3b";

    /// <summary>
    ///     Temperature for generation (lower = more deterministic).
    /// </summary>
    public float Temperature { get; set; } = 0.3f;

    /// <summary>
    ///     Maximum tokens to generate.
    /// </summary>
    public int MaxTokens { get; set; } = 2048;

    /// <summary>
    ///     Request timeout in seconds.
    /// </summary>
    public int TimeoutSeconds { get; set; } = 120;

    /// <summary>
    ///     Whether to use streaming responses.
    /// </summary>
    public bool UseStreaming { get; set; }
}

/// <summary>
///     Log source configuration.
/// </summary>
public class LogSourceOptions
{
    /// <summary>
    ///     Serilog JSON file sources.
    /// </summary>
    public List<SerilogSourceConfig> SerilogFiles { get; set; } = new();

    /// <summary>
    ///     Plain text log file sources.
    /// </summary>
    public List<TextLogSourceConfig> TextFiles { get; set; } = new();

    /// <summary>
    ///     Application Insights configuration.
    /// </summary>
    public AppInsightsSourceConfig? AppInsights { get; set; }
}

/// <summary>
///     Serilog JSON log source configuration.
/// </summary>
public class SerilogSourceConfig
{
    /// <summary>
    ///     Name for this source.
    /// </summary>
    public string Name { get; set; } = "Serilog";

    /// <summary>
    ///     Path or glob pattern for log files.
    /// </summary>
    public string Path { get; set; } = string.Empty;

    /// <summary>
    ///     Whether to include archived/rotated logs.
    /// </summary>
    public bool IncludeArchived { get; set; } = true;

    /// <summary>
    ///     File encoding (default: UTF-8).
    /// </summary>
    public string Encoding { get; set; } = "utf-8";
}

/// <summary>
///     Text log source configuration.
/// </summary>
public class TextLogSourceConfig
{
    /// <summary>
    ///     Name for this source.
    /// </summary>
    public string Name { get; set; } = "TextLog";

    /// <summary>
    ///     Path or glob pattern for log files.
    /// </summary>
    public string Path { get; set; } = string.Empty;

    /// <summary>
    ///     Regex pattern to parse log lines.
    ///     Default pattern: [Timestamp] [Level] Message
    /// </summary>
    public string? ParsePattern { get; set; }

    /// <summary>
    ///     Timestamp format in logs.
    /// </summary>
    public string TimestampFormat { get; set; } = "yyyy-MM-dd HH:mm:ss.fff";

    /// <summary>
    ///     Whether to include archived/rotated logs.
    /// </summary>
    public bool IncludeArchived { get; set; } = true;
}

/// <summary>
///     Application Insights source configuration.
/// </summary>
public class AppInsightsSourceConfig
{
    /// <summary>
    ///     Name for this source.
    /// </summary>
    public string Name { get; set; } = "AppInsights";

    /// <summary>
    ///     Application Insights connection string.
    /// </summary>
    public string ConnectionString { get; set; } = string.Empty;

    /// <summary>
    ///     Application ID for API access.
    /// </summary>
    public string? ApplicationId { get; set; }

    /// <summary>
    ///     API key for read access.
    /// </summary>
    public string? ApiKey { get; set; }
}

/// <summary>
///     Output configuration.
/// </summary>
public class OutputOptions
{
    /// <summary>
    ///     Markdown file output.
    /// </summary>
    public MarkdownOutputConfig? Markdown { get; set; }

    /// <summary>
    ///     Email output.
    /// </summary>
    public EmailOutputConfig? Email { get; set; }

    /// <summary>
    ///     Slack webhook output.
    /// </summary>
    public SlackOutputConfig? Slack { get; set; }

    /// <summary>
    ///     Generic webhook output.
    /// </summary>
    public WebhookOutputConfig? Webhook { get; set; }
}

/// <summary>
///     Markdown output configuration.
/// </summary>
public class MarkdownOutputConfig
{
    /// <summary>
    ///     Whether this output is enabled.
    /// </summary>
    public bool Enabled { get; set; } = true;

    /// <summary>
    ///     Output directory for markdown files.
    /// </summary>
    public string OutputDirectory { get; set; } = "./logs/summaries";

    /// <summary>
    ///     File name pattern. Supports: {date}, {time}, {period}
    /// </summary>
    public string FileNamePattern { get; set; } = "log-summary-{date}.md";

    /// <summary>
    ///     Maximum files to keep (0 = unlimited).
    /// </summary>
    public int MaxFilesToKeep { get; set; } = 30;
}

/// <summary>
///     Email output configuration.
/// </summary>
public class EmailOutputConfig
{
    /// <summary>
    ///     Whether this output is enabled.
    /// </summary>
    public bool Enabled { get; set; }

    /// <summary>
    ///     SMTP server address.
    /// </summary>
    public string SmtpHost { get; set; } = string.Empty;

    /// <summary>
    ///     SMTP port.
    /// </summary>
    public int SmtpPort { get; set; } = 587;

    /// <summary>
    ///     Use SSL/TLS.
    /// </summary>
    public bool UseSsl { get; set; } = true;

    /// <summary>
    ///     SMTP username.
    /// </summary>
    public string? Username { get; set; }

    /// <summary>
    ///     SMTP password.
    /// </summary>
    public string? Password { get; set; }

    /// <summary>
    ///     From email address.
    /// </summary>
    public string FromAddress { get; set; } = string.Empty;

    /// <summary>
    ///     From display name.
    /// </summary>
    public string FromName { get; set; } = "Log Summarizer";

    /// <summary>
    ///     Recipient email addresses.
    /// </summary>
    public List<string> ToAddresses { get; set; } = new();

    /// <summary>
    ///     Subject line pattern. Supports: {date}, {errorCount}, {health}
    /// </summary>
    public string SubjectPattern { get; set; } = "[Log Summary] {date} - {health} ({errorCount} errors)";

    /// <summary>
    ///     Only send email if there are errors.
    /// </summary>
    public bool OnlyOnErrors { get; set; }
}

/// <summary>
///     Slack webhook output configuration.
/// </summary>
public class SlackOutputConfig
{
    /// <summary>
    ///     Whether this output is enabled.
    /// </summary>
    public bool Enabled { get; set; }

    /// <summary>
    ///     Slack webhook URL.
    /// </summary>
    public string WebhookUrl { get; set; } = string.Empty;

    /// <summary>
    ///     Channel to post to (optional, uses webhook default).
    /// </summary>
    public string? Channel { get; set; }

    /// <summary>
    ///     Username for the bot.
    /// </summary>
    public string Username { get; set; } = "Log Summarizer";

    /// <summary>
    ///     Icon emoji for the bot.
    /// </summary>
    public string IconEmoji { get; set; } = ":robot_face:";

    /// <summary>
    ///     Only post if there are errors.
    /// </summary>
    public bool OnlyOnErrors { get; set; }
}

/// <summary>
///     Generic webhook output configuration.
/// </summary>
public class WebhookOutputConfig
{
    /// <summary>
    ///     Whether this output is enabled.
    /// </summary>
    public bool Enabled { get; set; }

    /// <summary>
    ///     Webhook URL.
    /// </summary>
    public string Url { get; set; } = string.Empty;

    /// <summary>
    ///     HTTP method (POST, PUT).
    /// </summary>
    public string Method { get; set; } = "POST";

    /// <summary>
    ///     Additional headers.
    /// </summary>
    public Dictionary<string, string> Headers { get; set; } = new();

    /// <summary>
    ///     Only send if there are errors.
    /// </summary>
    public bool OnlyOnErrors { get; set; }
}

/// <summary>
///     Clustering configuration.
/// </summary>
public class ClusteringOptions
{
    /// <summary>
    ///     Similarity threshold for clustering (0.0 - 1.0).
    ///     Higher = stricter matching.
    /// </summary>
    public double SimilarityThreshold { get; set; } = 0.8;

    /// <summary>
    ///     Whether to use Levenshtein distance for similarity.
    /// </summary>
    public bool UseLevenshteinDistance { get; set; } = true;

    /// <summary>
    ///     Maximum clusters to create.
    /// </summary>
    public int MaxClusters { get; set; } = 100;

    /// <summary>
    ///     Minimum entries for a cluster to be reported.
    /// </summary>
    public int MinClusterSize { get; set; } = 1;
}