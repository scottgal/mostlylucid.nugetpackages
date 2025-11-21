using Florence2;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Mostlylucid.LlmAltText.Models;

namespace Mostlylucid.LlmAltText.Services;

/// <summary>
///     Florence-2 Vision Language Model implementation for alt text generation and OCR
/// </summary>
public class Florence2ImageAnalysisService : IImageAnalysisService, IDisposable
{
    private readonly SemaphoreSlim _initLock = new(1, 1);
    private readonly bool _isInitialized;
    private readonly ILogger<Florence2ImageAnalysisService> _logger;
    private readonly Florence2Model? _model;
    private readonly AltTextOptions _options;

    public Florence2ImageAnalysisService(
        ILogger<Florence2ImageAnalysisService> logger,
        IOptions<AltTextOptions> options)
    {
        _logger = logger;
        _options = options.Value;

        try
        {
            LogInfo("Initializing Florence-2 Vision Language Model...");
            LogInfo($"Model path: {_options.ModelPath}");
            LogInfo("Note: Models (~800MB) will be downloaded on first use if not present");

            var modelSource = new FlorenceModelDownloader(_options.ModelPath);

            // Download models if not already present
            LogInfo("Checking for model files...");
            modelSource
                .DownloadModelsAsync(
                    status => LogModelDownloadStatus(status),
                    _logger,
                    CancellationToken.None)
                .GetAwaiter()
                .GetResult();

            _model = new Florence2Model(modelSource);
            _isInitialized = true;

            LogInfo("Florence-2 model initialized successfully");
            LogInfo("Available task types: CAPTION, DETAILED_CAPTION, MORE_DETAILED_CAPTION, OCR");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to initialize Florence-2 model. Alt text generation will not be available.");
            _isInitialized = false;
        }
    }

    public void Dispose()
    {
        _initLock.Dispose();
        GC.SuppressFinalize(this);
    }

    public bool IsReady => _isInitialized && _model is not null;

    public async Task<string> GenerateAltTextAsync(Stream imageStream, string taskType = "MORE_DETAILED_CAPTION")
    {
        await EnsureInitializedAsync();

        var task = ResolveTaskType(taskType, TaskTypes.MORE_DETAILED_CAPTION);

        try
        {
            LogInfo($"Generating alt text using task type: {task}");
            var startTime = DateTime.UtcNow;

            var results = _model!.Run(task, new[] { imageStream }, _options.AltTextPrompt, CancellationToken.None);
            var altText = results.FirstOrDefault()?.PureText;

            var duration = (DateTime.UtcNow - startTime).TotalMilliseconds;
            LogInfo($"Alt text generated in {duration:F0}ms");

            if (string.IsNullOrWhiteSpace(altText))
            {
                _logger.LogWarning("No alt text generated for image");
                return "No description available";
            }

            var normalized = NormalizeAltText(altText);
            LogInfo($"Generated alt text: {normalized.Substring(0, Math.Min(50, normalized.Length))}...");

            return normalized;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error generating alt text");
            throw;
        }
    }

    public async Task<string> ExtractTextAsync(Stream imageStream)
    {
        await EnsureInitializedAsync();

        try
        {
            LogInfo("Extracting text from image using OCR");
            var startTime = DateTime.UtcNow;

            var results = _model!.Run(TaskTypes.OCR, new[] { imageStream }, string.Empty, CancellationToken.None);
            var ocrText = results.FirstOrDefault()?.PureText;

            var duration = (DateTime.UtcNow - startTime).TotalMilliseconds;
            LogInfo($"Text extracted in {duration:F0}ms");

            if (string.IsNullOrWhiteSpace(ocrText))
            {
                LogInfo("No text found in image");
                return "No text found";
            }

            var trimmed = ocrText.Trim();
            LogInfo($"Extracted {trimmed.Length} characters of text");

            return trimmed;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error extracting text from image");
            throw;
        }
    }

