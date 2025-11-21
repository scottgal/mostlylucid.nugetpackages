namespace Mostlylucid.LlmAltText.Models;

/// <summary>
///     Database provider for alt text caching
/// </summary>
public enum AltTextDbProvider
{
    /// <summary>SQLite (default, file-based)</summary>
    Sqlite,

    /// <summary>PostgreSQL</summary>
    PostgreSql
}

/// <summary>
///     Configuration options for alt text generation
/// </summary>
public class AltTextOptions
{
    /// <summary>
    ///     Directory where Florence-2 models will be downloaded and stored
    ///     Default: "./models"
    ///     Note: Models are approximately 800MB and will be downloaded on first use
    /// </summary>
    public string ModelPath { get; set; } = "./models";

    /// <summary>
    ///     Custom prompt for alt text generation
    ///     Default provides descriptive, accessible alt text
    /// </summary>
    public string AltTextPrompt { get; set; } =
        "Provide 2-3 complete, descriptive alt text sentences in English. Do not stop mid-sentence; include context, subjects, and visible relationships. Avoid fragments and keep under 90 words.";

    /// <summary>
    ///     Default task type for alt text generation
    ///     Options: CAPTION, DETAILED_CAPTION, MORE_DETAILED_CAPTION
    ///     Default: MORE_DETAILED_CAPTION
    /// </summary>
    public string DefaultTaskType { get; set; } = "MORE_DETAILED_CAPTION";

    /// <summary>
    ///     Enable detailed diagnostic logging for model initialization and processing
    /// </summary>
    public bool EnableDiagnosticLogging { get; set; } = true;

    /// <summary>
    ///     Maximum word count for generated alt text
    ///     Default: 90 (recommended for accessibility)
    /// </summary>
    public int MaxWords { get; set; } = 90;

    // ===== TagHelper and Database Options =====

    /// <summary>
    ///     Enable the auto alt text TagHelper
    ///     When enabled, img tags without alt text will be automatically populated
    ///     Default: false
    /// </summary>
    public bool EnableTagHelper { get; set; } = false;

    /// <summary>
    ///     Enable database caching of generated alt text
    ///     Required for TagHelper to work efficiently
    ///     Default: false
    /// </summary>
    public bool EnableDatabase { get; set; } = false;

    /// <summary>
    ///     Database provider to use (Sqlite or PostgreSql)
    ///     Default: Sqlite
    /// </summary>
    public AltTextDbProvider DbProvider { get; set; } = AltTextDbProvider.Sqlite;

    /// <summary>
    ///     Connection string for the database
    ///     For SQLite: "Data Source=alttext.db"
    ///     For PostgreSQL: "Host=localhost;Database=alttext;Username=user;Password=pass"
    /// </summary>
    public string? ConnectionString { get; set; }

    /// <summary>
    ///     SQLite database file path (only used when DbProvider is Sqlite and ConnectionString is not set)
    ///     Default: "./alttext.db"
    /// </summary>
    public string SqliteDbPath { get; set; } = "./alttext.db";

    /// <summary>
    ///     Auto-migrate database on startup
    ///     Default: true
    /// </summary>
    public bool AutoMigrateDatabase { get; set; } = true;

    /// <summary>
    ///     Only process images from these domains (empty = all domains)
    ///     Example: ["example.com", "cdn.example.com"]
    /// </summary>
    public List<string> AllowedImageDomains { get; set; } = new();

    /// <summary>
    ///     Skip images with these prefixes in src (e.g., data: URIs)
    ///     Default: ["data:", "blob:"]
    /// </summary>
    public List<string> SkipSrcPrefixes { get; set; } = new() { "data:", "blob:" };

    /// <summary>
    ///     Cache duration for alt text lookups in minutes
    ///     Default: 60
    /// </summary>
    public int CacheDurationMinutes { get; set; } = 60;
}