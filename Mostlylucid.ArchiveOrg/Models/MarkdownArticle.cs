namespace Mostlylucid.ArchiveOrg.Models;

/// <summary>
/// Represents a converted markdown article ready for blog import
/// </summary>
public class MarkdownArticle
{
    /// <summary>
    /// Article title extracted from HTML
    /// </summary>
    public string Title { get; set; } = string.Empty;

    /// <summary>
    /// URL slug for the article
    /// </summary>
    public string Slug { get; set; } = string.Empty;

    /// <summary>
    /// Original URL of the archived page
    /// </summary>
    public string OriginalUrl { get; set; } = string.Empty;

    /// <summary>
    /// Archive.org snapshot date
    /// </summary>
    public DateTime ArchiveDate { get; set; }

    /// <summary>
    /// Inferred/extracted publish date (may differ from archive date)
    /// </summary>
    public DateTime? PublishDate { get; set; }

    /// <summary>
    /// Categories/tags for the article
    /// </summary>
    public List<string> Categories { get; set; } = [];

    /// <summary>
    /// The markdown content
    /// </summary>
    public string MarkdownContent { get; set; } = string.Empty;

    /// <summary>
    /// Source HTML file path
    /// </summary>
    public string SourceFilePath { get; set; } = string.Empty;

    /// <summary>
    /// Output markdown file path
    /// </summary>
    public string OutputFilePath { get; set; } = string.Empty;

    /// <summary>
    /// Images found and downloaded
    /// </summary>
    public List<ImageInfo> Images { get; set; } = [];

    /// <summary>
    /// Generate the full markdown file content with frontmatter
    /// Compatible with the Mostlylucid blog format
    /// </summary>
    public string ToFullMarkdown()
    {
        var effectiveDate = PublishDate ?? ArchiveDate;
        var categories = Categories.Count > 0 ? string.Join(", ", Categories) : "Imported";

        return $"""
                # {Title}

                <datetime class="hidden">{effectiveDate:yyyy-MM-ddTHH:mm}</datetime>
                <!-- category -- {categories} -->

                {MarkdownContent}
                """;
    }
}

public class ImageInfo
{
    public string OriginalUrl { get; set; } = string.Empty;
    public string LocalPath { get; set; } = string.Empty;
    public string MarkdownPath { get; set; } = string.Empty;
    public bool Downloaded { get; set; }
}
