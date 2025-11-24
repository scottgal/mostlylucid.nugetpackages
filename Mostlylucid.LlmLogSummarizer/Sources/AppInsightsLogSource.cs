using System.Net.Http.Json;
using System.Runtime.CompilerServices;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using Mostlylucid.LlmLogSummarizer.Models;
using LogLevel = Mostlylucid.LlmLogSummarizer.Models.LogLevel;

namespace Mostlylucid.LlmLogSummarizer.Sources;

/// <summary>
///     Log source for Azure Application Insights.
/// </summary>
public class AppInsightsLogSource : ILogSource
{
    private readonly AppInsightsSourceConfig _config;
    private readonly HttpClient _httpClient;
    private readonly ILogger<AppInsightsLogSource> _logger;

    public AppInsightsLogSource(
        AppInsightsSourceConfig config,
        HttpClient httpClient,
        ILogger<AppInsightsLogSource> logger)
    {
        _config = config;
        _httpClient = httpClient;
        _logger = logger;
    }

    public string Name => _config.Name;

    public bool IsAvailable =>
        !string.IsNullOrEmpty(_config.ApplicationId) &&
        !string.IsNullOrEmpty(_config.ApiKey);

    public async IAsyncEnumerable<LogEntry> GetEntriesAsync(
        DateTimeOffset from,
        DateTimeOffset to,
        int maxEntries,
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        if (!IsAvailable)
        {
            _logger.LogWarning("Application Insights source is not properly configured");
            yield break;
        }

        // Query traces (logs)
        var traces = await QueryTracesAsync(from, to, maxEntries, cancellationToken);
        foreach (var entry in traces) yield return entry;

        // Query exceptions
        var exceptions = await QueryExceptionsAsync(from, to, maxEntries, cancellationToken);
        foreach (var entry in exceptions) yield return entry;
    }

    public async Task<bool> TestConnectionAsync(CancellationToken cancellationToken = default)
    {
        if (!IsAvailable)
            return false;

        try
        {
            var request = new HttpRequestMessage(HttpMethod.Get,
                $"https://api.applicationinsights.io/v1/apps/{_config.ApplicationId}/query?query=traces | take 1");
            request.Headers.Add("x-api-key", _config.ApiKey);

            var response = await _httpClient.SendAsync(request, cancellationToken);
            return response.IsSuccessStatusCode;
        }
        catch (Exception ex)
        {
            _logger.LogDebug(ex, "Failed to test Application Insights connection");
            return false;
        }
    }

    private async Task<List<LogEntry>> QueryTracesAsync(
        DateTimeOffset from,
        DateTimeOffset to,
        int maxEntries,
        CancellationToken cancellationToken)
    {
        var results = new List<LogEntry>();

        try
        {
            var query = $@"
                traces
                | where timestamp >= datetime('{from:O}') and timestamp <= datetime('{to:O}')
                | where severityLevel >= 2
                | project timestamp, severityLevel, message, customDimensions, operation_Id
                | order by timestamp desc
                | take {maxEntries}";

            var response = await ExecuteQueryAsync(query, cancellationToken);
            if (response == null)
                return results;

            foreach (var row in response.Tables.FirstOrDefault()?.Rows ?? Enumerable.Empty<JsonElement[]>())
            {
                var entry = new LogEntry
                {
                    SourceName = Name,
                    Timestamp = DateTimeOffset.Parse(row[0].GetString()!),
                    Level = MapSeverityLevel(row[1].GetInt32()),
                    Message = row[2].GetString() ?? string.Empty,
                    RequestId = row.Length > 4 ? row[4].GetString() : null
                };

                // Parse custom dimensions
                if (row.Length > 3 && row[3].ValueKind == JsonValueKind.Object)
                    foreach (var prop in row[3].EnumerateObject())
                        entry.Properties[prop.Name] = prop.Value.ToString();

                results.Add(entry);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to query traces from Application Insights");
        }

        return results;
    }

    private async Task<List<LogEntry>> QueryExceptionsAsync(
        DateTimeOffset from,
        DateTimeOffset to,
        int maxEntries,
        CancellationToken cancellationToken)
    {
        var results = new List<LogEntry>();

        try
        {
            var query = $@"
                exceptions
                | where timestamp >= datetime('{from:O}') and timestamp <= datetime('{to:O}')
                | project timestamp, type, outerMessage, details, operation_Id
                | order by timestamp desc
                | take {maxEntries}";

            var response = await ExecuteQueryAsync(query, cancellationToken);
            if (response == null)
                return results;

            foreach (var row in response.Tables.FirstOrDefault()?.Rows ?? Enumerable.Empty<JsonElement[]>())
            {
                var entry = new LogEntry
                {
                    SourceName = Name,
                    Timestamp = DateTimeOffset.Parse(row[0].GetString()!),
                    Level = LogLevel.Error,
                    ExceptionType = row[1].GetString(),
                    ExceptionMessage = row[2].GetString(),
                    Message = row[2].GetString() ?? string.Empty,
                    RequestId = row.Length > 4 ? row[4].GetString() : null
                };

                // Parse exception details for stack trace
                if (row.Length > 3 && row[3].ValueKind == JsonValueKind.Array)
                {
                    var details = row[3].EnumerateArray().FirstOrDefault();
                    if (details.TryGetProperty("parsedStack", out var stack))
                        entry.StackTrace = FormatStackTrace(stack);
                }

                results.Add(entry);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to query exceptions from Application Insights");
        }

        return results;
    }

    private async Task<AppInsightsQueryResponse?> ExecuteQueryAsync(string query, CancellationToken cancellationToken)
    {
        var request = new HttpRequestMessage(HttpMethod.Post,
            $"https://api.applicationinsights.io/v1/apps/{_config.ApplicationId}/query");
        request.Headers.Add("x-api-key", _config.ApiKey);
        request.Content = JsonContent.Create(new { query });

        var response = await _httpClient.SendAsync(request, cancellationToken);
        if (!response.IsSuccessStatusCode)
        {
            _logger.LogWarning("Application Insights query failed with status {Status}", response.StatusCode);
            return null;
        }

        return await response.Content.ReadFromJsonAsync<AppInsightsQueryResponse>(cancellationToken);
    }

    private static LogLevel MapSeverityLevel(int severity)
    {
        return severity switch
        {
            0 => LogLevel.Trace,
            1 => LogLevel.Debug,
            2 => LogLevel.Information,
            3 => LogLevel.Warning,
            4 => LogLevel.Error,
            _ => LogLevel.Information
        };
    }

    private static string FormatStackTrace(JsonElement stack)
    {
        var lines = new List<string>();
        foreach (var frame in stack.EnumerateArray())
            if (frame.TryGetProperty("method", out var method) &&
                frame.TryGetProperty("assembly", out var assembly))
            {
                var line = frame.TryGetProperty("line", out var lineNum) ? lineNum.GetInt32() : 0;
                lines.Add($"   at {method.GetString()} in {assembly.GetString()}:line {line}");
            }

        return string.Join(Environment.NewLine, lines);
    }

    private class AppInsightsQueryResponse
    {
        public List<AppInsightsTable> Tables { get; set; } = new();
    }

    private class AppInsightsTable
    {
        public string Name { get; set; } = string.Empty;
        public List<AppInsightsColumn> Columns { get; set; } = new();
        public JsonElement[][] Rows { get; } = Array.Empty<JsonElement[]>();
    }

    private class AppInsightsColumn
    {
        public string Name { get; set; } = string.Empty;
        public string Type { get; set; } = string.Empty;
    }
}