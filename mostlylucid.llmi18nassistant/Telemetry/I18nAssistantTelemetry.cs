using System.Diagnostics;
using Mostlylucid.LlmI18nAssistant.Models;

namespace Mostlylucid.LlmI18nAssistant.Telemetry;

/// <summary>
/// Telemetry instrumentation for i18n translation operations
/// </summary>
public static class I18nAssistantTelemetry
{
    /// <summary>
    /// Activity source name for i18n translation
    /// </summary>
    public const string ActivitySourceName = "Mostlylucid.LlmI18nAssistant";

    /// <summary>
    /// Activity source for i18n translation telemetry
    /// </summary>
    public static readonly ActivitySource ActivitySource = new(ActivitySourceName, GetVersion());

    private static string GetVersion()
    {
        return typeof(I18nAssistantTelemetry).Assembly.GetName().Version?.ToString() ?? "1.0.0";
    }

    /// <summary>
    /// Starts an activity for resource file translation
    /// </summary>
    public static Activity? StartTranslateResourceFileActivity(
        string? filePath = null,
        string? sourceLanguage = null,
        string? targetLanguage = null)
    {
        var activity = ActivitySource.StartActivity("I18nAssistant.TranslateResourceFile", ActivityKind.Internal);

        if (activity != null)
        {
            if (filePath != null)
                activity.SetTag("mostlylucid.i18n.file_path", filePath);
            if (sourceLanguage != null)
                activity.SetTag("mostlylucid.i18n.source_language", sourceLanguage);
            if (targetLanguage != null)
                activity.SetTag("mostlylucid.i18n.target_language", targetLanguage);
        }

        return activity;
    }

    /// <summary>
    /// Starts an activity for stream-based translation
    /// </summary>
    public static Activity? StartTranslateStreamActivity(
        ResourceFileType fileType,
        string? sourceLanguage = null,
        string? targetLanguage = null)
    {
        var activity = ActivitySource.StartActivity("I18nAssistant.TranslateStream", ActivityKind.Internal);

        if (activity != null)
        {
            activity.SetTag("mostlylucid.i18n.file_type", fileType.ToString());
            if (sourceLanguage != null)
                activity.SetTag("mostlylucid.i18n.source_language", sourceLanguage);
            if (targetLanguage != null)
                activity.SetTag("mostlylucid.i18n.target_language", targetLanguage);
        }

        return activity;
    }

    /// <summary>
    /// Starts an activity for multi-language translation
    /// </summary>
    public static Activity? StartTranslateMultipleLanguagesActivity(
        string? filePath = null,
        string? sourceLanguage = null,
        int targetLanguageCount = 0)
    {
        var activity = ActivitySource.StartActivity("I18nAssistant.TranslateMultipleLanguages", ActivityKind.Internal);

        if (activity != null)
        {
            if (filePath != null)
                activity.SetTag("mostlylucid.i18n.file_path", filePath);
            if (sourceLanguage != null)
                activity.SetTag("mostlylucid.i18n.source_language", sourceLanguage);
            activity.SetTag("mostlylucid.i18n.target_language_count", targetLanguageCount);
        }

        return activity;
    }

    /// <summary>
    /// Starts an activity for single string translation
    /// </summary>
    public static Activity? StartTranslateStringActivity(
        string? sourceLanguage = null,
        string? targetLanguage = null,
        int? textLength = null)
    {
        var activity = ActivitySource.StartActivity("I18nAssistant.TranslateString", ActivityKind.Internal);

        if (activity != null)
        {
            if (sourceLanguage != null)
                activity.SetTag("mostlylucid.i18n.source_language", sourceLanguage);
            if (targetLanguage != null)
                activity.SetTag("mostlylucid.i18n.target_language", targetLanguage);
            if (textLength.HasValue)
                activity.SetTag("mostlylucid.i18n.text_length", textLength.Value);
        }

        return activity;
    }

