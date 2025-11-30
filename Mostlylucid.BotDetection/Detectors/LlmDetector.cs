using System.Text;
using System.Text.Json;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Mostlylucid.BotDetection.Models;
using OllamaSharp;

namespace Mostlylucid.BotDetection.Detectors;

/// <summary>
///     Advanced LLM-based bot detection with learning capabilities
///     Uses a small language model (like qwen2.5:1.5b) to analyze request patterns
/// </summary>
public class LlmDetector : IDetector, IDisposable
{
    private readonly SemaphoreSlim _fileLock = new(1, 1);
    private readonly string _learnedPatternsPath;
    private readonly ILogger<LlmDetector> _logger;
    private readonly BotDetectionOptions _options;
    private bool _disposed;

    public LlmDetector(
        ILogger<LlmDetector> logger,
        IOptions<BotDetectionOptions> options)
    {
        _logger = logger;
        _options = options.Value;
        _learnedPatternsPath = Path.Combine(AppContext.BaseDirectory, "learned_bot_patterns.json");
    }

    public string Name => "LLM Detector";

    public async Task<DetectorResult> DetectAsync(HttpContext context, CancellationToken cancellationToken = default)
    {
        var result = new DetectorResult();

        if (!_options.EnableLlmDetection || string.IsNullOrEmpty(_options.OllamaEndpoint))
            return result;

        try
        {
            var requestInfo = BuildRequestInfo(context);
            var analysis = await AnalyzeWithLlm(requestInfo, cancellationToken);

            if (analysis.IsBot)
            {
                result.Confidence = analysis.Confidence;
                result.Reasons.Add(new DetectionReason
                {
                    Category = "LLM Analysis",
                    Detail = analysis.Reasoning,
                    ConfidenceImpact = analysis.Confidence
                });

                result.BotType = analysis.BotType;

                if (analysis.Confidence > 0.8)
                    await LearnPattern(requestInfo, analysis, cancellationToken);
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "LLM detection failed, continuing without it");
        }

        return result;
    }

    public void Dispose()
    {
        Dispose(true);
        GC.SuppressFinalize(this);
    }

    protected virtual void Dispose(bool disposing)
    {
        if (_disposed) return;

        if (disposing)
        {
            _fileLock.Dispose();
        }

        _disposed = true;
    }

    private string BuildRequestInfo(HttpContext context)
    {
        var sb = new StringBuilder();
        sb.AppendLine($"User-Agent: {context.Request.Headers.UserAgent}");
        sb.AppendLine($"Path: {context.Request.Path}");
        sb.AppendLine($"Method: {context.Request.Method}");

        var headers = new[] { "Accept", "Accept-Language", "Accept-Encoding", "Referer", "Connection" };
        foreach (var header in headers)
            if (context.Request.Headers.ContainsKey(header))
                sb.AppendLine($"{header}: {context.Request.Headers[header]}");
            else
                sb.AppendLine($"{header}: (missing)");

        sb.AppendLine($"Has-Cookies: {context.Request.Cookies.Any()}");
        sb.AppendLine($"Client-IP: {context.Connection.RemoteIpAddress}");

        return sb.ToString();
    }

