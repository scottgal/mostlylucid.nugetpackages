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
      "Model": "mannix/llamax3-8b-alpaca",
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

### Cross-Document Learning Mode (Experimental)

> **Experimental Feature**: This mode is off by default and should be used with caution.

By default, RAG entries are cleared after each document translation. **Cross-document mode** retains translation pairs across documents, allowing the translator to "learn" from previous translations.

```csharp
// Enable cross-document learning
var result = await translator.TranslateAsync(
    markdown: content,
    documentId: "doc_002",
    sourceLanguage: "en",
    targetLanguage: "de",
    method: TranslationMethod.RagLlm,
    crossDocumentMode: true  // Retain RAG data from previous translations
);
```

**How it works:**
```
Document 1 Translation          Document 2 Translation (with cross-doc)
┌─────────────────────┐         ┌─────────────────────────────────────┐
│ Block 1: "mayor"    │         │ Block 1: "mayor"                    │
│ → "Bürgermeister"   │────────▶│ RAG retrieves from Doc 1!           │
│                     │  kept   │ → "Bürgermeister" (lower weight)    │
└─────────────────────┘         └─────────────────────────────────────┘
        ↓                                      ↓
   RAG retained                   Consistent from first block
```

**Use cases:**
- **Book series**: Translate multiple books maintaining character name consistency
- **Documentation sets**: Keep API terminology consistent across docs
- **Incremental translation**: Add new chapters to an existing translation

**Configuration:**
```json
{
  "LlmSlideTranslator": {
    "Rag": {
      "CrossDocumentMode": false,
      "CrossDocumentWeight": 0.7,
      "MaxCrossDocumentBlocks": 3
    }
  }
}
```

**Weighting:**
- Same-document RAG matches: weight = 1.0 (full confidence)
- Cross-document RAG matches: weight = 0.7 (lower confidence, configurable)

**Caveats:**
- Cross-document entries are lower-weighted to prefer same-document context
- May introduce inconsistencies if source documents have conflicting terminology
- Vector store size grows over time (consider periodic cleanup)

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

## Why Context Preservation Matters

When translating long documents, without context, LLMs often translate the same term inconsistently. This leads to confusing and unprofessional translations. Consider these real-world examples:

### Example 1: "Mayor" in a Political Document

Without context preservation (chunk-by-chunk translation):
- Block 1: "The **mayor** announced new policies..." → "Der **Bürgermeister** kündigte..."
- Block 15: "...the **mayor** spoke at the conference..." → "...der **Oberbürgermeister** sprach..."
- Block 28: "...city **mayor** Johnson said..." → "...Stadtoberhaupt Johnson sagte..."

**Problem**: Three different translations for the same person/role!

With RAG + sliding window:
- All blocks consistently use "**Bürgermeister**" because the translator retrieves earlier translations of "mayor" and maintains consistency.

### Example 2: Technical Terms

Without context:
- "The **controller** handles requests..." → "Der **Controller** behandelt..."
- "...each **controller** is tested..." → "...jeder **Regler** wird getestet..."
- "...the **controller** pattern..." → "...das **Steuerung**-Muster..."

With context preservation:
- All instances use "**Controller**" consistently (keeping the technical term).

### Example 3: Proper Names and Titles

Without context:
- "Dr. Smith, the **lead researcher**..." → "Dr. Smith, der **leitende Forscher**..."
- "...the **lead researcher** presented..." → "...der **Hauptforscher** präsentierte..."

With context preservation:
- Both use "**leitende Forscher**" consistently, maintaining document coherence.

### How This Package Solves It

1. **Vector Similarity Search (RAG)**: When translating "mayor" in block 28, the system finds blocks 1 and 15 that also contain "mayor" and provides **both the original AND translated text** as reference examples. The LLM sees:
   ```
   Earlier translation examples:
   [EN] "The mayor announced new policies..."
   [DE] "Der Bürgermeister kündigte neue Richtlinien an..."
   ```

2. **Sliding Window**: The previous translated block (original + translation) is always included, providing immediate context for style, tone, and terminology.

3. **Result**: Professional, consistent translations that read like they were done by a human translator with a terminology glossary.

## Recommended Models

For translation tasks, you want a model with:
- Good multilingual capabilities
- Large context window (for RAG context)
- Fast inference (many blocks to translate)

### Specialized Translation Models (Available Now)

| Model | Size | Context | Languages | Notes |
|-------|------|---------|-----------|-------|
| **mannix/llamax3-8b-alpaca** | 8B | 8K | 100+ | Best coverage, multilingual powerhouse |
| **winkefinger/alma-13b** | 13B | 4K | 10 pairs | Beat GPT-4 on WMT benchmarks |
| **aya:8b** | 8B | 8K | 23 | Cohere's multilingual, excellent quality |
| **zongwei/gemma3-translator** | 9B | 8K | Multi | Specifically tuned for translation |

**Top Pick**: `mannix/llamax3-8b-alpaca` - Supports 100+ languages while maintaining instruction-following. Best for diverse language pairs.

**Quality Pick**: `winkefinger/alma-13b` - ALMA-R beat GPT-4 on several WMT translation benchmarks. Supports: EN↔DE, EN↔CS, EN↔IS, EN↔ZH, EN↔RU.

### General Purpose Models (Good at Translation)

