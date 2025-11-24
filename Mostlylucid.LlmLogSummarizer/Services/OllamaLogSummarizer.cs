using System.Net.Http.Json;
using System.Text;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Mostlylucid.LlmLogSummarizer.Models;

namespace Mostlylucid.LlmLogSummarizer.Services;

/// <summary>
///     LLM-powered log summarization using Ollama.
/// </summary>
public class OllamaLogSummarizer : ILogSummarizer
{
    private readonly HttpClient _httpClient;
    private readonly ILogger<OllamaLogSummarizer> _logger;
    private readonly OllamaOptions _options;

    public OllamaLogSummarizer(
        HttpClient httpClient,
        IOptions<LogSummarizerOptions> options,
        ILogger<OllamaLogSummarizer> logger)
    {
        _httpClient = httpClient;
        _options = options.Value.Ollama;
        _logger = logger;

        _httpClient.BaseAddress = new Uri(_options.Endpoint);
        _httpClient.Timeout = TimeSpan.FromSeconds(_options.TimeoutSeconds);
    }

    public async Task<bool> IsAvailableAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            var response = await _httpClient.GetAsync("/api/tags", cancellationToken);
            return response.IsSuccessStatusCode;
        }
        catch (Exception ex)
        {
            _logger.LogDebug(ex, "Ollama is not available at {Endpoint}", _options.Endpoint);
            return false;
        }
    }

    public async Task<string> SummarizeClusterAsync(
        ExceptionCluster cluster,
        CancellationToken cancellationToken = default)
    {
        var prompt = BuildClusterSummaryPrompt(cluster);
        return await GenerateAsync(prompt, cancellationToken);
    }

    public async Task<string> GenerateSuggestedActionAsync(
        ExceptionCluster cluster,
        CancellationToken cancellationToken = default)
    {
        var prompt = BuildSuggestedActionPrompt(cluster);
        return await GenerateAsync(prompt, cancellationToken);
    }

    public async Task<string> GenerateExecutiveSummaryAsync(
        SummaryReport report,
        CancellationToken cancellationToken = default)
    {
        var prompt = BuildExecutiveSummaryPrompt(report);
        return await GenerateAsync(prompt, cancellationToken);
    }

    public async Task<List<string>> GenerateKeyInsightsAsync(
        SummaryReport report,
        CancellationToken cancellationToken = default)
    {
        var prompt = BuildKeyInsightsPrompt(report);
        var response = await GenerateAsync(prompt, cancellationToken);

        // Parse bullet points from response
        return response
            .Split('\n')
            .Where(line => line.Trim().StartsWith("-") || line.Trim().StartsWith("*") || line.Trim().StartsWith("•"))
            .Select(line => line.TrimStart('-', '*', '•', ' '))
            .Where(line => !string.IsNullOrWhiteSpace(line))
            .ToList();
    }

    public async Task<HealthStatus> AssessHealthStatusAsync(
        SummaryReport report,
        CancellationToken cancellationToken = default)
    {
        var prompt = BuildHealthAssessmentPrompt(report);
        var response = await GenerateAsync(prompt, cancellationToken);

        var lowerResponse = response.ToLowerInvariant();

        if (lowerResponse.Contains("critical"))
            return HealthStatus.Critical;
        if (lowerResponse.Contains("unhealthy"))
            return HealthStatus.Unhealthy;
        if (lowerResponse.Contains("degraded"))
            return HealthStatus.Degraded;
        if (lowerResponse.Contains("healthy"))
            return HealthStatus.Healthy;

        return HealthStatus.Unknown;
    }

    private async Task<string> GenerateAsync(string prompt, CancellationToken cancellationToken)
    {
        try
        {
            var request = new OllamaGenerateRequest
            {
                Model = _options.Model,
                Prompt = prompt,
                Stream = false,
                Options = new OllamaRequestOptions
                {
                    Temperature = _options.Temperature,
                    NumPredict = _options.MaxTokens
                }
            };

            _logger.LogDebug("Sending request to Ollama model {Model}", _options.Model);

            var response = await _httpClient.PostAsJsonAsync(
                "/api/generate",
                request,
                cancellationToken);

            response.EnsureSuccessStatusCode();

            var result = await response.Content.ReadFromJsonAsync<OllamaGenerateResponse>(
                cancellationToken);

            var text = result?.Response?.Trim() ?? string.Empty;

            _logger.LogDebug("Received response from Ollama: {Length} characters", text.Length);

            return text;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to generate with Ollama");
            return string.Empty;
        }
    }

    private static string BuildClusterSummaryPrompt(ExceptionCluster cluster)
    {
        var sb = new StringBuilder();
        sb.AppendLine(
            "You are a software engineer analyzing application logs. Summarize this error pattern concisely (2-3 sentences).");
        sb.AppendLine();
        sb.AppendLine($"Exception Type: {cluster.ExceptionType ?? "N/A"}");
        sb.AppendLine($"Occurrences: {cluster.Count}");
        sb.AppendLine($"Time Range: {cluster.FirstOccurrence:g} to {cluster.LastOccurrence:g}");
        sb.AppendLine($"Error Message: {cluster.RepresentativeMessage}");

        if (!string.IsNullOrEmpty(cluster.RepresentativeStackTrace))
        {
            var stackPreview = string.Join("\n",
                cluster.RepresentativeStackTrace.Split('\n').Take(5));
            sb.AppendLine($"Stack Trace (first 5 lines):\n{stackPreview}");
        }

        sb.AppendLine();
        sb.AppendLine("Provide a brief technical summary of what this error means and its potential impact.");

        return sb.ToString();
    }

    private static string BuildSuggestedActionPrompt(ExceptionCluster cluster)
    {
        var sb = new StringBuilder();
        sb.AppendLine(
            "You are a software engineer. Based on this error pattern, suggest 2-3 specific investigation or fix steps.");
        sb.AppendLine();
        sb.AppendLine($"Exception Type: {cluster.ExceptionType ?? "N/A"}");
        sb.AppendLine($"Error Message: {cluster.RepresentativeMessage}");

        if (!string.IsNullOrEmpty(cluster.RepresentativeStackTrace))
        {
            var stackPreview = string.Join("\n",
                cluster.RepresentativeStackTrace.Split('\n').Take(5));
            sb.AppendLine($"Stack Trace:\n{stackPreview}");
        }

        sb.AppendLine();
        sb.AppendLine("Provide actionable steps as bullet points.");

        return sb.ToString();
    }

    private static string BuildExecutiveSummaryPrompt(SummaryReport report)
    {
        var sb = new StringBuilder();
        sb.AppendLine(
            "You are creating an executive summary of application health for a development team. Be concise and actionable (3-4 sentences).");
        sb.AppendLine();
        sb.AppendLine($"Time Period: {report.PeriodStart:g} to {report.PeriodEnd:g}");
        sb.AppendLine($"Total Logs Analyzed: {report.TotalLogsAnalyzed:N0}");
        sb.AppendLine(
            $"Errors: {report.ErrorCount:N0}, Warnings: {report.WarningCount:N0}, Critical: {report.CriticalCount:N0}");
        sb.AppendLine($"Unique Error Patterns: {report.AllClusters.Count}");
        sb.AppendLine($"New Error Types: {report.NewErrorTypes.Count}");

        if (report.TopErrorPatterns.Any())
        {
            sb.AppendLine("\nTop Error Patterns:");
            foreach (var pattern in report.TopErrorPatterns.Take(5))
                sb.AppendLine($"- {pattern.Title} ({pattern.Count} occurrences)");
        }

        sb.AppendLine();
        sb.AppendLine(
            "Provide a brief executive summary highlighting the most important findings and recommended priorities.");

        return sb.ToString();
    }

    private static string BuildKeyInsightsPrompt(SummaryReport report)
    {
        var sb = new StringBuilder();
        sb.AppendLine(
            "Analyze these application logs and provide 3-5 key insights as bullet points. Focus on patterns, trends, and actionable observations.");
        sb.AppendLine();
        sb.AppendLine($"Time Period: {report.PeriodStart:g} to {report.PeriodEnd:g}");
        sb.AppendLine($"Errors: {report.ErrorCount:N0}, Warnings: {report.WarningCount:N0}");

        if (report.NewErrorTypes.Any())
        {
            sb.AppendLine($"\nNew Error Types ({report.NewErrorTypes.Count}):");
            foreach (var error in report.NewErrorTypes.Take(3)) sb.AppendLine($"- {error.Title}");
        }

        if (report.TrendingUp.Any())
        {
            sb.AppendLine($"\nIncreasing Errors ({report.TrendingUp.Count}):");
            foreach (var error in report.TrendingUp.Take(3))
                sb.AppendLine($"- {error.Title} (+{error.TrendPercent:F0}%)");
        }

        sb.AppendLine("\nProvide key insights as bullet points starting with '-'.");

        return sb.ToString();
    }

    private static string BuildHealthAssessmentPrompt(SummaryReport report)
    {
        var sb = new StringBuilder();
        sb.AppendLine(
            "Assess the overall health of this application based on log analysis. Respond with exactly one word: HEALTHY, DEGRADED, UNHEALTHY, or CRITICAL.");
        sb.AppendLine();
        sb.AppendLine($"Errors: {report.ErrorCount:N0}");
        sb.AppendLine($"Critical: {report.CriticalCount:N0}");
        sb.AppendLine($"Total Logs: {report.TotalLogsAnalyzed:N0}");

        var errorRate = report.TotalLogsAnalyzed > 0
            ? (double)report.ErrorCount / report.TotalLogsAnalyzed * 100
            : 0;
        sb.AppendLine($"Error Rate: {errorRate:F2}%");

        sb.AppendLine($"New Error Types: {report.NewErrorTypes.Count}");
        sb.AppendLine($"Trending Up Errors: {report.TrendingUp.Count}");

        return sb.ToString();
    }

    private class OllamaGenerateRequest
    {
        public string Model { get; set; } = string.Empty;
        public string Prompt { get; set; } = string.Empty;
        public bool Stream { get; set; }
        public OllamaRequestOptions? Options { get; set; }
    }

    private class OllamaRequestOptions
    {
        public float Temperature { get; set; }
        public int NumPredict { get; set; }
    }

    private class OllamaGenerateResponse
    {
        public string Response { get; set; } = string.Empty;
        public bool Done { get; set; }
    }
}