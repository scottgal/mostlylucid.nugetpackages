using System.Text.RegularExpressions;
using Microsoft.Extensions.Logging;
using Mostlylucid.LlmLogSummarizer.Models;

namespace Mostlylucid.LlmLogSummarizer.Sources;

/// <summary>
/// Log source for plain text log files.
/// </summary>
public class TextLogSource : ILogSource
{
    private readonly TextLogSourceConfig _config;
    private readonly ILogger<TextLogSource> _logger;
    private readonly Regex? _parseRegex;

    // Default pattern: 2024-01-15 10:30:45.123 [Error] Some message here
    private const string DefaultPattern = @"^(?<timestamp>\d{4}-\d{2}-\d{2}\s+\d{2}:\d{2}:\d{2}(?:\.\d+)?)\s*\[?(?<level>\w+)\]?\s*(?<message>.*)$";

    public TextLogSource(TextLogSourceConfig config, ILogger<TextLogSource> logger)
    {
        _config = config;
        _logger = logger;

        var pattern = config.ParsePattern ?? DefaultPattern;
        _parseRegex = new Regex(pattern, RegexOptions.Compiled | RegexOptions.IgnoreCase);
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

        _logger.LogDebug("Read {Count} entries from text log source {Name}", entriesYielded, Name);
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
                .Where(f => ShouldIncludeFile(f, from))
                .OrderByDescending(f => new FileInfo(f).LastWriteTimeUtc);
        }

        // Single file
        if (File.Exists(path))
            return new[] { path };

        // Directory with default pattern
        if (Directory.Exists(path))
        {
            return Directory.EnumerateFiles(path, "*.log")
                .Concat(Directory.EnumerateFiles(path, "*.txt"))
                .Where(f => ShouldIncludeFile(f, from))
                .OrderByDescending(f => new FileInfo(f).LastWriteTimeUtc);
        }

        return Enumerable.Empty<string>();
    }

    private bool ShouldIncludeFile(string filePath, DateTimeOffset from)
    {
        var fileInfo = new FileInfo(filePath);
        return fileInfo.LastWriteTimeUtc >= from.UtcDateTime || _config.IncludeArchived;
    }

    private async IAsyncEnumerable<LogEntry> ReadFileAsync(
        string filePath,
        DateTimeOffset from,
        DateTimeOffset to,
        [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken cancellationToken)
    {
        using var stream = new FileStream(filePath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
        using var reader = new StreamReader(stream);

        LogEntry? currentEntry = null;
        var multilineBuffer = new List<string>();

        string? line;
        while ((line = await reader.ReadLineAsync(cancellationToken)) != null)
        {
            if (cancellationToken.IsCancellationRequested)
                yield break;

            if (string.IsNullOrWhiteSpace(line))
                continue;

            var match = _parseRegex?.Match(line);
            if (match is { Success: true })
            {
                // Yield previous entry if exists
                if (currentEntry != null)
                {
                    FinalizeEntry(currentEntry, multilineBuffer);
                    if (currentEntry.Timestamp >= from && currentEntry.Timestamp <= to)
                        yield return currentEntry;
                }

                // Start new entry
                currentEntry = ParseLogLine(match, line);
                multilineBuffer.Clear();
            }
            else if (currentEntry != null)
            {
                // This is a continuation line (stack trace, etc.)
                multilineBuffer.Add(line);
            }
        }

        // Don't forget the last entry
        if (currentEntry != null)
        {
            FinalizeEntry(currentEntry, multilineBuffer);
            if (currentEntry.Timestamp >= from && currentEntry.Timestamp <= to)
                yield return currentEntry;
        }
    }

    private LogEntry ParseLogLine(Match match, string rawLine)
    {
        var entry = new LogEntry
        {
            SourceName = Name,
            RawContent = rawLine
        };

        if (match.Groups.TryGetValue("timestamp", out var tsGroup) && tsGroup.Success)
        {
            if (DateTimeOffset.TryParseExact(tsGroup.Value, _config.TimestampFormat,
                    null, System.Globalization.DateTimeStyles.AssumeLocal, out var ts))
            {
                entry.Timestamp = ts;
            }
            else if (DateTimeOffset.TryParse(tsGroup.Value, out ts))
            {
                entry.Timestamp = ts;
            }
        }

        if (match.Groups.TryGetValue("level", out var levelGroup) && levelGroup.Success)
        {
            entry.Level = ParseLogLevel(levelGroup.Value);
        }

        if (match.Groups.TryGetValue("message", out var msgGroup) && msgGroup.Success)
        {
            entry.Message = msgGroup.Value;
        }

        if (match.Groups.TryGetValue("source", out var srcGroup) && srcGroup.Success)
        {
            entry.SourceContext = srcGroup.Value;
        }

        return entry;
    }

    private static void FinalizeEntry(LogEntry entry, List<string> multilineBuffer)
    {
        if (multilineBuffer.Count == 0)
            return;

        var additionalText = string.Join(Environment.NewLine, multilineBuffer);

        // Check if it looks like a stack trace
        if (additionalText.Contains(" at ") && additionalText.Contains(" in "))
        {
            entry.StackTrace = additionalText;
            TryExtractExceptionInfo(entry, multilineBuffer);
        }
        else
        {
            // Append to message
            entry.Message = entry.Message + Environment.NewLine + additionalText;
        }
    }

    private static void TryExtractExceptionInfo(LogEntry entry, List<string> lines)
    {
        var firstLine = lines.FirstOrDefault();
        if (string.IsNullOrEmpty(firstLine))
            return;

        // Look for patterns like "System.NullReferenceException: Object reference not set..."
        var colonIndex = firstLine.IndexOf(':');
        if (colonIndex > 0 && firstLine[..colonIndex].Contains('.'))
        {
            entry.ExceptionType = firstLine[..colonIndex].Trim();
            entry.ExceptionMessage = firstLine[(colonIndex + 1)..].Trim();
        }
    }

    private static Models.LogLevel ParseLogLevel(string? level)
    {
        return level?.ToLowerInvariant() switch
        {
            "trace" or "verbose" or "vrb" => Models.LogLevel.Trace,
            "debug" or "dbg" => Models.LogLevel.Debug,
            "information" or "info" or "inf" => Models.LogLevel.Information,
            "warning" or "warn" or "wrn" => Models.LogLevel.Warning,
            "error" or "err" => Models.LogLevel.Error,
            "fatal" or "critical" or "crit" or "ftl" => Models.LogLevel.Critical,
            _ => Models.LogLevel.Information
        };
    }
}
