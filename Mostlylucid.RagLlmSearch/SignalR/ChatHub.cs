using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.Logging;
using Mostlylucid.RagLlmSearch.Models;
using ConversationModel = Mostlylucid.RagLlmSearch.Models.Conversation;

namespace Mostlylucid.RagLlmSearch.SignalR;

/// <summary>
///     SignalR hub for real-time chat streaming
/// </summary>
public class ChatHub : Hub<IChatHubClient>
{
    private readonly IChatService _chatService;
    private readonly ILogger<ChatHub> _logger;

    public ChatHub(IChatService chatService, ILogger<ChatHub> logger)
    {
        _chatService = chatService;
        _logger = logger;
    }

    /// <summary>
    ///     Sends a chat message and streams the response to the client
    /// </summary>
    /// <param name="request">The chat request</param>
    public async Task SendMessage(ChatRequest request)
    {
        _logger.LogDebug("Received chat request from connection {ConnectionId}", Context.ConnectionId);

        try
        {
            await foreach (var chunk in _chatService.ChatStreamAsync(request, Context.ConnectionAborted))
                await Clients.Caller.ReceiveChunk(chunk);
        }
        catch (OperationCanceledException)
        {
            _logger.LogDebug("Chat stream cancelled for connection {ConnectionId}", Context.ConnectionId);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error streaming chat response to connection {ConnectionId}", Context.ConnectionId);
            await Clients.Caller.ReceiveError(ex.Message);
        }
    }

    /// <summary>
    ///     Gets conversation history
    /// </summary>
    /// <param name="conversationId">The conversation ID</param>
    public async Task<ConversationModel?> GetConversation(string conversationId)
    {
        return await _chatService.GetConversationAsync(conversationId, Context.ConnectionAborted);
    }

    /// <summary>
    ///     Gets all conversations for the current user
    /// </summary>
    /// <param name="userId">The user ID</param>
    public async Task<List<ConversationModel>> GetUserConversations(string userId)
    {
        return await _chatService.GetUserConversationsAsync(userId, Context.ConnectionAborted);
    }

    /// <summary>
    ///     Deletes a conversation
    /// </summary>
    /// <param name="conversationId">The conversation ID</param>
    public async Task DeleteConversation(string conversationId)
    {
        await _chatService.DeleteConversationAsync(conversationId, Context.ConnectionAborted);
        await Clients.Caller.ConversationDeleted(conversationId);
    }

    /// <summary>
    ///     Performs a web search
    /// </summary>
    /// <param name="query">The search query</param>
    /// <param name="provider">Optional provider name</param>
    public async Task<SearchResponse> Search(string query, string? provider = null)
    {
        return await _chatService.SearchAsync(query, provider, Context.ConnectionAborted);
    }

    /// <summary>
    ///     Gets available search providers
    /// </summary>
    public IEnumerable<string> GetAvailableProviders()
    {
        return _chatService.GetAvailableProviders();
    }

    public override async Task OnConnectedAsync()
    {
        _logger.LogDebug("Client connected: {ConnectionId}", Context.ConnectionId);
        await base.OnConnectedAsync();
    }

    public override async Task OnDisconnectedAsync(Exception? exception)
    {
        _logger.LogDebug("Client disconnected: {ConnectionId}", Context.ConnectionId);
        await base.OnDisconnectedAsync(exception);
    }
}

/// <summary>
///     Client interface for the ChatHub
/// </summary>
public interface IChatHubClient
{
    /// <summary>
    ///     Receives a streaming chunk of the response
    /// </summary>
    Task ReceiveChunk(ChatStreamChunk chunk);

    /// <summary>
    ///     Receives an error message
    /// </summary>
    Task ReceiveError(string error);

    /// <summary>
    ///     Notifies that a conversation was deleted
    /// </summary>
    Task ConversationDeleted(string conversationId);
}