using System.Diagnostics;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Microsoft.ML.OnnxRuntime;
using Microsoft.ML.OnnxRuntime.Tensors;
using Mostlylucid.BotDetection.Metrics;
using Mostlylucid.BotDetection.Models;

namespace Mostlylucid.BotDetection.Detectors;

/// <summary>
///     ONNX-based bot detection using a lightweight classification model.
///     Falls back gracefully if model is not available.
/// </summary>
public class OnnxDetector : IDetector, IDisposable
{
    private readonly ILogger<OnnxDetector> _logger;
    private readonly BotDetectionOptions _options;
    private readonly BotDetectionMetrics? _metrics;
    private readonly SemaphoreSlim _initLock = new(1, 1);
    private InferenceSession? _session;
    private bool _initialized;
    private bool _modelAvailable;
    private bool _disposed;

    // Feature indices for the model
    private const int FeatureCount = 12;

    public OnnxDetector(
        ILogger<OnnxDetector> logger,
        IOptions<BotDetectionOptions> options,
        BotDetectionMetrics? metrics = null)
    {
        _logger = logger;
        _options = options.Value;
        _metrics = metrics;
    }

    public string Name => "ONNX Detector";

    /// <summary>Stage 3: AI/ML - can use all prior signals for learning</summary>
    public DetectorStage Stage => DetectorStage.Intelligence;

    public async Task<DetectorResult> DetectAsync(HttpContext context, CancellationToken cancellationToken = default)
    {
        var stopwatch = Stopwatch.StartNew();
        var result = new DetectorResult();

        // Skip if ONNX detection is disabled
        if (!_options.AiDetection.Onnx.Enabled)
        {
            stopwatch.Stop();
            return result;
        }

        try
        {
            // Initialize model on first use
            await EnsureInitializedAsync(cancellationToken);

            if (!_modelAvailable)
            {
                // No model available, use heuristic fallback
                return await HeuristicFallbackAsync(context, stopwatch);
            }

            // Extract features from request
            var features = ExtractFeatures(context);

            // Run inference
            var prediction = await RunInferenceAsync(features, cancellationToken);

            if (prediction.IsBot)
            {
                result.Confidence = prediction.Confidence;
                result.BotType = prediction.BotType;
                result.Reasons.Add(new DetectionReason
                {
                    Category = "ONNX",
                    Detail = $"ML model classified as bot (confidence: {prediction.Confidence:F2})",
                    ConfidenceImpact = prediction.Confidence
                });
            }

            stopwatch.Stop();
            _metrics?.RecordDetection(result.Confidence, result.Confidence > _options.BotThreshold, stopwatch.Elapsed, Name);
        }
        catch (Exception ex)
        {
            stopwatch.Stop();
            _metrics?.RecordError(Name, ex.GetType().Name);
            _logger.LogWarning(ex, "ONNX detection failed, continuing without it");
        }

        return result;
    }

    private async Task EnsureInitializedAsync(CancellationToken cancellationToken)
    {
        if (_initialized) return;

        await _initLock.WaitAsync(cancellationToken);
        try
        {
            if (_initialized) return;

            var modelPath = GetModelPath();

            if (!File.Exists(modelPath))
            {
                if (_options.AiDetection.Onnx.AutoDownloadModel && !string.IsNullOrEmpty(_options.AiDetection.Onnx.ModelDownloadUrl))
                {
                    await DownloadModelAsync(modelPath, cancellationToken);
                }
            }

            if (File.Exists(modelPath))
            {
                try
                {
                    var sessionOptions = new SessionOptions();

                    if (_options.AiDetection.Onnx.UseGpu)
                    {
                        // Try to use CUDA if available
                        try
                        {
                            sessionOptions.AppendExecutionProvider_CUDA();
                            _logger.LogInformation("ONNX using CUDA GPU acceleration");
                        }
                        catch
                        {
                            _logger.LogWarning("CUDA not available, falling back to CPU");
                        }
                    }

                    _session = new InferenceSession(modelPath, sessionOptions);
                    _modelAvailable = true;
                    _logger.LogInformation("ONNX model loaded from {Path}", modelPath);
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Failed to load ONNX model, using heuristic fallback");
                    _modelAvailable = false;
                }
            }
            else
            {
                _logger.LogInformation("No ONNX model available, using heuristic fallback for ONNX detection");
                _modelAvailable = false;
            }

            _initialized = true;
        }
        finally
        {
            _initLock.Release();
        }
    }

