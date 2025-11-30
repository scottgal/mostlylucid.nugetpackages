using System.Text;
using FastBertTokenizer;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Microsoft.ML.OnnxRuntime;
using Microsoft.ML.OnnxRuntime.Tensors;
using Mostlylucid.SentimentAnalysis.Models;
using Mostlylucid.SentimentAnalysis.Telemetry;

namespace Mostlylucid.SentimentAnalysis.Services;

/// <summary>
/// ONNX-based sentiment analysis service using a small, efficient model.
/// </summary>
public class SentimentAnalysisService : ISentimentAnalysisService, IDisposable
{
    private readonly ILogger<SentimentAnalysisService> _logger;
    private readonly SentimentOptions _options;
    private readonly HttpClient _httpClient;
    private readonly SemaphoreSlim _initLock = new(1, 1);

    private InferenceSession? _session;
    private BertTokenizer? _tokenizer;
    private bool _isInitialized;
    private bool _disposed;

    public bool IsReady => _isInitialized && _session != null;

    public SentimentAnalysisService(
        ILogger<SentimentAnalysisService> logger,
        IOptions<SentimentOptions> options,
        IHttpClientFactory? httpClientFactory = null)
    {
        _logger = logger;
        _options = options.Value;
        _httpClient = httpClientFactory?.CreateClient("SentimentModelDownloader")
                      ?? new HttpClient();

        // Initialize synchronously in constructor
        InitializeAsync().GetAwaiter().GetResult();
    }

    private async Task InitializeAsync()
    {
        await _initLock.WaitAsync();
        try
        {
            if (_isInitialized) return;

            var modelPath = Path.Combine(_options.ModelPath, _options.ModelFileName);

            // Ensure model directory exists
            Directory.CreateDirectory(_options.ModelPath);

            // Download model if needed
            if (!File.Exists(modelPath) && _options.AutoDownloadModel)
            {
                await DownloadModelAsync(modelPath);
            }

            if (!File.Exists(modelPath))
            {
                _logger.LogError("Sentiment model not found at {ModelPath}", modelPath);
                return;
            }

            // Initialize tokenizer
            _tokenizer = new BertTokenizer();
            await _tokenizer.LoadFromHuggingFaceAsync("distilbert-base-multilingual-cased");

            // Initialize ONNX session
            var sessionOptions = new SessionOptions();
            if (_options.InferenceThreads > 0)
            {
                sessionOptions.IntraOpNumThreads = _options.InferenceThreads;
            }

            _session = new InferenceSession(modelPath, sessionOptions);
            _isInitialized = true;

            if (_options.EnableDiagnosticLogging)
            {
                _logger.LogInformation("Sentiment analysis service initialized with model: {ModelPath}", modelPath);
                LogModelInfo();
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to initialize sentiment analysis service");
            _isInitialized = false;
        }
        finally
        {
            _initLock.Release();
        }
    }

    private async Task DownloadModelAsync(string modelPath)
    {
        using var activity = SentimentTelemetry.StartModelDownloadActivity();

        _logger.LogInformation("Downloading sentiment model from {Url}", _options.ModelUrl);

        try
        {
            using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(_options.DownloadTimeoutSeconds));
            using var response = await _httpClient.GetAsync(_options.ModelUrl, HttpCompletionOption.ResponseHeadersRead, cts.Token);
            response.EnsureSuccessStatusCode();

            var totalBytes = response.Content.Headers.ContentLength ?? 0;
            await using var contentStream = await response.Content.ReadAsStreamAsync(cts.Token);
            await using var fileStream = new FileStream(modelPath, FileMode.Create, FileAccess.Write, FileShare.None);

            var buffer = new byte[81920];
            long downloadedBytes = 0;
            int bytesRead;

            while ((bytesRead = await contentStream.ReadAsync(buffer, cts.Token)) > 0)
            {
                await fileStream.WriteAsync(buffer.AsMemory(0, bytesRead), cts.Token);
                downloadedBytes += bytesRead;

                if (_options.EnableDiagnosticLogging && totalBytes > 0)
                {
                    var progress = (double)downloadedBytes / totalBytes * 100;
                    if (downloadedBytes % (1024 * 1024) < buffer.Length) // Log every ~1MB
                    {
                        _logger.LogDebug("Download progress: {Progress:F1}%", progress);
                    }
                }
            }

            activity?.SetTag("sentiment.download_size_bytes", downloadedBytes);
            _logger.LogInformation("Model downloaded successfully: {Bytes} bytes", downloadedBytes);
        }
        catch (Exception ex)
        {
            activity?.SetTag("sentiment.download_error", ex.Message);
            _logger.LogError(ex, "Failed to download sentiment model");
            throw;
        }
    }

