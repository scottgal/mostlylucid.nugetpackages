# Mostlylucid.RagLlmSearch

> **Note**: These packages are provided as-is. I'll get them working good enough to release but I can't commit to
> support. However they are Unlicense so have at it!

A lightweight RAG (Retrieval Augmented Generation) enabled LLM package with multiple search provider support,
conversation history, and real-time streaming via SignalR for ASP.NET Core applications.

## Table of Contents

- [Features](#features)
- [Installation](#installation)
- [Quick Start](#quick-start)
- [Configuration](#configuration)
- [Search Providers](#search-providers)
- [API Reference](#api-reference)
- [SignalR Hub](#signalr-hub)
- [RAG System](#rag-system)
- [Demo Application](#demo-application)

## Features

- **Multiple Search Providers**: DuckDuckGo (free), Brave Search, Tavily, and SerpApi
- **Ollama LLM Integration**: Local LLM support with streaming responses
- **RAG (Retrieval Augmented Generation)**: SQLite-based vector storage with cosine similarity search
- **Conversation History**: Persistent conversation storage with full history
- **SignalR Streaming**: Real-time response streaming for responsive UIs
- **Fact Checking**: Automatic web search for queries requiring current information
- **Source Citations**: All responses include source references when using search
- **Easy Integration**: Simple extension methods for ASP.NET Core

## Installation

```bash
dotnet add package mostlylucid.ragllmsearch
```

### Prerequisites

- .NET 8.0 or .NET 9.0
- [Ollama](https://ollama.ai/) running locally (default: http://localhost:11434)
- Required Ollama models:
  ```bash
  ollama pull llama3.2
  ollama pull nomic-embed-text
  ```

## Quick Start

### 1. Add Services

```csharp
using Mostlylucid.RagLlmSearch.Extensions;

var builder = WebApplication.CreateBuilder(args);

// Add RAG LLM Search services
builder.Services.AddRagLlmSearch(
    options =>
    {
        options.OllamaEndpoint = "http://localhost:11434";
        options.ChatModel = "llama3.2";
        options.EmbeddingModel = "nomic-embed-text";
        options.DatabasePath = "ragllmsearch.db";
    },
    searchProviders =>
    {
        // DuckDuckGo works without API key
        searchProviders.DefaultProvider = SearchProviderType.DuckDuckGo;

        // Optional: Configure other providers
        searchProviders.Brave.ApiKey = "your-brave-api-key";
        searchProviders.Tavily.ApiKey = "your-tavily-api-key";
    });

var app = builder.Build();

// Initialize databases
await app.InitializeRagLlmSearchAsync();

// Map SignalR hub
app.MapChatHub("/chathub");

app.Run();
```

### 2. Use the Chat Service

```csharp
public class MyController : ControllerBase
{
    private readonly IChatService _chatService;

    public MyController(IChatService chatService)
    {
        _chatService = chatService;
    }

    [HttpPost("chat")]
    public async Task<ChatResponse> Chat(ChatRequest request)
    {
        return await _chatService.ChatAsync(request);
    }
}
```

### 3. Connect via SignalR (JavaScript)

```javascript
const connection = new signalR.HubConnectionBuilder()
    .withUrl('/chathub')
    .build();

connection.on('ReceiveChunk', (chunk) => {
    console.log(chunk.content);
    if (chunk.isFinal) {
        console.log('Sources:', chunk.sources);
    }
});

await connection.start();

// Send a message
await connection.invoke('SendMessage', {
    message: "What's the weather like today?",
    enableWebSearch: true,
    enableRag: true
});
```

## Configuration

### appsettings.json

```json
{
  "RagLlmSearch": {
    "OllamaEndpoint": "http://localhost:11434",
    "ChatModel": "llama3.2",
    "EmbeddingModel": "nomic-embed-text",
    "DatabasePath": "ragllmsearch.db",
    "MaxSearchResults": 5,
    "MaxRagContextItems": 3,
    "EnableFactChecking": true,
    "Temperature": 0.7,
    "MaxTokens": 2048,
    "SearchCacheMinutes": 30,
    "SystemPrompt": "You are a helpful AI assistant..."
  },
  "SearchProviders": {
    "DefaultProvider": "DuckDuckGo",
    "DuckDuckGo": {
      "SafeSearch": true,
      "Region": "us-en"
    },
    "Brave": {
      "ApiKey": null,
      "SafeSearch": true,
      "Country": "US"
    },
    "Tavily": {
      "ApiKey": null,
      "SearchDepth": "basic",
      "IncludeAnswer": true
    },
    "SerpApi": {
      "ApiKey": null,
      "Engine": "google",
      "Location": "United States"
    }
  }
}
```

## Search Providers

| Provider       | Free Tier  | API Key Required | Notes                                    |
|----------------|------------|------------------|------------------------------------------|
| **DuckDuckGo** | Unlimited  | No               | Instant Answer API, best for quick facts |
| **Brave**      | 2000/month | Yes              | Full web search, good results            |
| **Tavily**     | Limited    | Yes              | AI-optimized, includes summaries         |
| **SerpApi**    | 100/month  | Yes              | Google results, comprehensive            |

### Getting API Keys

- **Brave Search**: https://brave.com/search/api/
- **Tavily**: https://tavily.com/
- **SerpApi**: https://serpapi.com/

## API Reference

### IChatService

```csharp
public interface IChatService
{
    // Send a message and get a complete response
    Task<ChatResponse> ChatAsync(ChatRequest request, CancellationToken ct = default);

    // Stream response chunks
    IAsyncEnumerable<ChatStreamChunk> ChatStreamAsync(ChatRequest request, CancellationToken ct = default);

    // Get conversation by ID
    Task<Conversation?> GetConversationAsync(string conversationId, CancellationToken ct = default);

    // Get user's conversations
    Task<List<Conversation>> GetUserConversationsAsync(string userId, CancellationToken ct = default);

    // Delete a conversation
    Task DeleteConversationAsync(string conversationId, CancellationToken ct = default);

    // Perform a web search
    Task<SearchResponse> SearchAsync(string query, string? provider = null, CancellationToken ct = default);

    // Get available search providers
    IEnumerable<string> GetAvailableProviders();
}
```

### ChatRequest

```csharp
public class ChatRequest
{
    public string? ConversationId { get; set; }  // null for new conversation
    public string Message { get; set; }          // User's message
    public string? UserId { get; set; }          // Optional user ID
    public bool EnableWebSearch { get; set; }    // Search web for context
    public bool EnableRag { get; set; }          // Use RAG context
    public string? SearchProvider { get; set; }  // Override default provider
    public bool Stream { get; set; }             // Stream response
}
```

## SignalR Hub

### Hub Methods

| Method                  | Parameters                       | Returns               | Description                    |
|-------------------------|----------------------------------|-----------------------|--------------------------------|
| `SendMessage`           | `ChatRequest`                    | -                     | Stream a chat response         |
| `GetConversation`       | `string conversationId`          | `Conversation`        | Get conversation with messages |
| `GetUserConversations`  | `string userId`                  | `List<Conversation>`  | List user's conversations      |
| `DeleteConversation`    | `string conversationId`          | -                     | Delete a conversation          |
| `Search`                | `string query, string? provider` | `SearchResponse`      | Perform web search             |
| `GetAvailableProviders` | -                                | `IEnumerable<string>` | List available providers       |

### Client Events

| Event                 | Payload                 | Description              |
|-----------------------|-------------------------|--------------------------|
| `ReceiveChunk`        | `ChatStreamChunk`       | Streaming response chunk |
| `ReceiveError`        | `string error`          | Error message            |
| `ConversationDeleted` | `string conversationId` | Conversation was deleted |

## RAG System

The RAG system stores documents with embeddings for semantic search.

### IRagService

```csharp
public interface IRagService
{
    Task AddDocumentAsync(RagDocument document, CancellationToken ct = default);
    Task<List<RagSearchResult>> SearchAsync(string query, int maxResults = 5, float minScore = 0.5f, CancellationToken ct = default);
    Task<RagDocument?> GetDocumentAsync(string id, CancellationToken ct = default);
    Task DeleteDocumentAsync(string id, CancellationToken ct = default);
    Task ClearAsync(CancellationToken ct = default);
    Task<int> GetDocumentCountAsync(CancellationToken ct = default);
}
```

### Adding Documents

```csharp
var ragService = serviceProvider.GetRequiredService<IRagService>();

await ragService.AddDocumentAsync(new RagDocument
{
    Title = "Company Policy",
    Content = "Our company policy states that...",
    SourceUrl = "https://example.com/policy",
    DocumentType = "policy"
});
```

### How It Works

1. **User sends a message** via REST API or SignalR
2. **RAG search** finds relevant stored documents
3. **LLM determines** if web search is needed for current information
4. **Web search** (if needed) retrieves and stores results
5. **Context is built** from RAG and search results
6. **LLM generates** response with source citations
7. **Response streams** back to client in real-time

## Demo Application

The demo includes a full web UI with:

- Real-time chat with streaming responses
- Conversation history management
- Search provider selection
- RAG document management
- Source citation display

### Running the Demo

```bash
cd Mostlylucid.RagLlmSearch.Demo
dotnet run
```

Open http://localhost:5000 in your browser.

### Demo Endpoints

| Endpoint                       | Method  | Description        |
|--------------------------------|---------|--------------------|
| `/api/chat`                    | POST    | Send chat message  |
| `/api/chat/conversations/{id}` | GET     | Get conversation   |
| `/api/chat/search`             | GET     | Perform web search |
| `/api/chat/providers`          | GET     | List providers     |
| `/api/chat/health`             | GET     | Health check       |
| `/api/chat/rag`                | POST    | Add RAG document   |
| `/api/chat/rag/search`         | GET     | Search RAG store   |
| `/chathub`                     | SignalR | Real-time chat hub |

## License

This project is released under the Unlicense. See LICENSE for details.

## Source

[View source on GitHub](https://github.com/scottgal/mostlylucid.nugetpackages/tree/main/Mostlylucid.RagLlmSearch)