    private string GetModelPath()
    {
        if (!string.IsNullOrEmpty(_options.AiDetection.Onnx.ModelPath))
            return _options.AiDetection.Onnx.ModelPath;

        var modelsDir = Path.Combine(AppContext.BaseDirectory, "models");
        return Path.Combine(modelsDir, "bot_classifier.onnx");
    }

    private async Task DownloadModelAsync(string modelPath, CancellationToken cancellationToken)
    {
        var directory = Path.GetDirectoryName(modelPath);
        if (!string.IsNullOrEmpty(directory) && !Directory.Exists(directory))
            Directory.CreateDirectory(directory);

        try
        {
            using var httpClient = new HttpClient();
            httpClient.Timeout = TimeSpan.FromSeconds(60);

            _logger.LogInformation("Downloading ONNX model from {Url}", _options.AiDetection.Onnx.ModelDownloadUrl);

            using var response = await httpClient.GetAsync(_options.AiDetection.Onnx.ModelDownloadUrl, cancellationToken);
            response.EnsureSuccessStatusCode();

            await using var fileStream = File.Create(modelPath);
            await response.Content.CopyToAsync(fileStream, cancellationToken);

            _logger.LogInformation("ONNX model downloaded successfully to {Path}", modelPath);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to download ONNX model");
        }
    }

    /// <summary>
    ///     Extracts numerical features from the HTTP request for ML classification.
    /// </summary>
    private float[] ExtractFeatures(HttpContext context)
    {
        var features = new float[FeatureCount];
        var headers = context.Request.Headers;
        var userAgent = headers.UserAgent.ToString();

        // Feature 0: User-Agent length (normalized)
        features[0] = Math.Min(userAgent.Length / 200f, 1f);

        // Feature 1: Has Accept-Language header
        features[1] = headers.ContainsKey("Accept-Language") ? 1f : 0f;

        // Feature 2: Has Accept header
        features[2] = headers.ContainsKey("Accept") ? 1f : 0f;

        // Feature 3: Has Referer header
        features[3] = headers.ContainsKey("Referer") ? 1f : 0f;

        // Feature 4: Has cookies
        features[4] = context.Request.Cookies.Any() ? 1f : 0f;

        // Feature 5: Header count (normalized)
        features[5] = Math.Min(headers.Count / 20f, 1f);

        // Feature 6: Contains "bot" in UA (case insensitive)
        features[6] = userAgent.Contains("bot", StringComparison.OrdinalIgnoreCase) ? 1f : 0f;

        // Feature 7: Contains "spider" in UA
        features[7] = userAgent.Contains("spider", StringComparison.OrdinalIgnoreCase) ? 1f : 0f;

        // Feature 8: Contains "crawler" in UA
        features[8] = userAgent.Contains("crawler", StringComparison.OrdinalIgnoreCase) ? 1f : 0f;

        // Feature 9: Contains "http" in UA (common for bots declaring their URL)
        features[9] = userAgent.Contains("http", StringComparison.OrdinalIgnoreCase) ? 1f : 0f;

        // Feature 10: Is Accept header generic "*/*"
        var accept = headers.Accept.ToString();
        features[10] = accept == "*/*" ? 1f : 0f;

        // Feature 11: Connection header is "close"
        var connection = headers.Connection.ToString().ToLowerInvariant();
        features[11] = connection == "close" ? 1f : 0f;

        return features;
    }