    /// <summary>
    /// Starts an activity for batch translation
    /// </summary>
    public static Activity? StartTranslateBatchActivity(
        string? sourceLanguage = null,
        string? targetLanguage = null,
        int entryCount = 0)
    {
        var activity = ActivitySource.StartActivity("I18nAssistant.TranslateBatch", ActivityKind.Internal);

        if (activity != null)
        {
            if (sourceLanguage != null)
                activity.SetTag("mostlylucid.i18n.source_language", sourceLanguage);
            if (targetLanguage != null)
                activity.SetTag("mostlylucid.i18n.target_language", targetLanguage);
            activity.SetTag("mostlylucid.i18n.entry_count", entryCount);
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

        activity.SetTag("mostlylucid.i18n.success", result.Success);
        activity.SetTag("mostlylucid.i18n.source_language", result.SourceLanguage);
        activity.SetTag("mostlylucid.i18n.target_language", result.TargetLanguage);
        activity.SetTag("mostlylucid.i18n.duration_ms", result.Duration.TotalMilliseconds);

        // Statistics
        activity.SetTag("mostlylucid.i18n.total_entries", result.Statistics.TotalEntries);
        activity.SetTag("mostlylucid.i18n.translated_count", result.Statistics.TranslatedCount);
        activity.SetTag("mostlylucid.i18n.skipped_count", result.Statistics.SkippedCount);
        activity.SetTag("mostlylucid.i18n.failed_count", result.Statistics.FailedCount);
        activity.SetTag("mostlylucid.i18n.total_characters", result.Statistics.TotalCharactersTranslated);

        // Translation method breakdown
        if (result.Statistics.LlmOnlyCount > 0)
            activity.SetTag("mostlylucid.i18n.llm_only_count", result.Statistics.LlmOnlyCount);
        if (result.Statistics.NmtOnlyCount > 0)
            activity.SetTag("mostlylucid.i18n.nmt_only_count", result.Statistics.NmtOnlyCount);
        if (result.Statistics.NmtPlusLlmCount > 0)
            activity.SetTag("mostlylucid.i18n.nmt_plus_llm_count", result.Statistics.NmtPlusLlmCount);
        if (result.Statistics.RagLlmCount > 0)
            activity.SetTag("mostlylucid.i18n.rag_llm_count", result.Statistics.RagLlmCount);

        // Context usage
        if (result.Statistics.ContextEntriesUsed > 0)
            activity.SetTag("mostlylucid.i18n.context_entries_used", result.Statistics.ContextEntriesUsed);
        if (result.Statistics.GlossaryTermsUsed > 0)
            activity.SetTag("mostlylucid.i18n.glossary_terms_used", result.Statistics.GlossaryTermsUsed);

        // Error count
        if (result.Errors.Count > 0)
            activity.SetTag("mostlylucid.i18n.error_count", result.Errors.Count);

        activity.SetStatus(result.Success ? ActivityStatusCode.Ok : ActivityStatusCode.Error);
    }

    /// <summary>
    /// Records multi-language translation result on the activity
    /// </summary>
    public static void RecordMultiLanguageResult(Activity? activity, MultiLanguageTranslationResult result)
    {
        if (activity == null)
            return;

        activity.SetTag("mostlylucid.i18n.success", result.Success);
        activity.SetTag("mostlylucid.i18n.source_language", result.SourceLanguage);
        activity.SetTag("mostlylucid.i18n.total_languages", result.Results.Count);
        activity.SetTag("mostlylucid.i18n.successful_languages", result.SuccessfulLanguages.Count());
        activity.SetTag("mostlylucid.i18n.failed_languages", result.FailedLanguages.Count());
        activity.SetTag("mostlylucid.i18n.total_duration_ms", result.TotalDuration.TotalMilliseconds);

        // Aggregate statistics
        var totalTranslated = result.Results.Values.Sum(r => r.Statistics.TranslatedCount);
        var totalCharacters = result.Results.Values.Sum(r => r.Statistics.TotalCharactersTranslated);
        activity.SetTag("mostlylucid.i18n.total_entries_translated", totalTranslated);
        activity.SetTag("mostlylucid.i18n.total_characters", totalCharacters);

        activity.SetStatus(result.Success ? ActivityStatusCode.Ok : ActivityStatusCode.Error);
    }

    /// <summary>
    /// Records string translation result on the activity
    /// </summary>
    public static void RecordStringResult(Activity? activity, int translatedLength)
    {
        if (activity == null)
            return;

        activity.SetTag("mostlylucid.i18n.translated_length", translatedLength);
        activity.SetStatus(ActivityStatusCode.Ok);
    }

    /// <summary>
    /// Records batch translation result on the activity
    /// </summary>
    public static void RecordBatchResult(Activity? activity, int translatedCount, int totalCount)
    {
        if (activity == null)
            return;

        activity.SetTag("mostlylucid.i18n.translated_count", translatedCount);
        activity.SetTag("mostlylucid.i18n.total_count", totalCount);
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
