using System.Text.RegularExpressions;

namespace Mostlylucid.LlmLogSummarizer.Models;

/// <summary>
///     Represents a single log entry from any source.
/// </summary>
public class LogEntry
{
    /// <summary>
    ///     Unique identifier for this log entry.
    /// </summary>
    public string Id { get; set; } = Guid.NewGuid().ToString();

    /// <summary>
    ///     Timestamp when the log was generated.
    /// </summary>
    public DateTimeOffset Timestamp { get; set; }

    /// <summary>
    ///     Log level (e.g., Information, Warning, Error, Critical).
    /// </summary>
    public LogLevel Level { get; set; }

    /// <summary>
    ///     The log message content.
    /// </summary>
    public string Message { get; set; } = string.Empty;

    /// <summary>
    ///     Exception type if this is an error log.
    /// </summary>
    public string? ExceptionType { get; set; }

    /// <summary>
    ///     Exception message if available.
    /// </summary>
    public string? ExceptionMessage { get; set; }

    /// <summary>
    ///     Full stack trace if available.
    /// </summary>
    public string? StackTrace { get; set; }

    /// <summary>
    ///     Source context (usually the logger name or class).
    /// </summary>
    public string? SourceContext { get; set; }

    /// <summary>
    ///     Additional structured properties from the log.
    /// </summary>
    public Dictionary<string, object?> Properties { get; set; } = new();

    /// <summary>
    ///     The name of the log source this entry came from.
    /// </summary>
    public string? SourceName { get; set; }

    /// <summary>
    ///     Request ID or correlation ID for tracing.
    /// </summary>
    public string? RequestId { get; set; }

    /// <summary>
    ///     Trace ID for distributed tracing.
    /// </summary>
    public string? TraceId { get; set; }

    /// <summary>
    ///     The raw log line (if applicable).
    /// </summary>
    public string? RawContent { get; set; }

    /// <summary>
    ///     Creates a fingerprint for clustering similar log entries.
    /// </summary>
    public string GetClusteringFingerprint()
    {
        var parts = new List<string>();

        if (!string.IsNullOrEmpty(ExceptionType))
            parts.Add(ExceptionType);

        if (!string.IsNullOrEmpty(SourceContext))
            parts.Add(SourceContext);

        // Normalize the message by removing variable parts (numbers, guids, etc.)
        var normalizedMessage = NormalizeMessage(Message);
        if (!string.IsNullOrEmpty(normalizedMessage))
            parts.Add(normalizedMessage);

        // Include first line of stack trace for more specific clustering
        if (!string.IsNullOrEmpty(StackTrace))
        {
            var firstStackLine = StackTrace.Split('\n').FirstOrDefault()?.Trim();
            if (!string.IsNullOrEmpty(firstStackLine))
                parts.Add(firstStackLine);
        }

        return string.Join("|", parts);
    }

    private static string NormalizeMessage(string message)
    {
        if (string.IsNullOrEmpty(message))
            return string.Empty;

        // Replace GUIDs
        message = Regex.Replace(
            message,
            @"[0-9a-fA-F]{8}-[0-9a-fA-F]{4}-[0-9a-fA-F]{4}-[0-9a-fA-F]{4}-[0-9a-fA-F]{12}",
            "<GUID>");

        // Replace numbers
        message = Regex.Replace(
            message,
            @"\b\d+\b",
            "<N>");

        // Replace quoted strings
        message = Regex.Replace(
            message,
            @"'[^']*'",
            "'<STR>'");

        message = Regex.Replace(
            message,
            "\"[^\"]*\"",
            "\"<STR>\"");

        return message;
    }
}

/// <summary>
///     Log severity levels.
/// </summary>
public enum LogLevel
{
    Trace = 0,
    Debug = 1,
    Information = 2,
    Warning = 3,
    Error = 4,
    Critical = 5
}