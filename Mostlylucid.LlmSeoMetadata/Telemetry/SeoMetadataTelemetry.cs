using System.Diagnostics;
using Mostlylucid.LlmSeoMetadata.Models;

namespace Mostlylucid.LlmSeoMetadata.Telemetry;

/// <summary>
///     Telemetry instrumentation for SEO metadata generation operations
/// </summary>
public static class SeoMetadataTelemetry
{
    /// <summary>
    ///     Activity source name for SEO metadata generation
    /// </summary>
    public const string ActivitySourceName = "Mostlylucid.LlmSeoMetadata";

    /// <summary>
    ///     Activity source for SEO metadata telemetry
    /// </summary>
    public static readonly ActivitySource ActivitySource = new(ActivitySourceName, GetVersion());

    private static string GetVersion()
    {
        return typeof(SeoMetadataTelemetry).Assembly.GetName().Version?.ToString() ?? "1.0.0";
    }

    /// <summary>
    ///     Starts an activity for SEO metadata generation
    /// </summary>
    /// <param name="contentType">The type of content being processed</param>
    /// <param name="title">The title of the content</param>
    public static Activity? StartGenerateMetadataActivity(SeoContentType? contentType = null, string? title = null)
    {
        var activity = ActivitySource.StartActivity("SeoMetadata.GenerateMetadata");

        if (activity != null)
        {
            if (contentType.HasValue)
                activity.SetTag("mostlylucid.seometadata.content_type", contentType.Value.ToString());
            if (title != null)
                activity.SetTag("mostlylucid.seometadata.content_title", TruncateTitle(title));
        }

        return activity;
    }

    /// <summary>
    ///     Starts an activity for generating meta description
    /// </summary>
    /// <param name="contentType">The type of content being processed</param>
    public static Activity? StartGenerateMetaDescriptionActivity(SeoContentType? contentType = null)
    {
        var activity = ActivitySource.StartActivity("SeoMetadata.GenerateMetaDescription");

        if (activity != null && contentType.HasValue)
        {
            activity.SetTag("mostlylucid.seometadata.content_type", contentType.Value.ToString());
            activity.SetTag("mostlylucid.seometadata.metadata_type", "MetaDescription");
        }

        return activity;
    }

    /// <summary>
    ///     Starts an activity for generating OpenGraph metadata
    /// </summary>
    /// <param name="contentType">The type of content being processed</param>
    public static Activity? StartGenerateOpenGraphActivity(SeoContentType? contentType = null)
    {
        var activity = ActivitySource.StartActivity("SeoMetadata.GenerateOpenGraph");

        if (activity != null && contentType.HasValue)
        {
            activity.SetTag("mostlylucid.seometadata.content_type", contentType.Value.ToString());
            activity.SetTag("mostlylucid.seometadata.metadata_type", "OpenGraph");
        }

        return activity;
    }

    /// <summary>
    ///     Starts an activity for generating JSON-LD structured data
    /// </summary>
    /// <param name="contentType">The type of content being processed</param>
    public static Activity? StartGenerateJsonLdActivity(SeoContentType? contentType = null)
    {
        var activity = ActivitySource.StartActivity("SeoMetadata.GenerateJsonLd");

        if (activity != null && contentType.HasValue)
        {
            activity.SetTag("mostlylucid.seometadata.content_type", contentType.Value.ToString());
            activity.SetTag("mostlylucid.seometadata.metadata_type", "JsonLd");
        }

        return activity;
    }

    /// <summary>
    ///     Starts an activity for generating keywords
    /// </summary>
    /// <param name="contentType">The type of content being processed</param>
    /// <param name="maxKeywords">Maximum number of keywords requested</param>
    public static Activity? StartGenerateKeywordsActivity(SeoContentType? contentType = null, int? maxKeywords = null)
    {
        var activity = ActivitySource.StartActivity("SeoMetadata.GenerateKeywords");

        if (activity != null)
        {
            if (contentType.HasValue)
                activity.SetTag("mostlylucid.seometadata.content_type", contentType.Value.ToString());
            activity.SetTag("mostlylucid.seometadata.metadata_type", "Keywords");
            if (maxKeywords.HasValue)
                activity.SetTag("mostlylucid.seometadata.max_keywords", maxKeywords.Value);
        }

        return activity;
    }