    private void LogModelInfo()
    {
        if (_session == null) return;

        _logger.LogDebug("Model inputs:");
        foreach (var input in _session.InputMetadata)
        {
            _logger.LogDebug("  {Name}: {Type}", input.Key, input.Value.ElementDataType);
        }

        _logger.LogDebug("Model outputs:");
        foreach (var output in _session.OutputMetadata)
        {
            _logger.LogDebug("  {Name}: {Type}", output.Key, output.Value.ElementDataType);
        }
    }

    public async Task<SentimentResult> AnalyzeAsync(string text, CancellationToken cancellationToken = default)
    {
        if (!IsReady)
            throw new InvalidOperationException("Sentiment analysis service is not initialized");

        if (string.IsNullOrWhiteSpace(text))
            throw new ArgumentException("Text cannot be empty", nameof(text));

        using var activity = SentimentTelemetry.StartAnalyzeActivity(text.Length);

        try
        {
            var result = await Task.Run(() => RunInference(text), cancellationToken);
            activity?.SetTag("sentiment.result", result.Sentiment.ToString());
            activity?.SetTag("sentiment.confidence", result.Confidence);
            return result;
        }
        catch (Exception ex)
        {
            activity?.SetTag("sentiment.error", ex.Message);
            throw;
        }
    }

    private SentimentResult RunInference(string text)
    {
        // Tokenize
        var (inputIds, attentionMask, tokenTypeIds) = Tokenize(text);

        // Create tensors
        var inputIdsTensor = new DenseTensor<long>(inputIds, [1, inputIds.Length]);
        var attentionMaskTensor = new DenseTensor<long>(attentionMask, [1, attentionMask.Length]);

        var inputs = new List<NamedOnnxValue>
        {
            NamedOnnxValue.CreateFromTensor("input_ids", inputIdsTensor),
            NamedOnnxValue.CreateFromTensor("attention_mask", attentionMaskTensor)
        };

        // Add token_type_ids if the model expects it
        if (_session!.InputMetadata.ContainsKey("token_type_ids"))
        {
            var tokenTypeIdsTensor = new DenseTensor<long>(tokenTypeIds, [1, tokenTypeIds.Length]);
            inputs.Add(NamedOnnxValue.CreateFromTensor("token_type_ids", tokenTypeIdsTensor));
        }

        // Run inference
        using var results = _session.Run(inputs);

        // Get logits output
        var logits = results.First().AsEnumerable<float>().ToArray();

        // Apply softmax
        var probabilities = Softmax(logits);

        // Map to sentiment
        return MapToSentimentResult(text, probabilities);
    }

    private (long[] inputIds, long[] attentionMask, long[] tokenTypeIds) Tokenize(string text)
    {
        var maxLength = Math.Min(_options.MaxChunkLength, 512);

        // Use FastBertTokenizer
        var encoded = _tokenizer!.Encode(text);

        // Truncate or pad to maxLength
        var inputIds = new long[maxLength];
        var attentionMask = new long[maxLength];
        var tokenTypeIds = new long[maxLength];

        var length = Math.Min(encoded.InputIds.Length, maxLength);

        for (int i = 0; i < length; i++)
        {
            inputIds[i] = encoded.InputIds.Span[i];
            attentionMask[i] = encoded.AttentionMask.Span[i];
            tokenTypeIds[i] = encoded.TokenTypeIds.Span[i];
        }

        // Pad the rest (already zeros from array initialization)

        return (inputIds, attentionMask, tokenTypeIds);
    }

    private static float[] Softmax(float[] logits)
    {
        var maxLogit = logits.Max();
        var expValues = logits.Select(x => MathF.Exp(x - maxLogit)).ToArray();
        var sumExp = expValues.Sum();
        return expValues.Select(x => x / sumExp).ToArray();
    }

