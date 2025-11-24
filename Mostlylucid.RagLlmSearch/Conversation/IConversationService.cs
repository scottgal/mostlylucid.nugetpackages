using Mostlylucid.RagLlmSearch.Models;

namespace Mostlylucid.RagLlmSearch.Conversation;

/// <summary>
///     Interface for conversation history management
/// </summary>
public interface IConversationService
{
    /// <summary>
    ///     Creates a new conversation
    /// </summary>
    /// <param name="userId">Optional user identifier</param>
    /// <param name="title">Optional conversation title</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>The created conversation</returns>
    Task<Models.Conversation> CreateConversationAsync(
        string? userId = null,
        string? title = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    ///     Gets a conversation by ID
    /// </summary>
    /// <param name="conversationId">Conversation ID</param>
    /// <param name="cancellationToken">Cancellation token</param>
    Task<Models.Conversation?> GetConversationAsync(
        string conversationId,
        CancellationToken cancellationToken = default);

    /// <summary>
    ///     Gets all conversations for a user
    /// </summary>
    /// <param name="userId">User identifier</param>
    /// <param name="includeInactive">Include inactive conversations</param>
    /// <param name="cancellationToken">Cancellation token</param>
    Task<List<Models.Conversation>> GetUserConversationsAsync(
        string userId,
        bool includeInactive = false,
        CancellationToken cancellationToken = default);

    /// <summary>
    ///     Adds a message to a conversation
    /// </summary>
    /// <param name="conversationId">Conversation ID</param>
    /// <param name="message">Message to add</param>
    /// <param name="cancellationToken">Cancellation token</param>
    Task<ChatMessage> AddMessageAsync(
        string conversationId,
        ChatMessage message,
        CancellationToken cancellationToken = default);

    /// <summary>
    ///     Gets messages for a conversation
    /// </summary>
    /// <param name="conversationId">Conversation ID</param>
    /// <param name="limit">Maximum messages to return</param>
    /// <param name="cancellationToken">Cancellation token</param>
    Task<List<ChatMessage>> GetMessagesAsync(
        string conversationId,
        int? limit = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    ///     Updates conversation metadata
    /// </summary>
    /// <param name="conversationId">Conversation ID</param>
    /// <param name="title">New title</param>
    /// <param name="isActive">Active status</param>
    /// <param name="cancellationToken">Cancellation token</param>
    Task UpdateConversationAsync(
        string conversationId,
        string? title = null,
        bool? isActive = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    ///     Deletes a conversation and all its messages
    /// </summary>
    /// <param name="conversationId">Conversation ID</param>
    /// <param name="cancellationToken">Cancellation token</param>
    Task DeleteConversationAsync(
        string conversationId,
        CancellationToken cancellationToken = default);

    /// <summary>
    ///     Initializes the conversation store
    /// </summary>
    /// <param name="cancellationToken">Cancellation token</param>
    Task InitializeAsync(CancellationToken cancellationToken = default);
}