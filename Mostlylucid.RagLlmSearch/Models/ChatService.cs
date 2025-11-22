using System.Runtime.CompilerServices;
using System.Text;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Mostlylucid.RagLlmSearch.Configuration;
using Mostlylucid.RagLlmSearch.Conversation;
using Mostlylucid.RagLlmSearch.LlmServices;
using Mostlylucid.RagLlmSearch.Rag;
using Mostlylucid.RagLlmSearch.SearchProviders;

namespace Mostlylucid.RagLlmSearch.Models;

/// <summary>
/// Interface for the main chat service
/// </summary>
public interface IChatService
{
    /// <summary>
    /// Sends a chat message and gets a response
    /// </summary>
    Task<ChatResponse> ChatAsync(ChatRequest request, CancellationToken cancellationToken = default);

    /// <summary>
    /// Sends a chat message and streams the response
    /// </summary>
    IAsyncEnumerable<ChatStreamChunk> ChatStreamAsync(ChatRequest request, CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets a conversation by ID
    /// </summary>
    Task<Conversation?> GetConversationAsync(string conversationId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets all conversations for a user
    /// </summary>
    Task<List<Conversation>> GetUserConversationsAsync(string userId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Deletes a conversation
    /// </summary>
    Task DeleteConversationAsync(string conversationId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Performs a search using the configured provider
    /// </summary>
    Task<SearchResponse> SearchAsync(string query, string? provider = null, CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets available search providers
    /// </summary>
    IEnumerable<string> GetAvailableProviders();
}

/// <summary>
/// Main chat service that orchestrates LLM, RAG, and search
/// </summary>
public class ChatService : IChatService
{
    private readonly ILlmService _llmService;
    private readonly IRagService _ragService;
    private readonly IConversationService _conversationService;
    private readonly ISearchProviderFactory _searchProviderFactory;
    private readonly RagLlmSearchOptions _options;
    private readonly ILogger<ChatService> _logger;

    public ChatService(
        ILlmService llmService,
        IRagService ragService,
        IConversationService conversationService,
        ISearchProviderFactory searchProviderFactory,
        IOptions<RagLlmSearchOptions> options,
        ILogger<ChatService> logger)
    {
        _llmService = llmService;
        _ragService = ragService;
        _conversationService = conversationService;
        _searchProviderFactory = searchProviderFactory;
        _options = options.Value;
        _logger = logger;
    }

    public async Task<ChatResponse> ChatAsync(ChatRequest request, CancellationToken cancellationToken = default)
    {
        var stopwatch = System.Diagnostics.Stopwatch.StartNew();
        var response = new ChatResponse();

        try
        {
            // Get or create conversation
            var conversation = await GetOrCreateConversationAsync(request, cancellationToken);
            response.ConversationId = conversation.Id;

            // Add user message
            var userMessage = new ChatMessage
            {
                Role = ChatRole.User,
                Content = request.Message,
                ConversationId = conversation.Id
            };
            await _conversationService.AddMessageAsync(conversation.Id, userMessage, cancellationToken);

            // Gather context
            var (context, sources, triggeredSearch, usedRag) = await GatherContextAsync(request, cancellationToken);

            // Get conversation history
            var messages = await _conversationService.GetMessagesAsync(conversation.Id, cancellationToken: cancellationToken);

            // Generate response
            var responseText = await _llmService.GenerateResponseAsync(messages, context, cancellationToken);

            // Create assistant message
            var assistantMessage = new ChatMessage
            {
                Role = ChatRole.Assistant,
                Content = responseText,
                ConversationId = conversation.Id,
                Sources = sources,
                TriggeredSearch = triggeredSearch,
                UsedRagContext = usedRag
            };

            await _conversationService.AddMessageAsync(conversation.Id, assistantMessage, cancellationToken);

            response.Message = assistantMessage;
            response.Sources = sources;
            response.IsComplete = true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error processing chat request");
            response.Error = ex.Message;
        }

        stopwatch.Stop();
        response.ResponseTimeMs = stopwatch.ElapsedMilliseconds;
        return response;
    }

    public async IAsyncEnumerable<ChatStreamChunk> ChatStreamAsync(
        ChatRequest request,
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        var responseBuilder = new StringBuilder();
        var sources = new List<SourceReference>();
        var triggeredSearch = false;
        var usedRag = false;
        Conversation? conversation = null;
        string messageId = Guid.NewGuid().ToString();

        try
        {
            // Get or create conversation
            conversation = await GetOrCreateConversationAsync(request, cancellationToken);

            // Add user message
            var userMessage = new ChatMessage
            {
                Role = ChatRole.User,
                Content = request.Message,
                ConversationId = conversation.Id
            };
            await _conversationService.AddMessageAsync(conversation.Id, userMessage, cancellationToken);

            // Gather context
            string? context;
            (context, sources, triggeredSearch, usedRag) = await GatherContextAsync(request, cancellationToken);

            // Get conversation history
            var messages = await _conversationService.GetMessagesAsync(conversation.Id, cancellationToken: cancellationToken);

            // Stream the response
            await foreach (var chunk in _llmService.GenerateStreamingResponseAsync(messages, context, cancellationToken))
            {
                responseBuilder.Append(chunk);
                yield return new ChatStreamChunk
                {
                    ConversationId = conversation.Id,
                    MessageId = messageId,
                    Content = chunk,
                    IsFinal = false
                };
            }

            // Save the complete assistant message
            var assistantMessage = new ChatMessage
            {
                Id = messageId,
                Role = ChatRole.Assistant,
                Content = responseBuilder.ToString(),
                ConversationId = conversation.Id,
                Sources = sources,
                TriggeredSearch = triggeredSearch,
                UsedRagContext = usedRag
            };
            await _conversationService.AddMessageAsync(conversation.Id, assistantMessage, cancellationToken);

            // Send final chunk with sources
            yield return new ChatStreamChunk
            {
                ConversationId = conversation.Id,
                MessageId = messageId,
                Content = string.Empty,
                IsFinal = true,
                Sources = sources
            };
        }
        finally
        {
            // Error handling - yield error chunk if needed
        }
    }

    public async Task<Conversation?> GetConversationAsync(string conversationId, CancellationToken cancellationToken = default)
    {
        return await _conversationService.GetConversationAsync(conversationId, cancellationToken);
    }

    public async Task<List<Conversation>> GetUserConversationsAsync(string userId, CancellationToken cancellationToken = default)
    {
        return await _conversationService.GetUserConversationsAsync(userId, cancellationToken: cancellationToken);
    }

    public async Task DeleteConversationAsync(string conversationId, CancellationToken cancellationToken = default)
    {
        await _conversationService.DeleteConversationAsync(conversationId, cancellationToken);
    }

    public async Task<SearchResponse> SearchAsync(string query, string? provider = null, CancellationToken cancellationToken = default)
    {
        var searchProvider = string.IsNullOrEmpty(provider)
            ? _searchProviderFactory.GetDefaultProvider()
            : _searchProviderFactory.GetProvider(provider) ?? _searchProviderFactory.GetDefaultProvider();

        return await searchProvider.SearchAsync(query, _options.MaxSearchResults, cancellationToken);
    }

    public IEnumerable<string> GetAvailableProviders()
    {
        return _searchProviderFactory.GetAvailableProviders().Select(p => p.Name);
    }

    private async Task<Conversation> GetOrCreateConversationAsync(ChatRequest request, CancellationToken cancellationToken)
    {
        if (!string.IsNullOrEmpty(request.ConversationId))
        {
            var existing = await _conversationService.GetConversationAsync(request.ConversationId, cancellationToken);
            if (existing != null) return existing;
        }

        return await _conversationService.CreateConversationAsync(request.UserId, cancellationToken: cancellationToken);
    }

    private async Task<(string? Context, List<SourceReference> Sources, bool TriggeredSearch, bool UsedRag)> GatherContextAsync(
        ChatRequest request,
        CancellationToken cancellationToken)
    {
        var sources = new List<SourceReference>();
        var contextParts = new List<string>();
        var triggeredSearch = false;
        var usedRag = false;

        // Check RAG first
        if (request.EnableRag)
        {
            var ragResults = await _ragService.SearchAsync(
                request.Message,
                _options.MaxRagContextItems,
                0.6f,
                cancellationToken);

            if (ragResults.Count > 0)
            {
                usedRag = true;
                _logger.LogDebug("Found {Count} RAG results", ragResults.Count);

                foreach (var result in ragResults)
                {
                    contextParts.Add($"[Stored Knowledge - Score: {result.Score:F2}]\n{result.Document.Content}");
                    if (!string.IsNullOrEmpty(result.Document.SourceUrl))
                    {
                        sources.Add(new SourceReference
                        {
                            Title = result.Document.Title,
                            Url = result.Document.SourceUrl,
                            Snippet = result.Document.Content.Length > 200
                                ? result.Document.Content[..200] + "..."
                                : result.Document.Content,
                            RelevanceScore = result.Score
                        });
                    }
                }
            }
        }

        // Web search if enabled and needed
        if (request.EnableWebSearch)
        {
            var shouldSearch = await _llmService.ShouldSearchWebAsync(request.Message, cancellationToken);

            if (shouldSearch)
            {
                triggeredSearch = true;
                var searchProvider = string.IsNullOrEmpty(request.SearchProvider)
                    ? _searchProviderFactory.GetDefaultProvider()
                    : _searchProviderFactory.GetProvider(request.SearchProvider) ?? _searchProviderFactory.GetDefaultProvider();

                var searchResponse = await searchProvider.SearchAsync(
                    request.Message,
                    _options.MaxSearchResults,
                    cancellationToken);

                if (searchResponse.Success)
                {
                    _logger.LogDebug("Web search returned {Count} results from {Provider}",
                        searchResponse.Results.Count, searchResponse.Provider);

                    foreach (var result in searchResponse.Results)
                    {
                        contextParts.Add($"[Web Search - {result.Title}]\n{result.Snippet}\nSource: {result.Url}");
                        sources.Add(new SourceReference
                        {
                            Title = result.Title,
                            Url = result.Url,
                            Snippet = result.Snippet,
                            RelevanceScore = result.Score
                        });

                        // Store search results in RAG for future use
                        await _ragService.AddDocumentAsync(new RagDocument
                        {
                            Title = result.Title,
                            Content = result.Snippet,
                            SourceUrl = result.Url,
                            DocumentType = "search_result",
                            Metadata = new Dictionary<string, string>
                            {
                                ["provider"] = searchResponse.Provider,
                                ["query"] = request.Message
                            }
                        }, cancellationToken);
                    }
                }
            }
        }

        var context = contextParts.Count > 0 ? string.Join("\n\n---\n\n", contextParts) : null;
        return (context, sources, triggeredSearch, usedRag);
    }
}
