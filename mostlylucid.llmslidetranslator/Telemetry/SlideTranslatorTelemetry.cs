using System.Diagnostics;
using mostlylucid.llmslidetranslator.Models;

namespace mostlylucid.llmslidetranslator.Telemetry;

/// <summary>
/// Telemetry instrumentation for slide translation operations
/// </summary>
public static class SlideTranslatorTelemetry
{
    /// <summary>
    /// Activity source name for slide translator
    /// </summary>
    public const string ActivitySourceName = "Mostlylucid.LlmSlideTranslator";

    /// <summary>
    /// Activity source for slide translator telemetry
    /// </summary>
    public static readonly ActivitySource ActivitySource = new(ActivitySourceName, GetVersion());

    private static string GetVersion()
    {
        return typeof(SlideTranslatorTelemetry).Assembly.GetName().Version?.ToString() ?? "1.0.0";
    }

    /// <summary>
    /// Starts an activity for document translation
    /// </summary>
    public static Activity? StartTranslateDocumentActivity(
        string documentId,
        string sourceLanguage,
        string targetLanguage,
        TranslationMethod method)
    {
        var activity = ActivitySource.StartActivity("SlideTranslator.TranslateDocument", ActivityKind.Internal);

        if (activity != null)
        {
            activity.SetTag("mostlylucid.slidetranslator.document_id", documentId);
            activity.SetTag("mostlylucid.slidetranslator.source_language", sourceLanguage);
            activity.SetTag("mostlylucid.slidetranslator.target_language", targetLanguage);
            activity.SetTag("mostlylucid.slidetranslator.method", method.ToString());
        }

        return activity;
    }

    /// <summary>
    /// Starts an activity for block translation
    /// </summary>
    public static Activity? StartTranslateBlockActivity(
        string documentId,
        int blockIndex,
        string sourceLanguage,
        string targetLanguage)
    {
        var activity = ActivitySource.StartActivity("SlideTranslator.TranslateBlock", ActivityKind.Internal);

        if (activity != null)
        {
            activity.SetTag("mostlylucid.slidetranslator.document_id", documentId);
            activity.SetTag("mostlylucid.slidetranslator.block_index", blockIndex);
            activity.SetTag("mostlylucid.slidetranslator.source_language", sourceLanguage);
            activity.SetTag("mostlylucid.slidetranslator.target_language", targetLanguage);
        }

        return activity;
    }

    /// <summary>
    /// Records translation result on the activity
    /// </summary>
    public static void RecordResult(Activity? activity, TranslationResult result)
    {
        if (activity == null)
            return;

        activity.SetTag("mostlylucid.slidetranslator.source_language", result.SourceLanguage);
        activity.SetTag("mostlylucid.slidetranslator.target_language", result.TargetLanguage);
        activity.SetTag("mostlylucid.slidetranslator.chunk_count", result.Blocks.Count);
        activity.SetTag("mostlylucid.slidetranslator.method", result.Method.ToString());
        activity.SetTag("mostlylucid.slidetranslator.duration_ms", result.Duration.TotalMilliseconds);
        activity.SetTag("mostlylucid.slidetranslator.error_count", result.Errors.Count);

        if (result.Errors.Count > 0)
        {
            activity.SetStatus(ActivityStatusCode.Error, string.Join("; ", result.Errors));
        }
        else
        {
            activity.SetStatus(ActivityStatusCode.Ok);
        }
    }

    /// <summary>
    /// Records block translation result on the activity
    /// </summary>
    public static void RecordBlockResult(Activity? activity, TranslationBlock block)
    {
        if (activity == null)
            return;

        activity.SetTag("mostlylucid.slidetranslator.block_translated", !string.IsNullOrEmpty(block.TranslatedText));
        activity.SetTag("mostlylucid.slidetranslator.should_translate", block.ShouldTranslate);
        activity.SetStatus(ActivityStatusCode.Ok);
    }

    /// <summary>
    /// Records an exception on the activity
    /// </summary>
    public static void RecordException(Activity? activity, Exception ex)
    {
        if (activity == null)
            return;

        activity.SetStatus(ActivityStatusCode.Error, ex.Message);
        activity.SetTag("exception.type", ex.GetType().FullName);
        activity.SetTag("exception.message", ex.Message);
    }
}
