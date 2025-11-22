namespace Mostlylucid.LlmI18nAssistant.Models;

/// <summary>
///     Represents a resource file (either .resx or JSON)
/// </summary>
public class ResourceFile
{
    /// <summary>
    ///     Unique identifier for this resource file
    /// </summary>
    public string Id { get; set; } = Guid.NewGuid().ToString("N");

    /// <summary>
    ///     Original file path (if loaded from file)
    /// </summary>
    public string? FilePath { get; set; }

    /// <summary>
    ///     File name without extension
    /// </summary>
    public string? FileName { get; set; }

    /// <summary>
    ///     File type (Resx or Json)
    /// </summary>
    public ResourceFileType FileType { get; set; }

    /// <summary>
    ///     Source language of the resource file
    /// </summary>
    public string SourceLanguage { get; set; } = "en";

    /// <summary>
    ///     All entries in the resource file
    /// </summary>
    public List<ResourceEntry> Entries { get; set; } = [];

    /// <summary>
    ///     Additional metadata (for .resx header fields, etc.)
    /// </summary>
    public Dictionary<string, string> Metadata { get; set; } = new();

    /// <summary>
    ///     Gets entries that should be translated
    /// </summary>
    public IEnumerable<ResourceEntry> TranslatableEntries =>
        Entries.Where(e => e.ShouldTranslate);

    /// <summary>
    ///     Gets entries that were skipped
    /// </summary>
    public IEnumerable<ResourceEntry> SkippedEntries =>
        Entries.Where(e => !e.ShouldTranslate);

    /// <summary>
    ///     Total number of entries
    /// </summary>
    public int TotalCount => Entries.Count;

    /// <summary>
    ///     Number of translatable entries
    /// </summary>
    public int TranslatableCount => Entries.Count(e => e.ShouldTranslate);
}

/// <summary>
///     Type of resource file
/// </summary>
public enum ResourceFileType
{
    /// <summary>
    ///     .resx XML resource file
    /// </summary>
    Resx,

    /// <summary>
    ///     JSON resource file
    /// </summary>
    Json,

    /// <summary>
    ///     Simple key=value properties file
    /// </summary>
    Properties
}
