using System.Diagnostics;
using Mostlylucid.RagLlmSearch.Models;

namespace Mostlylucid.RagLlmSearch.Telemetry;

/// <summary>
/// Telemetry instrumentation for RAG search operations
/// </summary>
public static class RagSearchTelemetry
{
    /// <summary>
    /// Activity source name for RAG search
    /// </summary>
    public const string ActivitySourceName = "Mostlylucid.RagLlmSearch";

    /// <summary>
    /// Activity source for RAG search telemetry
    /// </summary>
    public static readonly ActivitySource ActivitySource = new(ActivitySourceName, GetVersion());

    private static string GetVersion()
    {
        return typeof(RagSearchTelemetry).Assembly.GetName().Version?.ToString() ?? "1.0.0";
    }

    #region Chat Operations

    /// <summary>
    /// Starts an activity for a chat operation
    /// </summary>
    public static Activity? StartChatActivity(string? conversationId = null, bool enableRag = false, bool enableWebSearch = false)
    {
        var activity = ActivitySource.StartActivity("RagSearch.Chat", ActivityKind.Internal);

        if (activity != null)
        {
            if (conversationId != null)
                activity.SetTag("mostlylucid.ragsearch.conversation_id", conversationId);
            activity.SetTag("mostlylucid.ragsearch.rag_enabled", enableRag);
            activity.SetTag("mostlylucid.ragsearch.web_search_enabled", enableWebSearch);
        }

        return activity;
    }

    /// <summary>
    /// Records chat result on the activity
    /// </summary>
    public static void RecordChatResult(
        Activity? activity,
        long responseTimeMs,
        int sourceCount,
        bool triggeredSearch,
        bool usedRag,
        string? searchProvider = null)
    {
        if (activity == null)
            return;

        activity.SetTag("mostlylucid.ragsearch.response_time_ms", responseTimeMs);
        activity.SetTag("mostlylucid.ragsearch.source_count", sourceCount);
        activity.SetTag("mostlylucid.ragsearch.triggered_search", triggeredSearch);
        activity.SetTag("mostlylucid.ragsearch.used_rag", usedRag);

        if (searchProvider != null)
            activity.SetTag("mostlylucid.ragsearch.search_provider", searchProvider);

        activity.SetStatus(ActivityStatusCode.Ok);
    }

    #endregion

    #region Web Search Operations

    /// <summary>
    /// Starts an activity for a web search operation
    /// </summary>
    public static Activity? StartSearchActivity(string query, string providerName, int maxResults = 5)
    {
        var activity = ActivitySource.StartActivity($"RagSearch.WebSearch.{providerName}", ActivityKind.Client);

        if (activity != null)
        {
            activity.SetTag("mostlylucid.ragsearch.query", query);
            activity.SetTag("mostlylucid.ragsearch.search_provider", providerName);
            activity.SetTag("mostlylucid.ragsearch.max_results", maxResults);
        }

        return activity;
    }

    /// <summary>
    /// Records web search result on the activity
    /// </summary>
    public static void RecordSearchResult(Activity? activity, SearchResponse response)
    {
        if (activity == null)
            return;

        activity.SetTag("mostlylucid.ragsearch.result_count", response.Results.Count);
        activity.SetTag("mostlylucid.ragsearch.total_results", response.TotalResults);
        activity.SetTag("mostlylucid.ragsearch.search_time_ms", response.SearchTimeMs);
        activity.SetTag("mostlylucid.ragsearch.search_success", response.Success);

        if (!string.IsNullOrEmpty(response.Error))
        {
            activity.SetTag("mostlylucid.ragsearch.error", response.Error);
            activity.SetStatus(ActivityStatusCode.Error, response.Error);
        }
        else
        {
            activity.SetStatus(ActivityStatusCode.Ok);
        }
    }

    #endregion

    #region RAG Operations

    /// <summary>
    /// Starts an activity for a RAG search operation
    /// </summary>
    public static Activity? StartRagSearchActivity(string query, int maxResults = 5, float minScore = 0.5f)
    {
        var activity = ActivitySource.StartActivity("RagSearch.RagSearch", ActivityKind.Internal);

        if (activity != null)
        {
            activity.SetTag("mostlylucid.ragsearch.query", query);
            activity.SetTag("mostlylucid.ragsearch.max_results", maxResults);
            activity.SetTag("mostlylucid.ragsearch.min_score", minScore);
        }

        return activity;
    }

    /// <summary>
    /// Records RAG search result on the activity
    /// </summary>
    public static void RecordRagSearchResult(Activity? activity, int resultCount, long searchTimeMs)
    {
        if (activity == null)
            return;

        activity.SetTag("mostlylucid.ragsearch.result_count", resultCount);
        activity.SetTag("mostlylucid.ragsearch.search_time_ms", searchTimeMs);
        activity.SetStatus(ActivityStatusCode.Ok);
    }

    /// <summary>
    /// Starts an activity for adding a document to RAG
    /// </summary>
    public static Activity? StartAddDocumentActivity(string documentId, string? documentType = null)
    {
        var activity = ActivitySource.StartActivity("RagSearch.AddDocument", ActivityKind.Internal);

        if (activity != null)
        {
            activity.SetTag("mostlylucid.ragsearch.document_id", documentId);
            if (documentType != null)
                activity.SetTag("mostlylucid.ragsearch.document_type", documentType);
        }

        return activity;
    }

    /// <summary>
    /// Records document added successfully
    /// </summary>
    public static void RecordDocumentAdded(Activity? activity)
    {
        if (activity == null)
            return;

        activity.SetStatus(ActivityStatusCode.Ok);
    }

    #endregion

    #region LLM Operations

    /// <summary>
    /// Starts an activity for LLM response generation
    /// </summary>
    public static Activity? StartLlmGenerateActivity(int messageCount, bool hasContext = false)
    {
        var activity = ActivitySource.StartActivity("RagSearch.LlmGenerate", ActivityKind.Client);

        if (activity != null)
        {
            activity.SetTag("mostlylucid.ragsearch.message_count", messageCount);
            activity.SetTag("mostlylucid.ragsearch.has_context", hasContext);
        }

        return activity;
    }

    /// <summary>
    /// Records LLM generation result
    /// </summary>
    public static void RecordLlmGenerateResult(Activity? activity, int responseLength, long generationTimeMs)
    {
        if (activity == null)
            return;

        activity.SetTag("mostlylucid.ragsearch.response_length", responseLength);
        activity.SetTag("mostlylucid.ragsearch.generation_time_ms", generationTimeMs);
        activity.SetStatus(ActivityStatusCode.Ok);
    }

    #endregion

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
