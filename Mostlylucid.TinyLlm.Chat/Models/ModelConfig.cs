namespace Mostlylucid.TinyLlm.Chat.Models;

/// <summary>
/// Configuration for the LLM model
/// </summary>
public class ModelConfig
{
    /// <summary>
    /// Path to the GGUF model file
    /// </summary>
    public string ModelPath { get; set; } = string.Empty;

    /// <summary>
    /// Context size (number of tokens the model can process at once)
    /// </summary>
    public uint ContextSize { get; set; } = 2048;

    /// <summary>
    /// Number of GPU layers to offload (0 = CPU only)
    /// </summary>
    public int GpuLayerCount { get; set; } = 0;

    /// <summary>
    /// Random seed for reproducibility (-1 = random)
    /// </summary>
    public int Seed { get; set; } = -1;

    /// <summary>
    /// Temperature for text generation (higher = more creative)
    /// </summary>
    public float Temperature { get; set; } = 0.7f;

    /// <summary>
    /// Top-p sampling (nucleus sampling)
    /// </summary>
    public float TopP { get; set; } = 0.95f;

    /// <summary>
    /// Maximum number of tokens to generate
    /// </summary>
    public int MaxTokens { get; set; } = 256;

    /// <summary>
    /// Repetition penalty to reduce repetitive text
    /// </summary>
    public float RepeatPenalty { get; set; } = 1.1f;
}