    public async Task<(string AltText, string ExtractedText)> AnalyzeImageAsync(Stream imageStream)
    {
        await EnsureInitializedAsync();

        try
        {
            LogInfo("Starting complete image analysis (alt text + OCR)");
            var startTime = DateTime.UtcNow;

            // Create a memory stream to allow multiple reads
            using var memoryStream = new MemoryStream();
            await imageStream.CopyToAsync(memoryStream);
            LogInfo($"Image loaded: {memoryStream.Length:N0} bytes");

            // Generate alt text
            memoryStream.Position = 0;
            var altText = await GenerateAltTextAsync(memoryStream, _options.DefaultTaskType);

            // Extract text (OCR)
            memoryStream.Position = 0;
            var extractedText = await ExtractTextAsync(memoryStream);

            var duration = (DateTime.UtcNow - startTime).TotalMilliseconds;
            LogInfo($"Complete image analysis finished in {duration:F0}ms");

            return (altText, extractedText);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error analyzing image");
            throw;
        }
    }

    public async Task<ImageAnalysisResult> AnalyzeWithClassificationAsync(Stream imageStream)
    {
        await EnsureInitializedAsync();

        try
        {
            LogInfo("Starting complete image analysis with classification");
            var startTime = DateTime.UtcNow;

            // Create a memory stream to allow multiple reads
            using var memoryStream = new MemoryStream();
            await imageStream.CopyToAsync(memoryStream);
            LogInfo($"Image loaded: {memoryStream.Length:N0} bytes");

            // Generate alt text
            memoryStream.Position = 0;
            var altText = await GenerateAltTextAsync(memoryStream, _options.DefaultTaskType);

            // Extract text (OCR)
            memoryStream.Position = 0;
            var extractedText = await ExtractTextAsync(memoryStream);

            // Classify content type based on alt text and OCR results
            var (contentType, confidence) = ClassifyFromResults(altText, extractedText, memoryStream.Length);

            var hasSignificantText = !string.IsNullOrWhiteSpace(extractedText) &&
                                     extractedText != "No text found" &&
                                     extractedText.Length > 20;

            var duration = (DateTime.UtcNow - startTime).TotalMilliseconds;
            LogInfo($"Complete analysis with classification finished in {duration:F0}ms - Type: {contentType}");

            return new ImageAnalysisResult
            {
                AltText = altText,
                ExtractedText = extractedText,
                ContentType = contentType,
                ContentTypeConfidence = confidence,
                HasSignificantText = hasSignificantText
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error analyzing image with classification");
            throw;
        }
    }

    public async Task<(ImageContentType Type, double Confidence)> ClassifyContentTypeAsync(Stream imageStream)
    {
        await EnsureInitializedAsync();

        try
        {
            LogInfo("Classifying image content type");

            // Create a memory stream to allow multiple reads
            using var memoryStream = new MemoryStream();
            await imageStream.CopyToAsync(memoryStream);

            // Get alt text for classification
            memoryStream.Position = 0;
            var altText = await GenerateAltTextAsync(memoryStream, "CAPTION");

            // Get OCR results
            memoryStream.Position = 0;
            var extractedText = await ExtractTextAsync(memoryStream);

            return ClassifyFromResults(altText, extractedText, memoryStream.Length);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error classifying image content type");
            throw;
        }
    }

    private (ImageContentType Type, double Confidence) ClassifyFromResults(string altText, string extractedText,
        long imageSize)
    {
        var altLower = altText.ToLowerInvariant();
        var ocrLength = extractedText?.Length ?? 0;
        var hasOcrText = !string.IsNullOrWhiteSpace(extractedText) && extractedText != "No text found";

        // Document indicators
        var documentKeywords = new[]
        {
            "document", "text", "paper", "page", "form", "letter", "contract", "invoice", "receipt", "pdf", "printed"
        };
        var screenshotKeywords = new[]
        {
            "screenshot", "screen", "window", "browser", "desktop", "interface", "ui", "menu", "button", "toolbar",
            "application"
        };
        var chartKeywords = new[]
        {
            "chart", "graph", "diagram", "plot", "bar chart", "pie chart", "line graph", "statistics",
            "data visualization"
        };
        var illustrationKeywords = new[]
        {
            "illustration", "drawing", "cartoon", "artwork", "painting", "sketch", "artistic", "animated", "clipart"
        };
        var diagramKeywords = new[]
            { "diagram", "flowchart", "schematic", "architecture", "workflow", "process", "uml", "er diagram" };
        var photoKeywords = new[]
        {
            "photo", "photograph", "picture", "image of", "person", "people", "landscape", "building", "outdoor",
            "indoor", "nature", "animal"
        };

        // Calculate scores
        var scores = new Dictionary<ImageContentType, double>
        {
            [ImageContentType.Document] = CalculateKeywordScore(altLower, documentKeywords) +
                                          (hasOcrText && ocrLength > 100 ? 0.4 : 0),
            [ImageContentType.Screenshot] = CalculateKeywordScore(altLower, screenshotKeywords) +
                                            (hasOcrText && ocrLength > 20 && ocrLength < 500 ? 0.2 : 0),
            [ImageContentType.Chart] = CalculateKeywordScore(altLower, chartKeywords),
            [ImageContentType.Illustration] = CalculateKeywordScore(altLower, illustrationKeywords),
            [ImageContentType.Diagram] = CalculateKeywordScore(altLower, diagramKeywords),
            [ImageContentType.Photograph] = CalculateKeywordScore(altLower, photoKeywords) + (hasOcrText ? -0.1 : 0.2)
        };

        // High OCR text content strongly suggests document
        if (hasOcrText && ocrLength > 200) scores[ImageContentType.Document] += 0.3;

        // Find best match
        var bestMatch = scores.OrderByDescending(x => x.Value).First();

        if (bestMatch.Value < 0.1)
        {
            // No strong signals - default based on OCR presence
            if (hasOcrText && ocrLength > 50) return (ImageContentType.Document, 0.5);
            return (ImageContentType.Photograph, 0.4); // Default assumption
        }

        var confidence = Math.Min(bestMatch.Value, 1.0);
        LogInfo($"Classified as {bestMatch.Key} with confidence {confidence:F2}");

        return (bestMatch.Key, confidence);
    }

    private double CalculateKeywordScore(string text, string[] keywords)
    {
        double score = 0;
        foreach (var keyword in keywords)
            if (text.Contains(keyword))
                score += 0.2;

        return Math.Min(score, 0.8);
    }

    private async Task EnsureInitializedAsync()
    {
        if (_isInitialized && _model is not null) return;

        await _initLock.WaitAsync();
        try
        {
            if (_isInitialized && _model is not null) return;

            throw new InvalidOperationException(
                "Florence-2 model failed to initialize. Please check logs for details. " +
                "Ensure you have sufficient disk space (~800MB) and network connectivity for model downloads.");
        }
        finally
        {
            _initLock.Release();
        }
    }

    private TaskTypes ResolveTaskType(string taskType, TaskTypes fallback)
    {
        if (Enum.TryParse<TaskTypes>(taskType, true, out var parsed)) return parsed;

        _logger.LogWarning("Unknown task type '{TaskType}'; using fallback '{Fallback}'", taskType, fallback);
        return fallback;
    }

    private string NormalizeAltText(string altText)
    {
        var normalized = altText.Trim();

        // Ensure proper sentence ending
        if (!normalized.EndsWith(".") && !normalized.EndsWith("!") && !normalized.EndsWith("?")) normalized += ".";

        // Check word count and warn if exceeding recommendation
        var wordCount = normalized.Split(new[] { ' ', '\t', '\n' }, StringSplitOptions.RemoveEmptyEntries).Length;
        if (wordCount > _options.MaxWords)
            _logger.LogWarning(
                "Generated alt text has {WordCount} words, exceeding recommended maximum of {MaxWords}",
                wordCount, _options.MaxWords);

        return normalized;
    }

    private void LogModelDownloadStatus(IStatus status)
    {
        if (_options.EnableDiagnosticLogging)
        {
            if (!string.IsNullOrEmpty(status.Error))
                _logger.LogError("Model download error: {Error}", status.Error);
            else
                _logger.LogInformation(
                    "Model download progress: {Progress:P1} - {Message}",
                    status.Progress,
                    status.Message ?? "Processing");
        }
    }

    private void LogInfo(string message)
    {
        if (_options.EnableDiagnosticLogging) _logger.LogInformation(message);
    }
}