| Model | Size | Context | Notes |
|-------|------|---------|-------|
| **qwen2.5:7b** | 7B | 128K | Excellent multilingual, huge context |
| **mistral-small:22b** | 22B | 128K | State-of-the-art, if you have VRAM |
| **gemma2:9b** | 9B | 8K | Very accurate, Google quality |

### Future: Seed-X

**Seed-X** (ByteDance) - A 7B model specifically trained for translation that rivals GPT-4o:
- Supports **28 languages** with Chain-of-Thought reasoning
- Optimized for: tech, legal, medical, finance, literature

**Status**: Not yet in Ollama ([#11484](https://github.com/ollama/ollama/issues/11484)). Available on HuggingFace as `ByteDance-Seed/Seed-X-Instruct-7B`.

### Smaller Models (faster, less accurate)

| Model | Context | Best For |
|-------|---------|----------|
| **qwen2.5:3b** | 128K | Quick drafts, simple text |
| **llama3.2:3b** | 128K | Resource-constrained systems |
| **phi3:mini** | 128K | Very fast, basic quality |

### Configuration Example

```json
{
  "LlmSlideTranslator": {
    "Ollama": {
      "Model": "mannix/llamax3-8b-alpaca",
      "Temperature": 0.3,
      "MaxTokens": 2048
    }
  }
}
```

**Tip**: For best results, use `Temperature: 0.3` or lower to reduce creative variations and maintain consistency.

**Default model**: `mannix/llamax3-8b-alpaca` - Supports 100+ languages with excellent instruction-following. Pull with: `ollama pull mannix/llamax3-8b-alpaca`

## How It Works

### Architecture Overview

```
┌─────────────────┐    ┌──────────────────┐    ┌─────────────────┐
│  Source Block   │───▶│ Embedding Model  │───▶│ Vector Search   │
│  (Current)      │    │ (Ollama/Local)   │    │ (Qdrant/File)   │
└─────────────────┘    └──────────────────┘    └────────┬────────┘
                                                        │
┌─────────────────┐    ┌──────────────────┐    ┌────────▼────────┐
│  Previous Block │    │  Context Window  │◀───│ Similar Blocks  │
│  (Source+Trans) │───▶│  Builder         │    │ (Source+Trans)  │
└─────────────────┘    └────────┬─────────┘    └─────────────────┘
                                │
                       ┌────────▼────────┐
                       │  LLM Translate  │
                       │  (Ollama)       │
                       └────────┬────────┘
                                │
                       ┌────────▼────────┐
                       │ Store Result    │
                       │ (For Future RAG)│
                       └─────────────────┘
```

### Step-by-Step Process

1. **Chunking**: Markdown is split into translatable blocks (paragraphs, headings, lists) while preserving structure

2. **Embedding**: Each source block is converted to a semantic vector embedding using the configured embedding model

3. **Storage**: All blocks (source + translation pairs) are stored in a vector database:
   - **File-based** (default): Local JSON file for persistence - simple, no dependencies
   - **Qdrant**: Scalable vector database for larger deployments

4. **Translation Loop**: For each block to translate:
   - Generate embedding for current source block
   - **RAG Search**: Find semantically similar *earlier* blocks (already translated) using vector similarity
   - **Sliding Window**: Always include the immediately previous block (both source AND translation)
   - **Context Building**: Assemble context fitting within the model's context window:
     - Current source block (to translate)
     - Previous block pair (source + translation) for continuity
     - Similar earlier block pairs from RAG (source + translation) for terminology consistency
   - **LLM Translation**: Send context-aware prompt to LLM
   - **Store Result**: Save translation for future RAG lookups

5. **Output**: Aligned block-by-block translation with consistent terminology

### Why Both RAG and Sliding Window?

- **Sliding Window** (previous block): Ensures smooth flow and consistent tone between adjacent paragraphs
- **RAG** (similar blocks): Finds terminology matches from anywhere in the document - if you mentioned "controller" in paragraph 5 and translate it again in paragraph 50, RAG retrieves that earlier translation

### Lightweight Design for Massive Documents

The key insight: **small local LLMs can match frontier model quality on translation** - if you give them the right context. This package achieves that with minimal resources.

**The Problem with Large Context Windows:**
- Sending a 50,000 word document to an LLM is expensive and slow
- Most of that context is irrelevant to any single paragraph
- Small local models can't handle 100K+ context anyway

**The Sliding + RAG Solution:**
```
Traditional approach:           Sliding + RAG approach:
┌─────────────────────┐        ┌─────────────────────┐
│ Send entire 50K doc │        │ Send only:          │
│ to frontier model   │        │  - Current block    │
│ ($$$, slow)         │        │  - Previous block   │
└─────────────────────┘        │  - 3-5 RAG matches  │
                               │ (~2K tokens total)  │
                               └─────────────────────┘
```

**Why this works:**
1. **Chunking persists to JSON** - Document split once, stored on disk
2. **Only load what you need** - Current block + sliding window + RAG hits
3. **Fits small context windows** - Works with 8K context models
4. **Resumable** - If interrupted, resume from last translated block
5. **Same quality** - RAG ensures terminology consistency across the entire document

**Result:** Translate a 200-page book on a laptop with a 3B parameter model, maintaining the same translation consistency as sending the whole thing to GPT-4.

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
