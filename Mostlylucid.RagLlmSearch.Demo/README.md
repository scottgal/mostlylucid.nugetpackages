# RAG LLM Search Demo

Demo application for the Mostlylucid.RagLlmSearch package, showcasing AI-powered chat with web search and RAG
capabilities.

## Prerequisites

1. **Ollama** - Install and run Ollama: https://ollama.ai/
2. **Required Models** - Pull the required models:
   ```bash
   ollama pull llama3.2
   ollama pull nomic-embed-text
   ```

## Running the Demo

```bash
cd Mostlylucid.RagLlmSearch.Demo
dotnet run
```

Then open http://localhost:5000 in your browser.

## Features

- **Chat Interface** - Modern chat UI with streaming responses
- **Web Search** - Automatic fact-checking using DuckDuckGo (or configure Brave, Tavily, SerpApi)
- **RAG Knowledge Base** - Add your own documents for context-aware responses
- **Conversation History** - Persistent conversations with full history
- **Source Citations** - See where information comes from

## Configuration

Edit `appsettings.json` to configure:

- Ollama endpoint and models
- Search provider API keys
- RAG settings

### Search Provider API Keys

To use search providers other than DuckDuckGo, add your API keys:

```json
{
  "SearchProviders": {
    "Brave": {
      "ApiKey": "YOUR_BRAVE_API_KEY"
    },
    "Tavily": {
      "ApiKey": "YOUR_TAVILY_API_KEY"
    },
    "SerpApi": {
      "ApiKey": "YOUR_SERPAPI_KEY"
    }
  }
}
```

## API Endpoints

- `POST /api/chat` - Send chat message
- `GET /api/chat/health` - Check system health
- `GET /api/chat/search?query=...` - Perform web search
- `POST /api/chat/rag` - Add document to RAG
- `GET /swagger` - API documentation

## SignalR Hub

Connect to `/chathub` for real-time streaming:

```javascript
const connection = new signalR.HubConnectionBuilder()
    .withUrl('/chathub')
    .build();

connection.on('ReceiveChunk', (chunk) => {
    // Handle streaming response
});

await connection.start();
await connection.invoke('SendMessage', { message: "Hello!" });
```
