namespace Mostlylucid.LlmI18nAssistant.Models;

/// <summary>
///     Configuration for the LLM I18n Assistant
/// </summary>
public class LlmI18nAssistantConfig
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
    ///     Consistency mode configuration (RAG over glossary/translations)
    /// </summary>
    public ConsistencyModeConfig ConsistencyMode { get; set; } = new();

    /// <summary>
    ///     Value transformation settings
    /// </summary>
    public ValueTransformationConfig ValueTransformation { get; set; } = new();

    /// <summary>
    ///     Path to store embeddings and vector indices
    /// </summary>
    public string DataPath { get; set; } = "./data";

    /// <summary>
    ///     Default source language
    /// </summary>
    public string DefaultSourceLanguage { get; set; } = "en";

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
    ///     Maximum tokens to generate (shorter for UI strings)
    /// </summary>
    public int MaxTokens { get; set; } = 512;

    /// <summary>
    ///     Request timeout in seconds
    /// </summary>
    public int TimeoutSeconds { get; set; } = 60;
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
    ///     NMT service endpoints
    /// </summary>
    public List<string> ServiceEndpoints { get; set; } = ["http://localhost:24080"];

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
///     Consistency mode configuration for terminology alignment
/// </summary>
public class ConsistencyModeConfig
{
    /// <summary>
    ///     Enable consistency mode (RAG over existing translations and glossary)
    /// </summary>
    public bool Enabled { get; set; } = true;

    /// <summary>
    ///     Path to glossary files
    /// </summary>
    public string GlossaryPath { get; set; } = "./glossaries";

    /// <summary>
    ///     Minimum relevance threshold for retrieving similar terms (0.0 - 1.0)
    /// </summary>
    public float MinRelevance { get; set; } = 0.6f;

    /// <summary>
    ///     Maximum number of context items to include in prompt
    /// </summary>
    public int MaxContextItems { get; set; } = 5;

    /// <summary>
    ///     Number of similar entries to retrieve
    /// </summary>
    public int TopK { get; set; } = 3;

    /// <summary>
    ///     Include translations from the same file for context
    /// </summary>
    public bool UseSameFileContext { get; set; } = true;
}

/// <summary>
///     Value transformation settings for preserving special content
/// </summary>
public class ValueTransformationConfig
{
    /// <summary>
    ///     Preserve .NET format strings like {0}, {1:N2}, etc.
    /// </summary>
    public bool PreserveFormatStrings { get; set; } = true;

    /// <summary>
    ///     Preserve named placeholders like {{name}}, {userName}, etc.
    /// </summary>
    public bool PreserveNamedPlaceholders { get; set; } = true;

    /// <summary>
    ///     Preserve HTML tags in values
    /// </summary>
    public bool PreserveHtmlTags { get; set; } = true;

    /// <summary>
    ///     Preserve markdown formatting
    /// </summary>
    public bool PreserveMarkdown { get; set; } = true;

    /// <summary>
    ///     Regex patterns for keys to skip (not translate)
    /// </summary>
    public List<string> SkipKeyPatterns { get; set; } = [];

    /// <summary>
    ///     Regex patterns for values to skip (not translate)
    /// </summary>
    public List<string> SkipValuePatterns { get; set; } = [@"^https?://", @"^\d+$"];
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
    public string CollectionName { get; set; } = "i18n_translations";

    /// <summary>
    ///     Enable HTTPS
    /// </summary>
    public bool UseHttps { get; set; } = false;
}