using System.Diagnostics;
using System.Text;
using System.Text.Json;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Mostlylucid.BotDetection.Metrics;
using Mostlylucid.BotDetection.Middleware;
using Mostlylucid.BotDetection.Models;
using Mostlylucid.BotDetection.Orchestration;
using OllamaSharp;

namespace Mostlylucid.BotDetection.Detectors;

/// <summary>
///     Advanced LLM-based bot detection with learning capabilities.
///     Uses a small language model (default: gemma3:1b) to analyze request patterns.
///     Prompt is optimized for minimal context usage (~500 tokens).
/// </summary>
public class LlmDetector : IDetector, IDisposable
{
    private readonly SemaphoreSlim _fileLock = new(1, 1);
    private readonly string _learnedPatternsPath;
    private readonly ILogger<LlmDetector> _logger;
    private readonly BotDetectionOptions _options;
    private readonly BotDetectionMetrics? _metrics;
    private bool _disposed;

    public LlmDetector(
        ILogger<LlmDetector> logger,
        IOptions<BotDetectionOptions> options,
        BotDetectionMetrics? metrics = null)
    {
        _logger = logger;
        _options = options.Value;
        _metrics = metrics;
        _learnedPatternsPath = Path.Combine(AppContext.BaseDirectory, "learned_bot_patterns.json");
    }

    public string Name => "LLM Detector";

    /// <summary>Stage 3: AI/ML - can use all prior signals for learning</summary>
    public DetectorStage Stage => DetectorStage.Intelligence;

