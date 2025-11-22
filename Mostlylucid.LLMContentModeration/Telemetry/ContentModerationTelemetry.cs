using System.Diagnostics;
using Mostlylucid.LLMContentModeration.Models;

namespace Mostlylucid.LLMContentModeration.Telemetry;

/// <summary>
/// Telemetry instrumentation for content moderation operations
/// </summary>
public static class ContentModerationTelemetry
{
    /// <summary>
    /// Activity source name for content moderation
    /// </summary>
    public const string ActivitySourceName = "Mostlylucid.LLMContentModeration";

    /// <summary>
    /// Activity source for content moderation telemetry
    /// </summary>
    public static readonly ActivitySource ActivitySource = new(ActivitySourceName, GetVersion());

    private static string GetVersion()
    {
        return typeof(ContentModerationTelemetry).Assembly.GetName().Version?.ToString() ?? "1.0.0";
    }

    /// <summary>
    /// Starts an activity for content moderation
    /// </summary>
    /// <param name="contentLength">Length of the content being moderated</param>
    /// <param name="mode">The moderation mode being applied</param>
    public static Activity? StartModerationActivity(int contentLength, ModerationMode? mode = null)
    {
        var activity = ActivitySource.StartActivity("ContentModeration.Moderate", ActivityKind.Internal);

        if (activity != null)
        {
            activity.SetTag("mostlylucid.contentmoderation.content_length", contentLength);
            if (mode.HasValue)
                activity.SetTag("mostlylucid.contentmoderation.mode", mode.Value.ToString());
        }

        return activity;
    }

    /// <summary>
    /// Starts an activity for PII detection
    /// </summary>
    /// <param name="contentLength">Length of the content being analyzed</param>
    /// <param name="useLlmEnhancement">Whether LLM enhancement is enabled</param>
    public static Activity? StartPiiDetectionActivity(int contentLength, bool useLlmEnhancement = false)
    {
        var activity = ActivitySource.StartActivity("ContentModeration.PiiDetection", ActivityKind.Internal);

        if (activity != null)
        {
            activity.SetTag("mostlylucid.contentmoderation.content_length", contentLength);
            activity.SetTag("mostlylucid.contentmoderation.pii.llm_enhancement", useLlmEnhancement);
        }

        return activity;
    }

    /// <summary>
    /// Starts an activity for content classification
    /// </summary>
    /// <param name="contentLength">Length of the content being classified</param>
    public static Activity? StartClassificationActivity(int contentLength)
    {
        var activity = ActivitySource.StartActivity("ContentModeration.Classification", ActivityKind.Internal);

        if (activity != null)
        {
            activity.SetTag("mostlylucid.contentmoderation.content_length", contentLength);
        }

        return activity;
    }

    /// <summary>
    /// Records content moderation result on the activity
    /// </summary>
    public static void RecordResult(Activity? activity, ModerationResult result)
    {
        if (activity == null)
            return;

        activity.SetTag("mostlylucid.contentmoderation.is_flagged", result.IsFlagged);
        activity.SetTag("mostlylucid.contentmoderation.is_blocked", result.IsBlocked);
        activity.SetTag("mostlylucid.contentmoderation.processing_time_ms", result.ProcessingTimeMs);
        activity.SetTag("mostlylucid.contentmoderation.mode", result.Mode.ToString());
        activity.SetTag("mostlylucid.contentmoderation.success", result.Success);

        // Record flag details
        if (result.Flags.Count > 0)
        {
            activity.SetTag("mostlylucid.contentmoderation.flag_count", result.Flags.Count);
            var categories = string.Join(",", result.Flags.Select(f => f.Category.ToString()));
            activity.SetTag("mostlylucid.contentmoderation.categories", categories);
        }

        // Record PII details
        if (result.PiiMatches.Count > 0)
        {
            activity.SetTag("mostlylucid.contentmoderation.pii_match_count", result.PiiMatches.Count);
            var piiTypes = string.Join(",", result.PiiMatches.Select(p => p.Type.ToString()).Distinct());
            activity.SetTag("mostlylucid.contentmoderation.pii_types", piiTypes);
        }

        // Record errors if any
        if (result.Errors.Count > 0)
        {
            activity.SetTag("mostlylucid.contentmoderation.error_count", result.Errors.Count);
            activity.SetStatus(ActivityStatusCode.Error, result.Errors.First());
        }
        else
        {
            activity.SetStatus(ActivityStatusCode.Ok);
        }
    }

    /// <summary>
    /// Records PII detection result on the activity
    /// </summary>
    public static void RecordPiiResult(Activity? activity, List<PiiMatch> matches)
    {
        if (activity == null)
            return;

        activity.SetTag("mostlylucid.contentmoderation.pii_match_count", matches.Count);

        if (matches.Count > 0)
        {
            var piiTypes = string.Join(",", matches.Select(p => p.Type.ToString()).Distinct());
            activity.SetTag("mostlylucid.contentmoderation.pii_types", piiTypes);
        }

        activity.SetStatus(ActivityStatusCode.Ok);
    }

    /// <summary>
    /// Records content classification result on the activity
    /// </summary>
    public static void RecordClassificationResult(Activity? activity, List<ContentFlag> flags)
    {
        if (activity == null)
            return;

        activity.SetTag("mostlylucid.contentmoderation.flag_count", flags.Count);

        if (flags.Count > 0)
        {
            var categories = string.Join(",", flags.Select(f => f.Category.ToString()));
            activity.SetTag("mostlylucid.contentmoderation.categories", categories);

            // Record average confidence
            var avgConfidence = flags.Average(f => f.Confidence);
            activity.SetTag("mostlylucid.contentmoderation.avg_confidence", avgConfidence);
        }

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