    private SentimentResult MapToSentimentResult(string text, float[] probabilities)
    {
        // Map model output to our sentiment scale
        // Model outputs: negative (0), neutral (1), positive (2)
        var scores = new Dictionary<SentimentLabel, float>();

        if (probabilities.Length >= 3)
        {
            // 3-class model (negative, neutral, positive)
            var negativeProb = probabilities[0];
            var neutralProb = probabilities[1];
            var positiveProb = probabilities[2];

            // Distribute into 5 classes
            scores[SentimentLabel.VeryNegative] = negativeProb * 0.4f;
            scores[SentimentLabel.Negative] = negativeProb * 0.6f;
            scores[SentimentLabel.Neutral] = neutralProb;
            scores[SentimentLabel.Positive] = positiveProb * 0.6f;
            scores[SentimentLabel.VeryPositive] = positiveProb * 0.4f;
        }
        else if (probabilities.Length == 2)
        {
            // Binary model (negative, positive)
            scores[SentimentLabel.VeryNegative] = probabilities[0] * 0.3f;
            scores[SentimentLabel.Negative] = probabilities[0] * 0.7f;
            scores[SentimentLabel.Neutral] = 0.0f;
            scores[SentimentLabel.Positive] = probabilities[1] * 0.7f;
            scores[SentimentLabel.VeryPositive] = probabilities[1] * 0.3f;
        }
        else
        {
            throw new InvalidOperationException($"Unexpected number of output classes: {probabilities.Length}");
        }

        // Find best sentiment
        var maxScore = scores.MaxBy(kvp => kvp.Value);

        return new SentimentResult
        {
            Text = text.Length > 200 ? text[..200] + "..." : text,
            Sentiment = maxScore.Key,
            Confidence = maxScore.Value,
            Scores = scores
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

        // Split by paragraphs first
        var paragraphs = text.Split(["\n\n", "\r\n\r\n"], StringSplitOptions.RemoveEmptyEntries);

        var currentChunk = new StringBuilder();
        var currentLength = 0;
        var maxChunkChars = _options.MaxChunkLength * 4; // Approximate chars per token

        foreach (var paragraph in paragraphs)
        {
            var trimmedParagraph = paragraph.Trim();
            if (string.IsNullOrWhiteSpace(trimmedParagraph))
                continue;

            if (currentLength + trimmedParagraph.Length > maxChunkChars && currentChunk.Length > 0)
            {
                chunks.Add(currentChunk.ToString().Trim());
                currentChunk.Clear();
                currentLength = 0;
            }

            if (trimmedParagraph.Length > maxChunkChars)
            {
                // Split large paragraphs by sentences
                var sentences = SplitIntoSentences(trimmedParagraph);
                foreach (var sentence in sentences)
                {
                    if (currentLength + sentence.Length > maxChunkChars && currentChunk.Length > 0)
                    {
                        chunks.Add(currentChunk.ToString().Trim());
                        currentChunk.Clear();
                        currentLength = 0;
                    }

                    currentChunk.Append(sentence).Append(' ');
                    currentLength += sentence.Length + 1;
                }
            }
            else
            {
                currentChunk.Append(trimmedParagraph).Append("\n\n");
                currentLength += trimmedParagraph.Length + 2;
            }
        }

        if (currentChunk.Length > 0)
        {
            chunks.Add(currentChunk.ToString().Trim());
        }

        // Ensure we have at least one chunk
        if (chunks.Count == 0 && !string.IsNullOrWhiteSpace(text))
        {
            chunks.Add(text.Trim());
        }

        return chunks;
    }

    private static List<string> SplitIntoSentences(string text)
    {
        var sentences = new List<string>();
        var current = new StringBuilder();

        foreach (var ch in text)
        {
            current.Append(ch);

            if (ch is '.' or '!' or '?' && current.Length > 0)
            {
                sentences.Add(current.ToString().Trim());
                current.Clear();
            }
        }

        if (current.Length > 0)
        {
            sentences.Add(current.ToString().Trim());
        }

        return sentences;
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

        // Calculate distribution
        var distribution = results
            .GroupBy(r => r.Sentiment)
            .ToDictionary(g => g.Key, g => g.Count());

        // Fill in missing labels
        foreach (var label in Enum.GetValues<SentimentLabel>())
        {
            distribution.TryAdd(label, 0);
        }

        // Calculate weighted score (confidence-weighted average of normalized scores)
        var totalWeight = results.Sum(r => r.Confidence);
        var weightedScore = totalWeight > 0
            ? results.Sum(r => r.NormalizedScore * r.Confidence) / totalWeight
            : 0;

        // Determine overall sentiment from weighted score
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

        _session?.Dispose();
        _tokenizer = null;
        _initLock.Dispose();

        GC.SuppressFinalize(this);
    }
}