    private async Task<LlmAnalysis> AnalyzeWithLlm(string requestInfo, CancellationToken cancellationToken)
    {
        using var cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        cts.CancelAfter(_options.LlmTimeoutMs);

        try
        {
            var ollama = new OllamaApiClient(_options.OllamaEndpoint!)
            {
                SelectedModel = _options.OllamaModel ?? "qwen2.5:1.5b"
            };

            var prompt =
                $@"You are an expert at detecting bot traffic from HTTP requests. Analyze this request and determine if it's likely from a bot or legitimate user.

Request Information:
{requestInfo}

Respond ONLY with a valid JSON object in this exact format:
{{
  ""isBot"": true/false,
  ""confidence"": 0.0-1.0,
  ""reasoning"": ""brief explanation"",
  ""botType"": ""scraper/searchengine/monitor/malicious/unknown"",
  ""pattern"": ""key identifying pattern if bot""
}}

Important:
- Look for missing browser headers (Accept-Language, Referer)
- Simple User-Agents without version details are suspicious
- Generic Accept headers (*/*) without language preferences
- Known bot frameworks (Selenium, curl, wget, python-requests)
- Be conservative: when in doubt, prefer false negatives over false positives
";

            var chat = new Chat(ollama);
            var responseBuilder = new StringBuilder();

            await foreach (var token in chat.SendAsync(prompt, cts.Token))
                responseBuilder.Append(token);

            var response = responseBuilder.ToString();

            // Try to parse as JSON first
            try
            {
                var analysisResult = JsonSerializer.Deserialize<LlmAnalysisJson>(response);
                if (analysisResult != null)
                    return CreateAnalysis(analysisResult);
            }
            catch (JsonException)
            {
                // Fall back to extracting JSON from response
            }

            // Extract JSON from response (model might include extra text)
            var jsonStart = response.IndexOf('{');
            var jsonEnd = response.LastIndexOf('}');
            if (jsonStart >= 0 && jsonEnd > jsonStart)
            {
                var jsonText = response.Substring(jsonStart, jsonEnd - jsonStart + 1);
                var analysisResult = JsonSerializer.Deserialize<LlmAnalysisJson>(jsonText);

                if (analysisResult != null)
                    return CreateAnalysis(analysisResult);
            }

            _logger.LogWarning("Failed to parse LLM response: {Response}", response);
        }
        catch (OperationCanceledException)
        {
            _logger.LogWarning("LLM analysis timed out after {Timeout}ms", _options.LlmTimeoutMs);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error during LLM analysis");
        }

        return new LlmAnalysis { IsBot = false, Confidence = 0.0, Reasoning = "Analysis failed" };
    }

    private LlmAnalysis CreateAnalysis(LlmAnalysisJson result)
    {
        return new LlmAnalysis
        {
            IsBot = result.IsBot,
            Confidence = Math.Clamp(result.Confidence, 0.0, 1.0),
            Reasoning = result.Reasoning ?? "No reasoning provided",
            BotType = ParseBotType(result.BotType),
            Pattern = result.Pattern
        };
    }

    private async Task LearnPattern(string requestInfo, LlmAnalysis analysis, CancellationToken cancellationToken)
    {
        if (string.IsNullOrEmpty(analysis.Pattern))
            return;

        await _fileLock.WaitAsync(cancellationToken);
        try
        {
            var learnedPatterns = new List<LearnedPattern>();

            if (File.Exists(_learnedPatternsPath))
            {
                var json = await File.ReadAllTextAsync(_learnedPatternsPath, cancellationToken);
                learnedPatterns = JsonSerializer.Deserialize<List<LearnedPattern>>(json) ?? new List<LearnedPattern>();
            }

            var newPattern = new LearnedPattern
            {
                Pattern = analysis.Pattern,
                BotType = analysis.BotType.ToString(),
                Confidence = analysis.Confidence,
                FirstSeen = DateTime.UtcNow,
                LastSeen = DateTime.UtcNow,
                OccurrenceCount = 1,
                ExampleRequest = requestInfo
            };

            var existing = learnedPatterns.FirstOrDefault(p =>
                p.Pattern.Equals(analysis.Pattern, StringComparison.OrdinalIgnoreCase));

            if (existing != null)
            {
                existing.LastSeen = DateTime.UtcNow;
                existing.OccurrenceCount++;
                existing.Confidence = Math.Max(existing.Confidence, analysis.Confidence);
            }
            else
            {
                learnedPatterns.Add(newPattern);
                _logger.LogInformation("Learned new bot pattern: {Pattern}", analysis.Pattern);
            }

            var options = new JsonSerializerOptions { WriteIndented = true };
            var updatedJson = JsonSerializer.Serialize(learnedPatterns, options);
            await File.WriteAllTextAsync(_learnedPatternsPath, updatedJson, cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to save learned pattern");
        }
        finally
        {
            _fileLock.Release();
        }
    }

    private static BotType ParseBotType(string? botType)
    {
        if (string.IsNullOrEmpty(botType))
            return BotType.Unknown;

        return botType.ToLowerInvariant() switch
        {
            "scraper" => BotType.Scraper,
            "searchengine" => BotType.SearchEngine,
            "monitor" or "monitoring" => BotType.MonitoringBot,
            "malicious" => BotType.MaliciousBot,
            "social" or "socialmedia" => BotType.SocialMediaBot,
            "good" or "verified" => BotType.GoodBot,
            _ => BotType.Unknown
        };
    }

    private class LlmAnalysis
    {
        public bool IsBot { get; set; }
        public double Confidence { get; set; }
        public required string Reasoning { get; set; }
        public BotType BotType { get; set; }
        public string? Pattern { get; set; }
    }

    private class LlmAnalysisJson
    {
        public bool IsBot { get; set; }
        public double Confidence { get; set; }
        public string? Reasoning { get; set; }
        public string? BotType { get; set; }
        public string? Pattern { get; set; }
    }
}

/// <summary>
///     Learned bot pattern stored in JSON
/// </summary>
public class LearnedPattern
{
    public required string Pattern { get; set; }
    public required string BotType { get; set; }
    public double Confidence { get; set; }
    public DateTime FirstSeen { get; set; }
    public DateTime LastSeen { get; set; }
    public int OccurrenceCount { get; set; }
    public string? ExampleRequest { get; set; }
}
