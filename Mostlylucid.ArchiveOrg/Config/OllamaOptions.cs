namespace Mostlylucid.ArchiveOrg.Config;

public class OllamaOptions
{
    public const string SectionName = "Ollama";

    /// <summary>
    /// Ollama API endpoint
    /// </summary>
    public string BaseUrl { get; set; } = "http://localhost:11434";

    /// <summary>
    /// Model to use for tag generation (e.g., "llama3.2", "mistral", "phi3")
    /// </summary>
    public string Model { get; set; } = "llama3.2";

    /// <summary>
    /// Temperature for generation (0.0 - 1.0)
    /// Lower = more deterministic
    /// </summary>
    public float Temperature { get; set; } = 0.3f;

    /// <summary>
    /// Maximum number of tags to generate
    /// </summary>
    public int MaxTags { get; set; } = 5;

    /// <summary>
    /// Timeout for LLM requests in seconds
    /// </summary>
    public int TimeoutSeconds { get; set; } = 60;

    /// <summary>
    /// Whether Ollama integration is enabled
    /// </summary>
    public bool Enabled { get; set; } = true;
}
