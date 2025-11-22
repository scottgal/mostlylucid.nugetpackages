using System.Diagnostics;
using Mostlylucid.LlmAltText.Services;

namespace Mostlylucid.LlmAltText.Telemetry;

/// <summary>
///     OpenTelemetry instrumentation for LlmAltText operations
/// </summary>
public static class LlmAltTextTelemetry
{
    /// <summary>
    ///     The name of the ActivitySource for LlmAltText operations
    /// </summary>
    public const string ActivitySourceName = "Mostlylucid.LlmAltText";

    /// <summary>
    ///     ActivitySource for creating spans/activities
    /// </summary>
    public static readonly ActivitySource ActivitySource = new(ActivitySourceName, GetVersion());

    private static string GetVersion() =>
        typeof(LlmAltTextTelemetry).Assembly.GetName().Version?.ToString() ?? "1.0.0";

    /// <summary>
    ///     Start an activity for alt text generation
    /// </summary>
    /// <param name="taskType">The vision task type being used</param>
    /// <returns>The started activity, or null if no listeners</returns>
    public static Activity? StartGenerateAltTextActivity(string taskType)
    {
        var activity = ActivitySource.StartActivity("LlmAltText.GenerateAltText", ActivityKind.Internal);
        activity?.SetTag("llmalttext.task_type", taskType);
        activity?.SetTag("llmalttext.operation", "generate_alt_text");
        return activity;
    }

    /// <summary>
    ///     Start an activity for OCR text extraction
    /// </summary>
    /// <returns>The started activity, or null if no listeners</returns>
    public static Activity? StartExtractTextActivity()
    {
        var activity = ActivitySource.StartActivity("LlmAltText.ExtractText", ActivityKind.Internal);
        activity?.SetTag("llmalttext.operation", "extract_text");
        return activity;
    }

    /// <summary>
    ///     Start an activity for complete image analysis (alt text + OCR)
    /// </summary>
    /// <returns>The started activity, or null if no listeners</returns>
    public static Activity? StartAnalyzeImageActivity()
    {
        var activity = ActivitySource.StartActivity("LlmAltText.AnalyzeImage", ActivityKind.Internal);
        activity?.SetTag("llmalttext.operation", "analyze_image");
        return activity;
    }

    /// <summary>
    ///     Start an activity for image analysis with classification
    /// </summary>
    /// <returns>The started activity, or null if no listeners</returns>
    public static Activity? StartAnalyzeWithClassificationActivity()
    {
        var activity = ActivitySource.StartActivity("LlmAltText.AnalyzeWithClassification", ActivityKind.Internal);
        activity?.SetTag("llmalttext.operation", "analyze_with_classification");
        return activity;
    }

    /// <summary>
    ///     Start an activity for content type classification
    /// </summary>
    /// <returns>The started activity, or null if no listeners</returns>
    public static Activity? StartClassifyContentTypeActivity()
    {
        var activity = ActivitySource.StartActivity("LlmAltText.ClassifyContentType", ActivityKind.Internal);
        activity?.SetTag("llmalttext.operation", "classify_content_type");
        return activity;
    }

    /// <summary>
    ///     Record image size information on an activity
    /// </summary>
    /// <param name="activity">The activity to record on</param>
    /// <param name="imageSizeBytes">Size of the image in bytes</param>
    public static void RecordImageSize(Activity? activity, long imageSizeBytes)
    {
        activity?.SetTag("llmalttext.image_size_bytes", imageSizeBytes);
    }

    /// <summary>
    ///     Record alt text generation result
    /// </summary>
    /// <param name="activity">The activity to record on</param>
    /// <param name="altTextLength">Length of the generated alt text</param>
    /// <param name="durationMs">Processing duration in milliseconds</param>
    public static void RecordAltTextResult(Activity? activity, int altTextLength, double durationMs)
    {
        activity?.SetTag("llmalttext.alt_text_length", altTextLength);
        activity?.SetTag("llmalttext.duration_ms", durationMs);
        activity?.SetTag("llmalttext.success", true);
    }

    /// <summary>
    ///     Record OCR text extraction result
    /// </summary>
    /// <param name="activity">The activity to record on</param>
    /// <param name="extractedTextLength">Length of extracted text</param>
    /// <param name="hasText">Whether any text was found</param>
    /// <param name="durationMs">Processing duration in milliseconds</param>
    public static void RecordExtractTextResult(Activity? activity, int extractedTextLength, bool hasText, double durationMs)
    {
        activity?.SetTag("llmalttext.extracted_text_length", extractedTextLength);
        activity?.SetTag("llmalttext.has_text", hasText);
        activity?.SetTag("llmalttext.duration_ms", durationMs);
        activity?.SetTag("llmalttext.success", true);
    }

    /// <summary>
    ///     Record classification result
    /// </summary>
    /// <param name="activity">The activity to record on</param>
    /// <param name="contentType">The classified content type</param>
    /// <param name="confidence">Confidence score for the classification</param>
    /// <param name="durationMs">Processing duration in milliseconds</param>
    public static void RecordClassificationResult(Activity? activity, ImageContentType contentType, double confidence, double durationMs)
    {
        activity?.SetTag("llmalttext.content_type", contentType.ToString());
        activity?.SetTag("llmalttext.content_type_confidence", confidence);
        activity?.SetTag("llmalttext.duration_ms", durationMs);
        activity?.SetTag("llmalttext.success", true);
    }

    /// <summary>
    ///     Record complete analysis result
    /// </summary>
    /// <param name="activity">The activity to record on</param>
    /// <param name="result">The analysis result</param>
    /// <param name="durationMs">Processing duration in milliseconds</param>
    public static void RecordAnalysisResult(Activity? activity, ImageAnalysisResult result, double durationMs)
    {
        activity?.SetTag("llmalttext.alt_text_length", result.AltText?.Length ?? 0);
        activity?.SetTag("llmalttext.extracted_text_length", result.ExtractedText?.Length ?? 0);
        activity?.SetTag("llmalttext.content_type", result.ContentType.ToString());
        activity?.SetTag("llmalttext.content_type_confidence", result.ContentTypeConfidence);
        activity?.SetTag("llmalttext.has_significant_text", result.HasSignificantText);
        activity?.SetTag("llmalttext.duration_ms", durationMs);
        activity?.SetTag("llmalttext.success", true);
    }

    /// <summary>
    ///     Record an exception on the activity
    /// </summary>
    /// <param name="activity">The activity to record on</param>
    /// <param name="exception">The exception that occurred</param>
    public static void RecordException(Activity? activity, Exception exception)
    {
        if (activity == null) return;

        activity.SetStatus(ActivityStatusCode.Error, exception.Message);
        activity.SetTag("llmalttext.success", false);
        activity.SetTag("llmalttext.error_type", exception.GetType().Name);
        activity.SetTag("llmalttext.error_message", exception.Message);

        // Record exception event with stack trace
        var tags = new ActivityTagsCollection
        {
            { "exception.type", exception.GetType().FullName },
            { "exception.message", exception.Message },
            { "exception.stacktrace", exception.StackTrace }
        };
        activity.AddEvent(new ActivityEvent("exception", tags: tags));
    }
}