    public async Task<DetectorResult> DetectAsync(HttpContext context, CancellationToken cancellationToken = default)
    {
        var stopwatch = Stopwatch.StartNew();
        var result = new DetectorResult();

        // Skip if LLM detection is disabled (either globally or per-provider)
        if (!_options.EnableLlmDetection || !_options.AiDetection.Ollama.Enabled)
        {
            stopwatch.Stop();
            return result;
        }

        try
        {
            var requestInfo = BuildRequestInfo(context);
            var analysis = await AnalyzeWithLlm(requestInfo, cancellationToken);

            // Only report if we got a valid analysis (not the "Analysis failed" fallback)
            if (analysis.Reasoning != "Analysis failed")
            {
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
                else
                {
                    // Human classification - add reason with negative confidence impact
                    result.Confidence = 1.0 - analysis.Confidence; // Inverse for human score
                    result.Reasons.Add(new DetectionReason
                    {
                        Category = "LLM Analysis",
                        Detail = $"LLM classified as human: {analysis.Reasoning}",
                        ConfidenceImpact = -analysis.Confidence // Negative = evidence of human
                    });
                }
            }

            stopwatch.Stop();
            _metrics?.RecordDetection(result.Confidence, result.Confidence > _options.BotThreshold, stopwatch.Elapsed, Name);
        }
        catch (Exception ex)
        {
            stopwatch.Stop();
            _metrics?.RecordError(Name, ex.GetType().Name);
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

    /// <summary>
    ///     Builds a compact request info string for LLM analysis.
    ///     Uses TOML-like abbreviated format to minimize token count while remaining readable.
    /// </summary>
    private string BuildRequestInfo(HttpContext context)
    {
        var sb = new StringBuilder();

        // [request] section - basic request data
        sb.AppendLine("[request]");
        sb.AppendLine($"ua = \"{context.Request.Headers.UserAgent}\"");
        sb.AppendLine($"path = \"{context.Request.Path}\"");
        sb.AppendLine($"method = \"{context.Request.Method}\"");

        var accept = context.Request.Headers.Accept.ToString();
        var lang = context.Request.Headers.AcceptLanguage.ToString();
        var referer = context.Request.Headers.Referer.ToString();

        sb.AppendLine($"accept = \"{(!string.IsNullOrEmpty(accept) ? accept : "-")}\"");
        sb.AppendLine($"lang = \"{(!string.IsNullOrEmpty(lang) ? lang : "-")}\"");
        sb.AppendLine($"referer = \"{(!string.IsNullOrEmpty(referer) ? referer : "-")}\"");
        sb.AppendLine($"cookies = {context.Request.Cookies.Any().ToString().ToLower()}");
        sb.AppendLine($"headers = {context.Request.Headers.Count}");

        // [evidence] section - aggregated evidence from other detectors (if available)
        var evidence = context.Items[BotDetectionMiddleware.AggregatedEvidenceKey] as AggregatedEvidence;
        if (evidence != null)
        {
            // Check if localhost - we'll filter IP-related info to avoid confusing small LLMs
            var isLocalhost = evidence.Signals.TryGetValue(SignalKeys.IpIsLocal, out var isLocal) && isLocal is true;

            sb.AppendLine();
            sb.AppendLine("[evidence]");
            sb.AppendLine($"bot_probability = {evidence.BotProbability:F2}");
            sb.AppendLine($"confidence = {evidence.Confidence:F2}");
            sb.AppendLine($"risk_band = \"{evidence.RiskBand}\"");
            if (evidence.PrimaryBotType.HasValue)
                sb.AppendLine($"bot_type = \"{evidence.PrimaryBotType}\"");

            // [detectors] section - top contributions sorted by impact (compact)
            // Skip IP detector for localhost to avoid confusing small LLMs
            var topContributions = evidence.Contributions
                .Where(c => !isLocalhost || !c.DetectorName.Equals("Ip", StringComparison.OrdinalIgnoreCase))
                .OrderByDescending(c => Math.Abs(c.ConfidenceDelta) * c.Weight)
                .Take(8) // Top 8 to stay within context limits
                .ToList();

            if (topContributions.Count != 0)
            {
                sb.AppendLine();
                sb.AppendLine("[detectors]");
                foreach (var c in topContributions)
                {
                    // Compact: detector = delta | "reason"
                    var shortReason = c.Reason.Length > 50 ? c.Reason[..47] + "..." : c.Reason;
                    sb.AppendLine($"{c.DetectorName} = {c.ConfidenceDelta:+0.00;-0.00} | \"{shortReason}\"");
                }
            }

            // [categories] section - category breakdown (compact)
            // Skip IP category for localhost
            var topCategories = evidence.CategoryBreakdown
                .Where(kv => !isLocalhost || !kv.Key.Equals("IP", StringComparison.OrdinalIgnoreCase))
                .OrderByDescending(kv => kv.Value.Score)
                .Take(5)
                .ToList();

            if (topCategories.Count != 0)
            {
                sb.AppendLine();
                sb.AppendLine("[categories]");
                foreach (var (category, info) in topCategories)
                {
                    sb.AppendLine($"{category} = {info.Score:F2}");
                }
            }

            // [signals] section - key signals (compact, only most relevant)
            // Filter out localhost IP info to avoid confusing small LLMs
            var relevantSignals = evidence.Signals
                .Where(kv => kv.Value is bool or int or double or string { Length: < 50 })
                .Where(kv => !isLocalhost || !kv.Key.StartsWith("ip.", StringComparison.OrdinalIgnoreCase)) // Skip IP signals for localhost
                .Take(10)
                .ToList();

            if (relevantSignals.Count != 0)
            {
                sb.AppendLine();
                sb.AppendLine("[signals]");
                foreach (var (key, value) in relevantSignals)
                {
                    var formattedValue = value switch
                    {
                        bool b => b.ToString().ToLower(),
                        double d => d.ToString("F2"),
                        string s => $"\"{s}\"",
                        _ => value?.ToString() ?? "null"
                    };
                    sb.AppendLine($"{key} = {formattedValue}");
                }
            }
        }

        return sb.ToString();
    }

    private async Task<LlmAnalysis> AnalyzeWithLlm(string requestInfo, CancellationToken cancellationToken)
    {
        // Use new AiDetection settings (legacy properties are deprecated)
        var timeout = _options.AiDetection.TimeoutMs;
        var endpoint = _options.AiDetection.Ollama.Endpoint;
        var model = _options.AiDetection.Ollama.Model;

        using var cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        cts.CancelAfter(timeout);

        try
        {
            var ollama = new OllamaApiClient(endpoint!)
            {
                SelectedModel = model
            };

            // Use custom prompt if configured, otherwise use default compact prompt
            var promptTemplate = !string.IsNullOrEmpty(_options.AiDetection.Ollama.CustomPrompt)
                ? _options.AiDetection.Ollama.CustomPrompt
                : OllamaOptions.DefaultPrompt;

            // Replace placeholder with actual request info
            var prompt = promptTemplate.Replace("{REQUEST_INFO}", requestInfo);

            var chat = new Chat(ollama);
            var responseBuilder = new StringBuilder();

            await foreach (var token in chat.SendAsync(prompt, cts.Token))
                responseBuilder.Append(token);

            var response = responseBuilder.ToString();

            // Check if response is empty (Ollama may have failed silently)
            if (string.IsNullOrWhiteSpace(response))
            {
                _logger.LogWarning("LLM returned empty response. Ollama may have failed to generate output for model '{Model}'", model);
                return new LlmAnalysis { IsBot = false, Confidence = 0.0, Reasoning = "Analysis failed" };
            }

            // Check for Ollama error responses
            if (response.Contains("error", StringComparison.OrdinalIgnoreCase) &&
                (response.Contains("model", StringComparison.OrdinalIgnoreCase) ||
                 response.Contains("failed", StringComparison.OrdinalIgnoreCase)))
            {
                _logger.LogError("Ollama returned an error: {Response}", response.Length > 500 ? response[..500] + "..." : response);
                return new LlmAnalysis { IsBot = false, Confidence = 0.0, Reasoning = "Analysis failed" };
            }

            // Strip markdown code fences if present (```json ... ``` or ``` ... ```)
            var cleanedResponse = StripMarkdownCodeFences(response);

            // JSON options for case-insensitive parsing (LLM outputs camelCase, C# uses PascalCase)
            var jsonOptions = new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true
            };

            // Try to parse as JSON first
            try
            {
                var analysisResult = JsonSerializer.Deserialize<LlmAnalysisJson>(cleanedResponse, jsonOptions);
                if (analysisResult != null)
                {
                    _logger.LogDebug("LLM response parsed: isBot={IsBot}, confidence={Confidence}, reasoning={Reasoning}",
                        analysisResult.IsBot, analysisResult.Confidence, analysisResult.Reasoning);
                    return CreateAnalysis(analysisResult);
                }
            }
            catch (JsonException ex)
            {
                _logger.LogDebug(ex, "Direct JSON parse failed, trying to extract JSON from response");
            }

            // Extract JSON from response (model might include extra text)
            var jsonStart = cleanedResponse.IndexOf('{');
            var jsonEnd = cleanedResponse.LastIndexOf('}');
            if (jsonStart >= 0 && jsonEnd > jsonStart)
            {
                var jsonText = cleanedResponse.Substring(jsonStart, jsonEnd - jsonStart + 1);
                try
                {
                    var analysisResult = JsonSerializer.Deserialize<LlmAnalysisJson>(jsonText, jsonOptions);
                    if (analysisResult != null)
                    {
                        _logger.LogDebug("LLM response extracted and parsed: isBot={IsBot}, confidence={Confidence}, reasoning={Reasoning}",
                            analysisResult.IsBot, analysisResult.Confidence, analysisResult.Reasoning);
                        return CreateAnalysis(analysisResult);
                    }
                }
                catch (JsonException ex)
                {
                    _logger.LogDebug(ex, "Failed to parse extracted JSON: {Json}", jsonText);
                }
            }

            _logger.LogWarning("LLM returned invalid JSON (model '{Model}' may need a better prompt). Response: {Response}", model, response.Length > 500 ? response[..500] + "..." : response);
        }
        catch (OperationCanceledException)
        {
            _logger.LogWarning("LLM analysis timed out after {Timeout}ms. Consider increasing AiDetection.TimeoutMs (current: {Timeout}ms)", timeout, timeout);
        }
        catch (HttpRequestException httpEx) when (httpEx.StatusCode == System.Net.HttpStatusCode.NotFound)
        {
            _logger.LogError("Ollama model '{Model}' not found at {Endpoint}. Run 'ollama pull {Model}' to download it", model, endpoint, model);
        }
        catch (HttpRequestException httpEx) when (httpEx.StatusCode == System.Net.HttpStatusCode.InternalServerError)
        {
            _logger.LogError("Ollama server error (500) at {Endpoint}. Check Ollama logs - the model '{Model}' may have failed to load or run out of memory", endpoint, model);
        }
        catch (HttpRequestException httpEx)
        {
            _logger.LogError(httpEx, "Ollama HTTP error ({StatusCode}) at {Endpoint}. Is Ollama running?", (int?)httpEx.StatusCode ?? 0, endpoint);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "LLM analysis failed: {Message}", ex.Message);
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

    /// <summary>
    ///     Strips markdown code fences from LLM response.
    ///     Handles: ```json ... ```, ``` ... ```, and variations.
    /// </summary>
    private static string StripMarkdownCodeFences(string response)
    {
        if (string.IsNullOrWhiteSpace(response))
            return response;

        var trimmed = response.Trim();

        // Handle ```json or ```JSON or ``` at the start
        if (trimmed.StartsWith("```"))
        {
            // Find the end of the first line (after ```json or ```)
            var firstNewline = trimmed.IndexOf('\n');
            if (firstNewline > 0)
            {
                trimmed = trimmed[(firstNewline + 1)..];
            }
            else
            {
                // Just ``` on one line, skip it
                trimmed = trimmed[3..];
            }
        }

        // Handle ``` at the end
        if (trimmed.EndsWith("```"))
        {
            trimmed = trimmed[..^3];
        }

        return trimmed.Trim();
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
