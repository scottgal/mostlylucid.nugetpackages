namespace Mostlylucid.SentimentAnalysis.Models;

/// <summary>
/// Specifies the backend provider for sentiment analysis.
/// </summary>
public enum SentimentProvider
{
    /// <summary>
    /// Use ONNX Runtime with a local model (fast, CPU-only, works offline).
    /// </summary>
    Onnx,

    /// <summary>
    /// Use Ollama with an LLM model (more flexible, requires Ollama server).
    /// </summary>
    Ollama
}

/// <summary>
/// Configuration options for the sentiment analysis service.
/// </summary>
public class SentimentOptions
{
    /// <summary>
    /// The provider to use for sentiment analysis.
    /// Default: Onnx (for backwards compatibility and offline operation).
    /// </summary>
    public SentimentProvider Provider { get; set; } = SentimentProvider.Onnx;

    // ==========================================
    // ONNX-specific options
    // ==========================================

    /// <summary>
    /// The path where ONNX models will be stored.
    /// Default: "./models/sentiment"
    /// </summary>
    public string ModelPath { get; set; } = "./models/sentiment";

    /// <summary>
    /// The URL to download the ONNX model from if not present locally.
    /// Uses a quantized DistilBERT model fine-tuned for sentiment analysis.
    /// </summary>
    public string ModelUrl { get; set; } =
        "https://huggingface.co/lxyuan/distilbert-base-multilingual-cased-sentiments-student/resolve/main/onnx/model_quantized.onnx";

    /// <summary>
    /// The filename for the ONNX model.
    /// </summary>
    public string ModelFileName { get; set; } = "sentiment_model.onnx";

    /// <summary>
    /// Maximum length of text chunks for analysis.
    /// Text longer than this will be split into chunks.
    /// Default: 512 tokens (BERT limit).
    /// </summary>
    public int MaxChunkLength { get; set; } = 450;

    /// <summary>
    /// Overlap between chunks when splitting long text.
    /// Helps maintain context at chunk boundaries.
    /// Default: 50 tokens.
    /// </summary>
    public int ChunkOverlap { get; set; } = 50;

    /// <summary>
    /// Enable diagnostic logging for debugging.
    /// </summary>
    public bool EnableDiagnosticLogging { get; set; } = false;

    /// <summary>
    /// Number of threads to use for ONNX inference.
    /// Default: 0 (use all available cores).
    /// </summary>
    public int InferenceThreads { get; set; } = 0;

    /// <summary>
    /// Timeout in seconds for model download.
    /// Default: 300 seconds (5 minutes).
    /// </summary>
    public int DownloadTimeoutSeconds { get; set; } = 300;

    /// <summary>
    /// Whether to automatically download the model if not present.
    /// Default: true.
    /// </summary>
    public bool AutoDownloadModel { get; set; } = true;

    /// <summary>
    /// The sentiment labels used by the model.
    /// Default maps to: Negative (0), Neutral (1), Positive (2).
    /// </summary>
    public string[] ModelLabels { get; set; } = ["negative", "neutral", "positive"];

    // ==========================================
    // Ollama-specific options
    // ==========================================

    /// <summary>
    /// The Ollama API endpoint URL.
    /// Default: "http://localhost:11434"
    /// </summary>
    public string OllamaEndpoint { get; set; } = "http://localhost:11434";

    /// <summary>
    /// The Ollama model to use for sentiment analysis.
    /// Default: "llama3.2" (good balance of speed and accuracy).
    /// Other options: "qwen2.5:1.5b" (faster), "mistral" (more accurate).
    /// </summary>
    public string OllamaModel { get; set; } = "llama3.2";

    /// <summary>
    /// Timeout in milliseconds for Ollama API calls.
    /// Default: 30000 (30 seconds).
    /// </summary>
    public int OllamaTimeoutMs { get; set; } = 30000;

    /// <summary>
    /// The system prompt for the Ollama model.
    /// Used to instruct the model how to analyze sentiment.
    /// </summary>
    public string OllamaSystemPrompt { get; set; } = """
        You are a sentiment analysis assistant. Analyze the sentiment of the given text and respond with ONLY a JSON object in this exact format:
        {"sentiment": "positive|negative|neutral", "confidence": 0.0-1.0, "reasoning": "brief explanation"}

        Guidelines:
        - "positive": Happy, satisfied, enthusiastic, grateful, optimistic content
        - "negative": Angry, sad, frustrated, disappointed, critical content
        - "neutral": Factual, informational, balanced, or mixed sentiment
        - confidence: How certain you are (0.0 = uncertain, 1.0 = very certain)

        Respond with ONLY the JSON object, no other text.
        """;
}
