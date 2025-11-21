namespace mostlylucid.llmslidetranslator.Models;

/// <summary>
///     Configuration for the LLM Slide Translator
/// </summary>
public class LlmSlideTranslatorConfig
{
    /// <summary>
    ///     Ollama configuration
    /// </summary>
    public OllamaConfig Ollama { get; set; } = new();

    /// <summary>
    ///     NMT (Neural Machine Translation) configuration
    /// </summary>
    public NmtConfig Nmt { get; set; } = new();

    /// <summary>
    ///     Embedding configuration
    /// </summary>
    public EmbeddingConfig Embedding { get; set; } = new();

    /// <summary>
    ///     RAG configuration
    /// </summary>
    public RagConfig Rag { get; set; } = new();

    /// <summary>
    ///     Path to store embeddings and vector indices
    /// </summary>
    public string DataPath { get; set; } = "./data";

    /// <summary>
    ///     Default source language
    /// </summary>
    public string DefaultSourceLanguage { get; set; } = "en";

    /// <summary>
    ///     Default target language
    /// </summary>
    public string DefaultTargetLanguage { get; set; } = "de";

    /// <summary>
    ///     Vector store provider: "File" or "Qdrant"
    /// </summary>
    public string VectorStoreProvider { get; set; } = "File";

    /// <summary>
    ///     Qdrant configuration (if using Qdrant provider)
    /// </summary>
    public QdrantConfig Qdrant { get; set; } = new();
}

/// <summary>
///     Ollama LLM configuration
/// </summary>
public class OllamaConfig
{
    /// <summary>
    ///     Ollama API endpoint
    /// </summary>
    public string Endpoint { get; set; } = "http://localhost:11434";

    /// <summary>
    ///     Model name to use for translation
    /// </summary>
    public string Model { get; set; } = "llama3.2:3b";

    /// <summary>
    ///     Temperature for generation (0.0 - 1.0)
    /// </summary>
    public float Temperature { get; set; } = 0.3f;

    /// <summary>
    ///     Maximum tokens to generate
    /// </summary>
    public int MaxTokens { get; set; } = 2048;

    /// <summary>
    ///     Request timeout in seconds
    /// </summary>
    public int TimeoutSeconds { get; set; } = 120;
}

/// <summary>
///     NMT translator configuration
/// </summary>
public class NmtConfig
{
    /// <summary>
    ///     Enable NMT baseline translation
    /// </summary>
    public bool Enabled { get; set; } = true;

    /// <summary>
    ///     NMT service endpoints (from mostlyucid-nmt)
    /// </summary>
    public List<string> ServiceEndpoints { get; set; } = new() { "http://localhost:24080" };

    /// <summary>
    ///     Use NMT as baseline before LLM post-editing
    /// </summary>
    public bool UseAsBaseline { get; set; } = true;
}

/// <summary>
///     Embedding model configuration
/// </summary>
public class EmbeddingConfig
{
    /// <summary>
    ///     Provider for embeddings: "Local" (LlamaSharp GGUF) or "Ollama"
    /// </summary>
    public string Provider { get; set; } = "Ollama";

    /// <summary>
    ///     Path to the embedding model (GGUF format for LlamaSharp) - only used when Provider is "Local"
    /// </summary>
    public string ModelPath { get; set; } = "./models/nomic-embed-text-v1.5.Q8_0.gguf";

    /// <summary>
    ///     Ollama model name for embeddings - only used when Provider is "Ollama"
    /// </summary>
    public string OllamaModel { get; set; } = "nomic-embed-text:latest";

    /// <summary>
    ///     Embedding dimension
    /// </summary>
    public int Dimension { get; set; } = 768;

    /// <summary>
    ///     Context size for embeddings
    /// </summary>
    public int ContextSize { get; set; } = 2048;

    /// <summary>
    ///     Batch size for embedding generation
    /// </summary>
    public int BatchSize { get; set; } = 512;
}

/// <summary>
///     RAG retrieval configuration
/// </summary>
public class RagConfig
{
    /// <summary>
    ///     Number of similar blocks to retrieve
    /// </summary>
    public int TopK { get; set; } = 3;

    /// <summary>
    ///     Always include N-1 block (sliding window)
    /// </summary>
    public bool UseSlidingWindow { get; set; } = true;

    /// <summary>
    ///     Minimum similarity threshold (0.0 - 1.0)
    /// </summary>
    public float MinSimilarity { get; set; } = 0.5f;

    /// <summary>
    ///     Maximum context blocks to include in prompt
    /// </summary>
    public int MaxContextBlocks { get; set; } = 5;
}

/// <summary>
///     Qdrant vector database configuration
/// </summary>
public class QdrantConfig
{
    /// <summary>
    ///     Qdrant server endpoint
    /// </summary>
    public string Endpoint { get; set; } = "http://localhost:6333";

    /// <summary>
    ///     API key for Qdrant (if required)
    /// </summary>
    public string? ApiKey { get; set; }

    /// <summary>
    ///     Collection name for storing embeddings
    /// </summary>
    public string CollectionName { get; set; } = "translations";

    /// <summary>
    ///     Enable HTTPS
    /// </summary>
    public bool UseHttps { get; set; } = false;
}