using System.Text.Json;
using Microsoft.Extensions.Logging;
using Mostlylucid.LlmLogSummarizer.Models;

namespace Mostlylucid.LlmLogSummarizer.Sources;

/// <summary>
/// Log source for Serilog compact JSON format files.
/// </summary>
public class SerilogJsonLogSource : ILogSource
{
    private readonly SerilogSourceConfig _config;
    private readonly ILogger<SerilogJsonLogSource> _logger;

    public SerilogJsonLogSource(SerilogSourceConfig config, ILogger<SerilogJsonLogSource> logger)
    {
        _config = config;
        _logger = logger;
    }

    public string Name => _config.Name;

    public bool IsAvailable => !string.IsNullOrEmpty(_config.Path);

    public async IAsyncEnumerable<LogEntry> GetEntriesAsync(
        DateTimeOffset from,
        DateTimeOffset to,
        int maxEntries,
        [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        var files = GetLogFiles(from, to);
        var entriesYielded = 0;

        foreach (var file in files)
        {
            if (cancellationToken.IsCancellationRequested)
                yield break;

            await foreach (var entry in ReadFileAsync(file, from, to, cancellationToken))
            {
                if (entriesYielded >= maxEntries)
                    yield break;

                yield return entry;
                entriesYielded++;
            }
        }

        _logger.LogDebug("Read {Count} entries from Serilog source {Name}", entriesYielded, Name);
    }

    public Task<bool> TestConnectionAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            var files = GetLogFiles(DateTimeOffset.UtcNow.AddDays(-1), DateTimeOffset.UtcNow);
            return Task.FromResult(files.Any());
        }
        catch
        {
            return Task.FromResult(false);
        }
    }

    private IEnumerable<string> GetLogFiles(DateTimeOffset from, DateTimeOffset to)
    {
        var path = _config.Path;

        // Handle glob patterns
        if (path.Contains('*') || path.Contains('?'))
        {
            var directory = Path.GetDirectoryName(path) ?? ".";
            var pattern = Path.GetFileName(path);

            if (!Directory.Exists(directory))
                return Enumerable.Empty<string>();

            return Directory.EnumerateFiles(directory, pattern)
                .Where(f => ShouldIncludeFile(f, from, to))
                .OrderByDescending(f => new FileInfo(f).LastWriteTimeUtc);
        }

        // Single file
        if (File.Exists(path))
            return new[] { path };

        // Directory with default pattern
        if (Directory.Exists(path))
        {
            return Directory.EnumerateFiles(path, "*.json")
                .Concat(Directory.EnumerateFiles(path, "*.clef"))
                .Where(f => ShouldIncludeFile(f, from, to))
                .OrderByDescending(f => new FileInfo(f).LastWriteTimeUtc);
        }

        return Enumerable.Empty<string>();
    }

    private bool ShouldIncludeFile(string filePath, DateTimeOffset from, DateTimeOffset to)
    {
        var fileInfo = new FileInfo(filePath);

        // Skip if file was last modified before our time range
        if (fileInfo.LastWriteTimeUtc < from.UtcDateTime && !_config.IncludeArchived)
            return false;

        return true;
    }

    private async IAsyncEnumerable<LogEntry> ReadFileAsync(
        string filePath,
        DateTimeOffset from,
        DateTimeOffset to,
        [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken cancellationToken)
    {
        var encoding = System.Text.Encoding.GetEncoding(_config.Encoding);

        using var stream = new FileStream(filePath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
        using var reader = new StreamReader(stream, encoding);

        string? line;
        while ((line = await reader.ReadLineAsync(cancellationToken)) != null)
        {
            if (cancellationToken.IsCancellationRequested)
                yield break;

            if (string.IsNullOrWhiteSpace(line))
                continue;

            var entry = ParseSerilogLine(line, filePath);
            if (entry == null)
                continue;

            // Filter by time range
            if (entry.Timestamp < from || entry.Timestamp > to)
                continue;

            yield return entry;
        }
    }

    private LogEntry? ParseSerilogLine(string line, string filePath)
    {
        try
        {
            using var doc = JsonDocument.Parse(line);
            var root = doc.RootElement;

            var entry = new LogEntry
            {
                SourceName = Name,
                RawContent = line
            };

            // Parse timestamp (Serilog uses @t for compact format)
            if (root.TryGetProperty("@t", out var timestamp))
            {
                entry.Timestamp = DateTimeOffset.Parse(timestamp.GetString()!);
            }
            else if (root.TryGetProperty("Timestamp", out var ts))
            {
                entry.Timestamp = DateTimeOffset.Parse(ts.GetString()!);
            }

            // Parse level (Serilog uses @l for compact format)
            if (root.TryGetProperty("@l", out var level))
            {
                entry.Level = ParseLogLevel(level.GetString());
            }
            else if (root.TryGetProperty("Level", out var lvl))
            {
                entry.Level = ParseLogLevel(lvl.GetString());
            }

            // Parse message (Serilog uses @m for compact format)
            if (root.TryGetProperty("@m", out var message))
            {
                entry.Message = message.GetString() ?? string.Empty;
            }
            else if (root.TryGetProperty("RenderedMessage", out var rm))
            {
                entry.Message = rm.GetString() ?? string.Empty;
            }
            else if (root.TryGetProperty("Message", out var msg))
            {
                entry.Message = msg.GetString() ?? string.Empty;
            }

            // Parse exception (Serilog uses @x for compact format)
            if (root.TryGetProperty("@x", out var exception))
            {
                var exText = exception.GetString() ?? string.Empty;
                ParseExceptionText(entry, exText);
            }
            else if (root.TryGetProperty("Exception", out var ex))
            {
                var exText = ex.GetString() ?? string.Empty;
                ParseExceptionText(entry, exText);
            }

            // Parse source context
            if (root.TryGetProperty("SourceContext", out var sourceContext))
            {
                entry.SourceContext = sourceContext.GetString();
            }

            // Parse other properties
            foreach (var prop in root.EnumerateObject())
            {
                if (prop.Name.StartsWith("@") || IsStandardProperty(prop.Name))
                    continue;

                entry.Properties[prop.Name] = GetPropertyValue(prop.Value);
            }

            // Special handling for request/trace IDs
            if (root.TryGetProperty("RequestId", out var requestId))
                entry.RequestId = requestId.GetString();
            if (root.TryGetProperty("TraceId", out var traceId))
                entry.TraceId = traceId.GetString();

            return entry;
        }
        catch (Exception ex)
        {
            _logger.LogDebug(ex, "Failed to parse log line from {File}", filePath);
            return null;
        }
    }

    private static void ParseExceptionText(LogEntry entry, string exceptionText)
    {
        if (string.IsNullOrEmpty(exceptionText))
            return;

        entry.StackTrace = exceptionText;

        // Try to extract exception type and message from first line
        var firstLine = exceptionText.Split('\n').FirstOrDefault()?.Trim();
        if (!string.IsNullOrEmpty(firstLine))
        {
            var colonIndex = firstLine.IndexOf(':');
            if (colonIndex > 0)
            {
                entry.ExceptionType = firstLine[..colonIndex].Trim();
                entry.ExceptionMessage = firstLine[(colonIndex + 1)..].Trim();
            }
            else
            {
                entry.ExceptionType = firstLine;
            }
        }
    }

    private static LogLevel ParseLogLevel(string? level)
    {
        return level?.ToLowerInvariant() switch
        {
            "verbose" or "trace" => LogLevel.Trace,
            "debug" => LogLevel.Debug,
            "information" or "info" => LogLevel.Information,
            "warning" or "warn" => LogLevel.Warning,
            "error" or "err" => LogLevel.Error,
            "fatal" or "critical" => LogLevel.Critical,
            _ => LogLevel.Information
        };
    }

    private static bool IsStandardProperty(string name)
    {
        return name is "Timestamp" or "Level" or "Message" or "RenderedMessage"
            or "Exception" or "SourceContext" or "RequestId" or "TraceId";
    }

    private static object? GetPropertyValue(JsonElement element)
    {
        return element.ValueKind switch
        {
            JsonValueKind.String => element.GetString(),
            JsonValueKind.Number => element.TryGetInt64(out var l) ? l : element.GetDouble(),
            JsonValueKind.True => true,
            JsonValueKind.False => false,
            JsonValueKind.Null => null,
            _ => element.ToString()
        };
    }
}
