using System.Diagnostics;
using Mostlylucid.LlmPiiRedactor.Models;

namespace Mostlylucid.LlmPiiRedactor.Telemetry;

/// <summary>
/// Telemetry instrumentation for PII redaction operations
/// </summary>
public static class PiiRedactorTelemetry
{
    /// <summary>
    /// Activity source name for PII redaction
    /// </summary>
    public const string ActivitySourceName = "Mostlylucid.LlmPiiRedactor";

    /// <summary>
    /// Activity source for PII redaction telemetry
    /// </summary>
    public static readonly ActivitySource ActivitySource = new(ActivitySourceName, GetVersion());

    private static string GetVersion()
    {
        return typeof(PiiRedactorTelemetry).Assembly.GetName().Version?.ToString() ?? "1.0.0";
    }

    /// <summary>
    /// Starts an activity for PII redaction
    /// </summary>
    /// <param name="textLength">Length of the text being processed</param>
    public static Activity? StartRedactActivity(int textLength = 0)
    {
        var activity = ActivitySource.StartActivity("PiiRedactor.Redact", ActivityKind.Internal);

        if (activity != null && textLength > 0)
        {
            activity.SetTag("mostlylucid.piiredactor.text_length", textLength);
        }

        return activity;
    }

    /// <summary>
    /// Starts an activity for PII detection
    /// </summary>
    /// <param name="textLength">Length of the text being processed</param>
    public static Activity? StartDetectActivity(int textLength = 0)
    {
        var activity = ActivitySource.StartActivity("PiiRedactor.Detect", ActivityKind.Internal);

        if (activity != null && textLength > 0)
        {
            activity.SetTag("mostlylucid.piiredactor.text_length", textLength);
        }

        return activity;
    }

    /// <summary>
    /// Starts an activity for checking if text contains PII
    /// </summary>
    /// <param name="textLength">Length of the text being processed</param>
    public static Activity? StartContainsPiiActivity(int textLength = 0)
    {
        var activity = ActivitySource.StartActivity("PiiRedactor.ContainsPii", ActivityKind.Internal);

        if (activity != null && textLength > 0)
        {
            activity.SetTag("mostlylucid.piiredactor.text_length", textLength);
        }

        return activity;
    }

    /// <summary>
    /// Records PII redaction result on the activity
    /// </summary>
    /// <param name="activity">The activity to record on</param>
    /// <param name="result">The redaction result</param>
    public static void RecordRedactionResult(Activity? activity, RedactionResult result)
    {
        if (activity == null)
            return;

        activity.SetTag("mostlylucid.piiredactor.contained_pii", result.ContainedPii);
        activity.SetTag("mostlylucid.piiredactor.matches_count", result.Matches.Count);
        activity.SetTag("mostlylucid.piiredactor.unique_types_count", result.UniqueTypesCount);

        // Record the PII types found as a comma-separated string
        if (result.ContainedPii)
        {
            var typesFound = string.Join(",", result.TypeCounts.Keys.Select(t => t.ToString()));
            activity.SetTag("mostlylucid.piiredactor.pii_types_found", typesFound);

            // Record character counts
            var charactersRedacted = result.Matches.Sum(m => m.Length);
            activity.SetTag("mostlylucid.piiredactor.characters_redacted", charactersRedacted);
        }

        activity.SetStatus(ActivityStatusCode.Ok);
    }

    /// <summary>
    /// Records PII detection result on the activity
    /// </summary>
    /// <param name="activity">The activity to record on</param>
    /// <param name="matches">The detected matches</param>
    public static void RecordDetectionResult(Activity? activity, IReadOnlyList<PiiMatch> matches)
    {
        if (activity == null)
            return;

        activity.SetTag("mostlylucid.piiredactor.contained_pii", matches.Count > 0);
        activity.SetTag("mostlylucid.piiredactor.matches_count", matches.Count);

        if (matches.Count > 0)
        {
            var uniqueTypes = matches.Select(m => m.Type).Distinct().ToList();
            activity.SetTag("mostlylucid.piiredactor.unique_types_count", uniqueTypes.Count);

            var typesFound = string.Join(",", uniqueTypes.Select(t => t.ToString()));
            activity.SetTag("mostlylucid.piiredactor.pii_types_found", typesFound);
        }

        activity.SetStatus(ActivityStatusCode.Ok);
    }

    /// <summary>
    /// Records contains PII check result on the activity
    /// </summary>
    /// <param name="activity">The activity to record on</param>
    /// <param name="containsPii">Whether PII was found</param>
    public static void RecordContainsPiiResult(Activity? activity, bool containsPii)
    {
        if (activity == null)
            return;

        activity.SetTag("mostlylucid.piiredactor.contained_pii", containsPii);
        activity.SetStatus(ActivityStatusCode.Ok);
    }

    /// <summary>
    /// Records an exception on the activity
    /// </summary>
    /// <param name="activity">The activity to record on</param>
    /// <param name="ex">The exception that occurred</param>
    public static void RecordException(Activity? activity, Exception ex)
    {
        if (activity == null)
            return;

        activity.SetStatus(ActivityStatusCode.Error, ex.Message);
        activity.SetTag("exception.type", ex.GetType().FullName);
        activity.SetTag("exception.message", ex.Message);
    }
}
