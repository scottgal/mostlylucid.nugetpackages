using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Mostlylucid.SentimentAnalysis.Models;
using Mostlylucid.SentimentAnalysis.Telemetry;
using OllamaSharp;

namespace Mostlylucid.SentimentAnalysis.Services;

/// <summary>
/// Ollama-based sentiment analysis service using LLM models.
/// </summary>
public class OllamaSentimentService : ISentimentAnalysisService, IDisposable
{
    private readonly ILogger<OllamaSentimentService> _logger;
    private readonly SentimentOptions _options;
    private readonly OllamaApiClient _client;
    private bool _disposed;
    private bool _isReady;

    public bool IsReady => _isReady;

    public OllamaSentimentService(
        ILogger<OllamaSentimentService> logger,
        IOptions<SentimentOptions> options)
    {
        _logger = logger;
        _options = options.Value;

        var uri = new Uri(_options.OllamaEndpoint);
        _client = new OllamaApiClient(uri);
        _client.SelectedModel = _options.OllamaModel;

        // Check connectivity asynchronously
        _ = CheckConnectivityAsync();
    }

    private async Task CheckConnectivityAsync()
    {
        try
        {
            var models = await _client.ListLocalModelsAsync();
            _isReady = models.Any(m => m.Name.Contains(_options.OllamaModel, StringComparison.OrdinalIgnoreCase));

            if (!_isReady)
            {
                _logger.LogWarning(
                    "Ollama model '{Model}' not found. Available models: {Models}",
                    _options.OllamaModel,
                    string.Join(", ", models.Select(m => m.Name)));
            }
            else if (_options.EnableDiagnosticLogging)
            {
                _logger.LogInformation(
                    "Ollama sentiment service ready with model: {Model}",
                    _options.OllamaModel);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to connect to Ollama at {Endpoint}", _options.OllamaEndpoint);
            _isReady = false;
        }
    }

    public async Task<SentimentResult> AnalyzeAsync(string text, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(text))
            throw new ArgumentException("Text cannot be empty", nameof(text));

        using var activity = SentimentTelemetry.StartAnalyzeActivity(text.Length);
        activity?.SetTag("sentiment.provider", "ollama");

        try
        {
            using var cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            cts.CancelAfter(_options.OllamaTimeoutMs);

            var chat = new Chat(_client, _options.OllamaSystemPrompt);

            var response = new StringBuilder();
            await foreach (var chunk in chat.SendAsync(text, cts.Token))
            {
                response.Append(chunk);
            }

            var result = ParseOllamaResponse(text, response.ToString());
            activity?.SetTag("sentiment.result", result.Sentiment.ToString());
            activity?.SetTag("sentiment.confidence", result.Confidence);
            return result;
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (OperationCanceledException)
        {
            _logger.LogWarning("Ollama request timed out after {Timeout}ms", _options.OllamaTimeoutMs);
            activity?.SetTag("sentiment.error", "timeout");
            return CreateFallbackResult(text, "Timeout");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Ollama sentiment analysis failed");
            activity?.SetTag("sentiment.error", ex.Message);
            return CreateFallbackResult(text, ex.Message);
        }
    }

    private SentimentResult ParseOllamaResponse(string originalText, string response)
    {
        try
        {
            // Try to extract JSON from response
            var jsonStart = response.IndexOf('{');
            var jsonEnd = response.LastIndexOf('}');

            if (jsonStart >= 0 && jsonEnd > jsonStart)
            {
                var json = response.Substring(jsonStart, jsonEnd - jsonStart + 1);
                var parsed = JsonSerializer.Deserialize<OllamaResponse>(json, new JsonSerializerOptions
                {
                    PropertyNameCaseInsensitive = true
                });

                if (parsed != null)
                {
                    var sentiment = MapSentimentLabel(parsed.Sentiment);
                    var confidence = Math.Clamp(parsed.Confidence, 0f, 1f);

                    return new SentimentResult
                    {
                        Text = originalText.Length > 200 ? originalText[..200] + "..." : originalText,
                        Sentiment = sentiment,
                        Confidence = confidence,
                        Scores = CreateScoresFromSentiment(sentiment, confidence)
                    };
                }
            }

            // Fallback: Try to detect sentiment from raw response text
            return ParseSentimentFromText(originalText, response);
        }
        catch (JsonException ex)
        {
            _logger.LogWarning(ex, "Failed to parse Ollama JSON response: {Response}", response);
            return ParseSentimentFromText(originalText, response);
        }
    }

    private SentimentResult ParseSentimentFromText(string originalText, string response)
    {
        var lower = response.ToLowerInvariant();

        SentimentLabel sentiment;
        float confidence = 0.6f;

        if (lower.Contains("positive") || lower.Contains("happy") || lower.Contains("good"))
        {
            sentiment = lower.Contains("very") ? SentimentLabel.VeryPositive : SentimentLabel.Positive;
        }
        else if (lower.Contains("negative") || lower.Contains("sad") || lower.Contains("bad"))
        {
            sentiment = lower.Contains("very") ? SentimentLabel.VeryNegative : SentimentLabel.Negative;
        }
        else
        {
            sentiment = SentimentLabel.Neutral;
        }

        return new SentimentResult
        {
            Text = originalText.Length > 200 ? originalText[..200] + "..." : originalText,
            Sentiment = sentiment,
            Confidence = confidence,
            Scores = CreateScoresFromSentiment(sentiment, confidence)
        };
    }

    private static SentimentLabel MapSentimentLabel(string? sentiment)
    {
        return sentiment?.ToLowerInvariant() switch
        {
            "positive" or "very positive" or "very_positive" => SentimentLabel.Positive,
            "negative" or "very negative" or "very_negative" => SentimentLabel.Negative,
            "neutral" or "mixed" => SentimentLabel.Neutral,
            _ => SentimentLabel.Neutral
        };
    }

    private static Dictionary<SentimentLabel, float> CreateScoresFromSentiment(SentimentLabel primary, float confidence)
    {
        var scores = new Dictionary<SentimentLabel, float>();
        var remaining = 1f - confidence;

        foreach (var label in Enum.GetValues<SentimentLabel>())
        {
            if (label == primary)
            {
                scores[label] = confidence;
            }
            else
            {
                // Distribute remaining probability
                scores[label] = remaining / 4f;
            }
        }

        return scores;
    }

    private static SentimentResult CreateFallbackResult(string text, string error)
    {
        return new SentimentResult
        {
            Text = text.Length > 200 ? text[..200] + "..." : text,
            Sentiment = SentimentLabel.Neutral,
            Confidence = 0f,
            Scores = new Dictionary<SentimentLabel, float>
            {
                { SentimentLabel.VeryNegative, 0.2f },
                { SentimentLabel.Negative, 0.2f },
                { SentimentLabel.Neutral, 0.2f },
                { SentimentLabel.Positive, 0.2f },
                { SentimentLabel.VeryPositive, 0.2f }
            }
        };
    }

    public async Task<IReadOnlyList<SentimentResult>> AnalyzeBatchAsync(
        IEnumerable<string> texts,
        CancellationToken cancellationToken = default)
    {
        var results = new List<SentimentResult>();
        foreach (var text in texts)
        {
            cancellationToken.ThrowIfCancellationRequested();
            results.Add(await AnalyzeAsync(text, cancellationToken));
        }
        return results;
    }

    public async Task<AggregateSentimentResult> AnalyzeFileAsync(
        string filePath,
        CancellationToken cancellationToken = default)
    {
        if (!File.Exists(filePath))
            throw new FileNotFoundException("File not found", filePath);

        using var activity = SentimentTelemetry.StartAnalyzeFileActivity(filePath);

        await using var stream = File.OpenRead(filePath);
        return await AnalyzeStreamAsync(stream, filePath, cancellationToken);
    }

    public async Task<AggregateSentimentResult> AnalyzeStreamAsync(
        Stream stream,
        string sourceName = "stream",
        CancellationToken cancellationToken = default)
    {
        using var reader = new StreamReader(stream, Encoding.UTF8);
        var content = await reader.ReadToEndAsync(cancellationToken);
        return await AnalyzeLongTextAsync(content, sourceName, cancellationToken);
    }

    public async Task<AggregateSentimentResult> AnalyzeLongTextAsync(
        string text,
        string sourceName = "text",
        CancellationToken cancellationToken = default)
    {
        using var activity = SentimentTelemetry.StartAnalyzeLongTextActivity(text.Length);

        var chunks = ChunkText(text);
        var results = await AnalyzeBatchAsync(chunks, cancellationToken);

        return AggregateResults(sourceName, results.ToList());
    }

    private List<string> ChunkText(string text)
    {
        var chunks = new List<string>();
        var paragraphs = text.Split(["\n\n", "\r\n\r\n"], StringSplitOptions.RemoveEmptyEntries);

        var currentChunk = new StringBuilder();
        var maxChunkChars = 2000; // Smaller chunks for LLM (context window consideration)

        foreach (var paragraph in paragraphs)
        {
            var trimmedParagraph = paragraph.Trim();
            if (string.IsNullOrWhiteSpace(trimmedParagraph))
                continue;

            if (currentChunk.Length + trimmedParagraph.Length > maxChunkChars && currentChunk.Length > 0)
            {
                chunks.Add(currentChunk.ToString().Trim());
                currentChunk.Clear();
            }

            currentChunk.Append(trimmedParagraph).Append("\n\n");
        }

        if (currentChunk.Length > 0)
        {
            chunks.Add(currentChunk.ToString().Trim());
        }

        if (chunks.Count == 0 && !string.IsNullOrWhiteSpace(text))
        {
            chunks.Add(text.Trim());
        }

        return chunks;
    }

    private static AggregateSentimentResult AggregateResults(string source, List<SentimentResult> results)
    {
        if (results.Count == 0)
        {
            return new AggregateSentimentResult
            {
                Source = source,
                OverallSentiment = SentimentLabel.Neutral,
                AverageConfidence = 0,
                ChunkResults = [],
                SentimentDistribution = new Dictionary<SentimentLabel, int>(),
                WeightedScore = 0
            };
        }

        var distribution = results
            .GroupBy(r => r.Sentiment)
            .ToDictionary(g => g.Key, g => g.Count());

        foreach (var label in Enum.GetValues<SentimentLabel>())
        {
            distribution.TryAdd(label, 0);
        }

        var totalWeight = results.Sum(r => r.Confidence);
        var weightedScore = totalWeight > 0
            ? results.Sum(r => r.NormalizedScore * r.Confidence) / totalWeight
            : 0;

        var overallSentiment = weightedScore switch
        {
            < -0.5f => SentimentLabel.VeryNegative,
            < -0.1f => SentimentLabel.Negative,
            > 0.5f => SentimentLabel.VeryPositive,
            > 0.1f => SentimentLabel.Positive,
            _ => SentimentLabel.Neutral
        };

        return new AggregateSentimentResult
        {
            Source = source,
            OverallSentiment = overallSentiment,
            AverageConfidence = results.Average(r => r.Confidence),
            ChunkResults = results,
            SentimentDistribution = distribution,
            WeightedScore = weightedScore
        };
    }

    public async Task<SentimentLabel> GetSentimentAsync(string text, CancellationToken cancellationToken = default)
    {
        var result = await AnalyzeAsync(text, cancellationToken);
        return result.Sentiment;
    }

    public async Task<float> GetSentimentScoreAsync(string text, CancellationToken cancellationToken = default)
    {
        var result = await AnalyzeAsync(text, cancellationToken);
        return result.NormalizedScore;
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        GC.SuppressFinalize(this);
    }

    private class OllamaResponse
    {
        public string? Sentiment { get; set; }
        public float Confidence { get; set; }
        public string? Reasoning { get; set; }
    }
}