    private Task<(bool IsBot, double Confidence, BotType BotType)> RunInferenceAsync(
        float[] features, CancellationToken cancellationToken)
    {
        if (_session == null)
            return Task.FromResult((false, 0.0, BotType.Unknown));

        try
        {
            // Create input tensor
            var inputTensor = new DenseTensor<float>(features, [1, FeatureCount]);
            var inputs = new List<NamedOnnxValue>
            {
                NamedOnnxValue.CreateFromTensor("input", inputTensor)
            };

            // Run inference
            using var results = _session.Run(inputs);
            var output = results.First().AsTensor<float>();

            // Interpret output (assuming sigmoid output for binary classification)
            var botProbability = output[0];

            // Apply softmax if we have 2 outputs, otherwise treat as sigmoid
            if (output.Length >= 2)
            {
                var humanProb = Math.Exp(output[0]);
                var botProb = Math.Exp(output[1]);
                var total = humanProb + botProb;
                botProbability = (float)(botProb / total);
            }

            var isBot = botProbability > 0.5f;
            var confidence = Math.Abs(botProbability - 0.5) * 2; // Scale to 0-1

            return Task.FromResult((isBot, (double)confidence, isBot ? BotType.Scraper : BotType.Unknown));
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "ONNX inference failed");
            return Task.FromResult((false, 0.0, BotType.Unknown));
        }
    }

    /// <summary>
    ///     Heuristic fallback when no ONNX model is available.
    ///     Uses feature weights learned from typical bot patterns.
    /// </summary>
    private Task<DetectorResult> HeuristicFallbackAsync(HttpContext context, Stopwatch stopwatch)
    {
        var result = new DetectorResult();
        var features = ExtractFeatures(context);

        // Heuristic weights (learned from typical bot vs human patterns)
        float[] weights =
        [
            -0.5f,  // Longer UA = less likely bot
            -0.8f,  // Has Accept-Language = less likely bot
            -0.3f,  // Has Accept = less likely bot
            -0.5f,  // Has Referer = less likely bot
            -0.7f,  // Has cookies = less likely bot
            -0.4f,  // More headers = less likely bot
            0.9f,   // Contains "bot" = very likely bot
            0.8f,   // Contains "spider" = likely bot
            0.8f,   // Contains "crawler" = likely bot
            0.6f,   // Contains "http" in UA = likely bot
            0.4f,   // Generic Accept = slightly likely bot
            0.3f    // Connection close = slightly likely bot
        ];

        float bias = 0.2f; // Base probability

        // Calculate weighted sum
        float score = bias;
        for (int i = 0; i < FeatureCount; i++)
        {
            score += features[i] * weights[i];
        }

        // Apply sigmoid to get probability
        var probability = 1.0 / (1.0 + Math.Exp(-score));

        if (probability > 0.5)
        {
            result.Confidence = (probability - 0.5) * 2; // Scale 0.5-1.0 to 0-1
            result.BotType = BotType.Scraper;
            result.Reasons.Add(new DetectionReason
            {
                Category = "ONNX-Heuristic",
                Detail = $"Heuristic: {probability:P0} bot likelihood",
                ConfidenceImpact = result.Confidence
            });
        }
        else
        {
            // Human-like - return negative impact (helps with demo visibility)
            var humanProbability = 1.0 - probability;
            result.Confidence = (0.5 - probability) * 2; // Scale 0-0.5 to 0-1 (inverted)
            result.Reasons.Add(new DetectionReason
            {
                Category = "ONNX-Heuristic",
                Detail = $"Heuristic: {humanProbability:P0} human likelihood",
                ConfidenceImpact = -result.Confidence // Negative = human indicator
            });
        }

        stopwatch.Stop();
        _metrics?.RecordDetection(result.Confidence, result.Confidence > _options.BotThreshold, stopwatch.Elapsed, Name);

        return Task.FromResult(result);
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
            _session?.Dispose();
            _initLock.Dispose();
        }

        _disposed = true;
    }
}
