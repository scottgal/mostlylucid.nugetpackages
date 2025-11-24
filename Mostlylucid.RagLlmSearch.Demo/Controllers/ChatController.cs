using Microsoft.AspNetCore.Mvc;
using Mostlylucid.RagLlmSearch.LlmServices;
using Mostlylucid.RagLlmSearch.Models;
using Mostlylucid.RagLlmSearch.Rag;
using ConversationModel = Mostlylucid.RagLlmSearch.Models.Conversation;

namespace Mostlylucid.RagLlmSearch.Demo.Controllers;

/// <summary>
///     REST API controller for chat operations
/// </summary>
[ApiController]
[Route("api/[controller]")]
public class ChatController : ControllerBase
{
    private readonly IChatService _chatService;
    private readonly ILlmService _llmService;
    private readonly IRagService _ragService;

    public ChatController(
        IChatService chatService,
        ILlmService llmService,
        IRagService ragService)
    {
        _chatService = chatService;
        _llmService = llmService;
        _ragService = ragService;
    }

    /// <summary>
    ///     Sends a chat message and receives a response
    /// </summary>
    [HttpPost]
    [ProducesResponseType(typeof(ChatResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<ChatResponse>> Chat([FromBody] ChatRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.Message)) return BadRequest("Message is required");

        var response = await _chatService.ChatAsync(request);
        return Ok(response);
    }

    /// <summary>
    ///     Gets a conversation by ID
    /// </summary>
    [HttpGet("conversations/{conversationId}")]
    [ProducesResponseType(typeof(ConversationModel), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<ConversationModel>> GetConversation(string conversationId)
    {
        var conversation = await _chatService.GetConversationAsync(conversationId);
        if (conversation == null) return NotFound();
        return Ok(conversation);
    }

    /// <summary>
    ///     Gets all conversations for a user
    /// </summary>
    [HttpGet("conversations/user/{userId}")]
    [ProducesResponseType(typeof(List<ConversationModel>), StatusCodes.Status200OK)]
    public async Task<ActionResult<List<ConversationModel>>> GetUserConversations(string userId)
    {
        var conversations = await _chatService.GetUserConversationsAsync(userId);
        return Ok(conversations);
    }

    /// <summary>
    ///     Deletes a conversation
    /// </summary>
    [HttpDelete("conversations/{conversationId}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    public async Task<IActionResult> DeleteConversation(string conversationId)
    {
        await _chatService.DeleteConversationAsync(conversationId);
        return NoContent();
    }

    /// <summary>
    ///     Performs a web search
    /// </summary>
    [HttpGet("search")]
    [ProducesResponseType(typeof(SearchResponse), StatusCodes.Status200OK)]
    public async Task<ActionResult<SearchResponse>> Search([FromQuery] string query,
        [FromQuery] string? provider = null)
    {
        var response = await _chatService.SearchAsync(query, provider);
        return Ok(response);
    }

    /// <summary>
    ///     Gets available search providers
    /// </summary>
    [HttpGet("providers")]
    [ProducesResponseType(typeof(IEnumerable<string>), StatusCodes.Status200OK)]
    public ActionResult<IEnumerable<string>> GetProviders()
    {
        return Ok(_chatService.GetAvailableProviders());
    }

    /// <summary>
    ///     Checks if the LLM service is available
    /// </summary>
    [HttpGet("health")]
    [ProducesResponseType(typeof(HealthResponse), StatusCodes.Status200OK)]
    public async Task<ActionResult<HealthResponse>> HealthCheck()
    {
        var llmAvailable = await _llmService.IsAvailableAsync();
        var ragCount = await _ragService.GetDocumentCountAsync();

        return Ok(new HealthResponse
        {
            LlmAvailable = llmAvailable,
            RagDocumentCount = ragCount,
            AvailableProviders = _chatService.GetAvailableProviders().ToList()
        });
    }

    /// <summary>
    ///     Adds a document to the RAG store
    /// </summary>
    [HttpPost("rag")]
    [ProducesResponseType(StatusCodes.Status201Created)]
    public async Task<IActionResult> AddRagDocument([FromBody] RagDocumentRequest request)
    {
        var document = new RagDocument
        {
            Title = request.Title,
            Content = request.Content,
            SourceUrl = request.SourceUrl,
            DocumentType = request.DocumentType ?? "user_input"
        };

        await _ragService.AddDocumentAsync(document);
        return CreatedAtAction(nameof(GetRagDocument), new { id = document.Id }, document);
    }

    /// <summary>
    ///     Gets a RAG document by ID
    /// </summary>
    [HttpGet("rag/{id}")]
    [ProducesResponseType(typeof(RagDocument), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<RagDocument>> GetRagDocument(string id)
    {
        var document = await _ragService.GetDocumentAsync(id);
        if (document == null) return NotFound();
        return Ok(document);
    }

    /// <summary>
    ///     Searches the RAG store
    /// </summary>
    [HttpGet("rag/search")]
    [ProducesResponseType(typeof(List<RagSearchResult>), StatusCodes.Status200OK)]
    public async Task<ActionResult<List<RagSearchResult>>> SearchRag(
        [FromQuery] string query,
        [FromQuery] int maxResults = 5,
        [FromQuery] float minScore = 0.5f)
    {
        var results = await _ragService.SearchAsync(query, maxResults, minScore);
        return Ok(results);
    }
}

public class HealthResponse
{
    public bool LlmAvailable { get; set; }
    public int RagDocumentCount { get; set; }
    public List<string> AvailableProviders { get; set; } = new();
}

public class RagDocumentRequest
{
    public string Title { get; set; } = string.Empty;
    public string Content { get; set; } = string.Empty;
    public string? SourceUrl { get; set; }
    public string? DocumentType { get; set; }
}