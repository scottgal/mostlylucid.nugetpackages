namespace Mostlylucid.LLMContentModeration.Models;

/// <summary>
/// Main configuration options for content moderation
/// </summary>
public class ModerationOptions
{
    /// <summary>
    /// Whether content moderation is enabled
    /// </summary>
    public bool Enabled { get; set; } = true;

    /// <summary>
    /// Default moderation mode when not specified per-route
    /// </summary>
    public ModerationMode DefaultMode { get; set; } = ModerationMode.Block;

    /// <summary>
    /// Enable moderation for blog comments by default
    /// </summary>
    public bool EnableForComments { get; set; } = true;

    /// <summary>
    /// Enable moderation for request bodies
    /// </summary>
    public bool ModerateRequests { get; set; } = true;

    /// <summary>
    /// Enable moderation for response bodies
    /// </summary>
    public bool ModerateResponses { get; set; } = false;

    /// <summary>
    /// Content types to moderate (e.g., "application/json", "text/plain")
    /// </summary>
    public string[] ContentTypes { get; set; } = ["application/json", "text/plain", "text/html"];

    /// <summary>
    /// Maximum content length to moderate (in characters)
    /// </summary>
    public int MaxContentLength { get; set; } = 50000;

    /// <summary>
    /// Paths to exclude from moderation (supports wildcards)
    /// </summary>
    public string[] ExcludedPaths { get; set; } = ["/health", "/api/health", "/swagger*"];

    /// <summary>
    /// Paths that always require moderation (overrides exclusions)
    /// </summary>
    public string[] RequiredPaths { get; set; } = ["/api/comments*", "/api/reviews*"];

    /// <summary>
    /// Ollama configuration
    /// </summary>
    public OllamaConfig Ollama { get; set; } = new();

    /// <summary>
    /// Content classification settings
    /// </summary>
    public ContentClassificationOptions ContentClassification { get; set; } = new();

    /// <summary>
    /// PII detection settings
    /// </summary>
    public PiiDetectionOptions PiiDetection { get; set; } = new();

    /// <summary>
    /// Custom callback when content is blocked
    /// </summary>
    public Func<ModerationResult, Task>? OnContentBlocked { get; set; }

    /// <summary>
    /// Custom callback when content is flagged (in DetectOnly mode)
    /// </summary>
    public Func<ModerationResult, Task>? OnContentFlagged { get; set; }
}

/// <summary>
/// Ollama LLM configuration
/// </summary>
public class OllamaConfig
{
    /// <summary>
    /// Ollama API endpoint
    /// </summary>
    public string Endpoint { get; set; } = "http://localhost:11434";

    /// <summary>
    /// Model to use for content moderation
    /// </summary>
    public string Model { get; set; } = "llama3.2:3b";

    /// <summary>
    /// Temperature for LLM inference (lower = more deterministic)
    /// </summary>
    public float Temperature { get; set; } = 0.1f;

    /// <summary>
    /// Maximum tokens for response
    /// </summary>
    public int MaxTokens { get; set; } = 512;

    /// <summary>
    /// Request timeout in seconds
    /// </summary>
    public int TimeoutSeconds { get; set; } = 30;
}

/// <summary>
/// Content classification configuration
/// </summary>
public class ContentClassificationOptions
{
    /// <summary>
    /// Enable toxicity detection
    /// </summary>
    public bool EnableToxicity { get; set; } = true;

    /// <summary>
    /// Enable spam detection
    /// </summary>
    public bool EnableSpam { get; set; } = true;

    /// <summary>
    /// Enable self-harm content detection
    /// </summary>
    public bool EnableSelfHarm { get; set; } = true;

    /// <summary>
    /// Enable NSFW content detection
    /// </summary>
    public bool EnableNsfw { get; set; } = true;

    /// <summary>
    /// Confidence threshold for toxicity (0.0-1.0)
    /// </summary>
    public float ToxicityThreshold { get; set; } = 0.7f;

    /// <summary>
    /// Confidence threshold for spam (0.0-1.0)
    /// </summary>
    public float SpamThreshold { get; set; } = 0.8f;

    /// <summary>
    /// Confidence threshold for self-harm (0.0-1.0)
    /// </summary>
    public float SelfHarmThreshold { get; set; } = 0.5f;

    /// <summary>
    /// Confidence threshold for NSFW (0.0-1.0)
    /// </summary>
    public float NsfwThreshold { get; set; } = 0.7f;
}

/// <summary>
/// PII detection configuration
/// </summary>
public class PiiDetectionOptions
{
    /// <summary>
    /// Enable PII detection
    /// </summary>
    public bool Enabled { get; set; } = true;

    /// <summary>
    /// Use LLM for enhanced PII detection (in addition to regex)
    /// </summary>
    public bool UseLlmEnhancement { get; set; } = false;

    /// <summary>
    /// Detect email addresses
    /// </summary>
    public bool DetectEmail { get; set; } = true;

    /// <summary>
    /// Detect phone numbers
    /// </summary>
    public bool DetectPhone { get; set; } = true;

    /// <summary>
    /// Detect physical addresses
    /// </summary>
    public bool DetectAddress { get; set; } = true;

    /// <summary>
    /// Detect IBAN numbers
    /// </summary>
    public bool DetectIban { get; set; } = true;

    /// <summary>
    /// Detect credit card numbers
    /// </summary>
    public bool DetectCreditCard { get; set; } = true;

    /// <summary>
    /// Detect social security numbers
    /// </summary>
    public bool DetectSocialSecurityNumber { get; set; } = true;

    /// <summary>
    /// Character used to mask PII
    /// </summary>
    public char MaskCharacter { get; set; } = '*';

    /// <summary>
    /// Number of characters to leave unmasked at start/end
    /// </summary>
    public int UnmaskedChars { get; set; } = 2;
}
