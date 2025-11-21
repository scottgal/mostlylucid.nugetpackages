using Microsoft.AspNetCore.SignalR;

namespace mostlylucid.llmslidetranslator.Demo.Hubs;

/// <summary>
///     SignalR hub for real-time translation updates
/// </summary>
public class TranslationHub : Hub
{
    private readonly ILogger<TranslationHub> _logger;

    public TranslationHub(ILogger<TranslationHub> logger)
    {
        _logger = logger;
    }

    /// <summary>
    ///     Subscribe to updates for a specific document
    /// </summary>
    public async Task SubscribeToDocument(string documentId)
    {
        await Groups.AddToGroupAsync(Context.ConnectionId, $"document_{documentId}");
        _logger.LogInformation("Client {ConnectionId} subscribed to document {DocumentId}",
            Context.ConnectionId, documentId);
    }

    /// <summary>
    ///     Unsubscribe from updates for a specific document
    /// </summary>
    public async Task UnsubscribeFromDocument(string documentId)
    {
        await Groups.RemoveFromGroupAsync(Context.ConnectionId, $"document_{documentId}");
        _logger.LogInformation("Client {ConnectionId} unsubscribed from document {DocumentId}",
            Context.ConnectionId, documentId);
    }

    public override async Task OnConnectedAsync()
    {
        _logger.LogInformation("Client connected: {ConnectionId}", Context.ConnectionId);
        await base.OnConnectedAsync();
    }

    public override async Task OnDisconnectedAsync(Exception? exception)
    {
        _logger.LogInformation("Client disconnected: {ConnectionId}", Context.ConnectionId);
        await base.OnDisconnectedAsync(exception);
    }
}