    /// <summary>
    ///     Starts an activity for LLM API call
    /// </summary>
    /// <param name="model">The LLM model being used</param>
    public static Activity? StartLlmCallActivity(string? model = null)
    {
        var activity = ActivitySource.StartActivity("SeoMetadata.LlmCall", ActivityKind.Client);

        if (activity != null && model != null) activity.SetTag("mostlylucid.seometadata.llm_model", model);

        return activity;
    }

    /// <summary>
    ///     Records SEO metadata generation result on the activity
    /// </summary>
    /// <param name="activity">The activity to record on</param>
    /// <param name="response">The generation response</param>
    public static void RecordResult(Activity? activity, GenerationResponse response)
    {
        if (activity == null)
            return;

        activity.SetTag("mostlylucid.seometadata.success", response.Success);
        activity.SetTag("mostlylucid.seometadata.from_cache", response.FromCache);
        activity.SetTag("mostlylucid.seometadata.generation_time_ms", response.GenerationTimeMs);

        if (response.Metadata != null)
        {
            activity.SetTag("mostlylucid.seometadata.has_meta_description", response.Metadata.MetaDescription != null);
            activity.SetTag("mostlylucid.seometadata.has_open_graph", response.Metadata.OpenGraph != null);
            activity.SetTag("mostlylucid.seometadata.has_json_ld", response.Metadata.JsonLd != null);
            activity.SetTag("mostlylucid.seometadata.has_keywords", response.Metadata.Keywords?.Count > 0);
            activity.SetTag("mostlylucid.seometadata.keyword_count", response.Metadata.Keywords?.Count ?? 0);

            if (response.Metadata.GeneratedByModel != null)
                activity.SetTag("mostlylucid.seometadata.model", response.Metadata.GeneratedByModel);
        }

        if (!string.IsNullOrEmpty(response.CacheKey))
            activity.SetTag("mostlylucid.seometadata.cache_key", response.CacheKey);

        activity.SetStatus(response.Success ? ActivityStatusCode.Ok : ActivityStatusCode.Error);
    }

    /// <summary>
    ///     Records a cache hit on the activity
    /// </summary>
    /// <param name="activity">The activity to record on</param>
    /// <param name="cacheKey">The cache key that was hit</param>
    public static void RecordCacheHit(Activity? activity, string? cacheKey = null)
    {
        if (activity == null)
            return;

        activity.SetTag("mostlylucid.seometadata.cache_hit", true);
        if (cacheKey != null)
            activity.SetTag("mostlylucid.seometadata.cache_key", cacheKey);
    }

    /// <summary>
    ///     Records a cache miss on the activity
    /// </summary>
    /// <param name="activity">The activity to record on</param>
    public static void RecordCacheMiss(Activity? activity)
    {
        if (activity == null)
            return;

        activity.SetTag("mostlylucid.seometadata.cache_hit", false);
    }

    /// <summary>
    ///     Records generated keywords count
    /// </summary>
    /// <param name="activity">The activity to record on</param>
    /// <param name="keywordCount">Number of keywords generated</param>
    public static void RecordKeywordsGenerated(Activity? activity, int keywordCount)
    {
        if (activity == null)
            return;

        activity.SetTag("mostlylucid.seometadata.keyword_count", keywordCount);
        activity.SetStatus(ActivityStatusCode.Ok);
    }

    /// <summary>
    ///     Records an exception on the activity
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

    /// <summary>
    ///     Records a timeout on the activity
    /// </summary>
    /// <param name="activity">The activity to record on</param>
    /// <param name="timeoutSeconds">The timeout value in seconds</param>
    public static void RecordTimeout(Activity? activity, int timeoutSeconds)
    {
        if (activity == null)
            return;

        activity.SetStatus(ActivityStatusCode.Error, "LLM request timed out");
        activity.SetTag("mostlylucid.seometadata.timeout", true);
        activity.SetTag("mostlylucid.seometadata.timeout_seconds", timeoutSeconds);
    }

    private static string TruncateTitle(string title, int maxLength = 100)
    {
        if (string.IsNullOrEmpty(title))
            return string.Empty;

        return title.Length <= maxLength ? title : title[..(maxLength - 3)] + "...";
    }
}