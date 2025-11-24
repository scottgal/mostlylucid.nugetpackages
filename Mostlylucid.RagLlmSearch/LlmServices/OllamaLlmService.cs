using System.Runtime.CompilerServices;
using System.Text;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Mostlylucid.RagLlmSearch.Configuration;
using Mostlylucid.RagLlmSearch.Models;
using OllamaSharp;
using OllamaSharp.Models;
using OllamaSharp.Models.Chat;
using ChatRole = OllamaSharp.Models.Chat.ChatRole;

namespace Mostlylucid.RagLlmSearch.LlmServices;

/// <summary>
///     Ollama-based LLM service implementation
/// </summary>
public class OllamaLlmService : ILlmService
{
    private readonly OllamaApiClient _client;
    private readonly ILogger<OllamaLlmService> _logger;
    private readonly RagLlmSearchOptions _options;

    public OllamaLlmService(
        IOptions<RagLlmSearchOptions> options,
        ILogger<OllamaLlmService> logger)
    {
        _options = options.Value;
        _logger = logger;
        _client = new OllamaApiClient(new Uri(_options.OllamaEndpoint));
    }

    public async Task<string> GenerateResponseAsync(
        IEnumerable<ChatMessage> messages,
        string? context = null,
        CancellationToken cancellationToken = default)
    {
        var response = new StringBuilder();

        await foreach (var chunk in GenerateStreamingResponseAsync(messages, context, cancellationToken))
            response.Append(chunk);

        return response.ToString();
    }

    public async IAsyncEnumerable<string> GenerateStreamingResponseAsync(
        IEnumerable<ChatMessage> messages,
        string? context = null,
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        var ollamaMessages = BuildOllamaMessages(messages, context);

        _logger.LogDebug("Generating response with {Count} messages using model {Model}",
            ollamaMessages.Count, _options.ChatModel);

        var chat = new Chat(_client)
        {
            Model = _options.ChatModel,
            Options = new RequestOptions
            {
                Temperature = _options.Temperature,
                NumPredict = _options.MaxTokens
            }
        };

        // Add all messages to the chat
        foreach (var msg in ollamaMessages.Take(ollamaMessages.Count - 1))
            // Consume the async enumerable to ensure the message is sent
        await foreach (var _ in chat.SendAsync(msg.Content ?? string.Empty, cancellationToken))
        {
            // Just consume the stream
        }

        // Stream the response for the last message
        var lastMessage = ollamaMessages.LastOrDefault();
        if (lastMessage != null)
            await foreach (var chunk in chat.SendAsync(lastMessage.Content ?? string.Empty, cancellationToken))
                if (!string.IsNullOrEmpty(chunk))
                    yield return chunk;
    }

    public async Task<float[]> GenerateEmbeddingAsync(string text, CancellationToken cancellationToken = default)
    {
        _logger.LogDebug("Generating embedding for text of length {Length}", text.Length);

        try
        {
            var response = await _client.EmbedAsync(new EmbedRequest
            {
                Model = _options.EmbeddingModel,
                Input = new List<string> { text }
            }, cancellationToken);

            if (response.Embeddings != null && response.Embeddings.Any())
            {
                var embedding = response.Embeddings.First();
                return embedding.Select(d => d).ToArray();
            }

            _logger.LogWarning("No embeddings returned from Ollama");
            return Array.Empty<float>();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error generating embedding");
            return Array.Empty<float>();
        }
    }

    public async Task<bool> IsAvailableAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            var models = await _client.ListLocalModelsAsync(cancellationToken);
            var modelsList = models.ToList();
            var hasModels = modelsList.Any();

            if (hasModels) _logger.LogDebug("Ollama is available with {Count} models", modelsList.Count);

            return hasModels;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Ollama is not available at {Endpoint}", _options.OllamaEndpoint);
            return false;
        }
    }

    public async Task<bool> ShouldSearchWebAsync(string query, CancellationToken cancellationToken = default)
    {
        // Use the LLM to determine if web search is needed
        var prompt = $"""
                      Analyze this query and determine if it would benefit from a web search to get accurate, up-to-date information.

                      Query: "{query}"

                      Respond with only "YES" if the query:
                      - Asks about current events, news, or recent developments
                      - Requires factual information that could change over time
                      - Asks about specific statistics, prices, or data
                      - Requests information about real people, places, or organizations
                      - Would benefit from citations or sources

                      Respond with only "NO" if the query:
                      - Is a general knowledge question you can answer confidently
                      - Is asking for creative content, opinions, or advice
                      - Is a coding or technical question about established concepts
                      - Is conversational or asking about your capabilities

                      Response (YES or NO only):
                      """;

        try
        {
            var responses = new List<GenerateResponseStream?>();
            await foreach (var chunk in _client.GenerateAsync(new GenerateRequest
                           {
                               Model = _options.ChatModel,
                               Prompt = prompt,
                               Options = new RequestOptions
                               {
                                   Temperature = 0.1f,
                                   NumPredict = 10
                               }
                           }, cancellationToken))
                responses.Add(chunk);

            var result = string.Join("", responses.Where(r => r != null).Select(r => r!.Response)).Trim()
                .ToUpperInvariant();
            return result.Contains("YES");
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Error determining if web search is needed, defaulting to yes");
            return true; // Default to searching if we can't determine
        }
    }

    private List<Message> BuildOllamaMessages(IEnumerable<ChatMessage> messages, string? context)
    {
        var ollamaMessages = new List<Message>();

        // Add system message with context if provided
        var systemPrompt = _options.SystemPrompt;
        if (!string.IsNullOrEmpty(context))
            systemPrompt +=
                $"\n\n## Search Context\nUse the following search results to inform your response:\n\n{context}";

        ollamaMessages.Add(new Message(ChatRole.System, systemPrompt));

        // Add conversation history
        foreach (var msg in messages)
        {
            var role = msg.Role switch
            {
                Models.ChatRole.User => ChatRole.User,
                Models.ChatRole.Assistant => ChatRole.Assistant,
                Models.ChatRole.System => ChatRole.System,
                _ => ChatRole.User
            };

            ollamaMessages.Add(new Message(role, msg.Content));
        }

        return ollamaMessages;
    }
}