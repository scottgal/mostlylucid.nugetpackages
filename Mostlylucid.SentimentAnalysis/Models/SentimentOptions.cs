namespace Mostlylucid.SentimentAnalysis.Models;

/// <summary>
/// Configuration options for the sentiment analysis service.
/// </summary>
public class SentimentOptions
{
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
}
