# mostlylucid.llmslidetranslator

> **Note**: These packages are provided as-is. I'll get them working good enough to release but I can't commit to support. However they are Unlicense so have at it!

RAG-assisted translation for long documents using small local LLMs with sliding-window chunking and vector similarity
search.

## Features

- **RAG-Enhanced Translation**: Uses retrieval-augmented generation to maintain translation consistency across long
  documents
- **Multiple Translation Methods**:
    - RAG + LLM (recommended)
    - LLM only
    - NMT baseline + LLM post-editing
    - NMT only
- **Sliding Window Context**: Always includes the previous translated block for continuity
- **Vector Similarity Search**: Retrieves similar earlier blocks to maintain terminology consistency
- **Multiple Vector Store Providers**:
    - File-based (default)
    - Qdrant vector database
- **Markdown-Aware Chunking**: Intelligently chunks markdown while preserving structure
- **Real-time Progress Tracking**: SignalR support for streaming translation updates
- **Translation Comparison**: Compare different translation methods side-by-side

## Installation

```bash
dotnet add package mostlylucid.llmslidetranslator
```

## Quick Start

### Basic Usage

```csharp
using mostlylucid.llmslidetranslator.Extensions;
using mostlylucid.llmslidetranslator.Services;
using mostlylucid.llmslidetranslator.Models;

// Configure services
builder.Services.AddLlmSlideTranslator(builder.Configuration);

// Use the translator
var translator = serviceProvider.GetRequiredService<ILlmSlideTranslator>();

var result = await translator.TranslateAsync(
    markdown: "# My Document\n\nThis is a long technical document...",
    documentId: "doc_001",
    sourceLanguage: "en",
    targetLanguage: "de",
    method: TranslationMethod.RagLlm
);

Console.WriteLine(result.GetTranslatedText());
```

### Configuration

Add to `appsettings.json`:

```json
{
  "LlmSlideTranslator": {
    "Ollama": {
      "Endpoint": "http://localhost:11434",
      "Model": "llama3.2:3b",
      "Temperature": 0.3,
      "MaxTokens": 2048,
      "TimeoutSeconds": 120
    },
    "Nmt": {
      "Enabled": true,
      "ServiceEndpoints": ["http://localhost:24080"],
      "UseAsBaseline": true
    },
    "Embedding": {
      "Provider": "Ollama",
      "OllamaModel": "nomic-embed-text:latest",
      "ModelPath": "./models/nomic-embed-text-v1.5.Q8_0.gguf",
      "Dimension": 768,
      "ContextSize": 2048
    },
    "Rag": {
      "TopK": 3,
      "UseSlidingWindow": true,
      "MinSimilarity": 0.5,
      "MaxContextBlocks": 5
    },
    "VectorStoreProvider": "File",
    "DataPath": "./data"
  }
}
```

### Embedding Providers

The package supports two embedding providers:

**Ollama (Recommended)** - Uses Ollama's embedding API:
```json
{
  "Embedding": {
    "Provider": "Ollama",
    "OllamaModel": "nomic-embed-text:latest"
  }
}
```

**Local LlamaSharp** - Uses a local GGUF model file:
```json
{
  "Embedding": {
    "Provider": "Local",
    "ModelPath": "./models/nomic-embed-text-v1.5.Q8_0.gguf"
  }
}
```

### Using Qdrant

To use Qdrant instead of file-based storage:

```json
{
  "LlmSlideTranslator": {
    "VectorStoreProvider": "Qdrant",
    "Qdrant": {
      "Endpoint": "http://localhost:6333",
      "CollectionName": "translations"
    }
  }
}
```

### Translation Methods

```csharp
// RAG-enhanced LLM (best quality, maintains consistency)
await translator.TranslateAsync(..., method: TranslationMethod.RagLlm);

// NMT baseline + LLM post-editing (good balance)
await translator.TranslateAsync(..., method: TranslationMethod.NmtPlusLlm);

// LLM only (creative, but may drift)
await translator.TranslateAsync(..., method: TranslationMethod.LlmOnly);

// NMT only (fast, baseline quality)
await translator.TranslateAsync(..., method: TranslationMethod.NmtOnly);
```

### Comparing Translations

```csharp
var comparer = serviceProvider.GetRequiredService<ITranslationComparer>();

var comparison = await comparer.CompareAsync(result1, result2);

Console.WriteLine($"Overall similarity: {comparison.SimilarityScore:P}");

foreach (var diff in comparison.Differences)
{
    Console.WriteLine($"Block {diff.BlockIndex}: {diff.Similarity:P} similar");
}
```

## Requirements

- **.NET 8.0** or **.NET 9.0**
- **Ollama** running locally with:
  - A translation model (e.g., `llama3.2:3b`, `qwen2.5:3b`)
  - An embedding model (e.g., `nomic-embed-text:latest`) - if using Ollama embeddings
- **Optional**: Local GGUF embedding model (if using Local embedding provider)
- **Optional**: NMT service from [mostlyucid-nmt](https://github.com/scottgal/mostlyucid-nmt) for baseline translations
- **Optional**: Qdrant vector database for scalable vector storage

## How It Works

1. **Chunking**: Markdown is split into translatable blocks (paragraphs, headings, lists)
2. **Embedding**: Each block is converted to a vector embedding
3. **Storage**: Blocks and embeddings are stored in a vector database
4. **Translation Loop**: For each block:
    - Generate embedding for current block
    - Retrieve most similar earlier blocks (RAG)
    - Include previous block (sliding window)
    - Build context-aware prompt
    - Translate with LLM
5. **Output**: Aligned block-by-block translation

## Demo API

See the `mostlylucid.llmslidetranslator.Demo` project for a complete example with:

- ASP.NET Minimal API endpoints
- SignalR hub for real-time updates
- Streaming translation endpoint (SSE)
- Translation comparison

```bash
cd mostlylucid.llmslidetranslator.Demo
dotnet run
```

Then navigate to `http://localhost:5000/swagger` for the API documentation.

## License

Unlicense - Public Domain

## Contributing

Contributions welcome! Please open an issue or PR on GitHub.
