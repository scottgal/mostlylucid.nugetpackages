using System.ComponentModel.DataAnnotations;

namespace Mostlylucid.LlmAltText.Data;

/// <summary>
///     Cached alt text entry for an image
/// </summary>
public class ImageAltTextEntry
{
    /// <summary>
    ///     Unique identifier
    /// </summary>
    [Key]
    public int Id { get; set; }

    /// <summary>
    ///     Hash of the image source (URL or path) for lookup
    /// </summary>
    [Required]
    [MaxLength(64)]
    public required string SourceHash { get; set; }

    /// <summary>
    ///     Original image source (URL or file path)
    /// </summary>
    [Required]
    [MaxLength(2048)]
    public required string ImageSource { get; set; }

    /// <summary>
    ///     Hash of the image content (for detecting changes)
    /// </summary>
    [MaxLength(64)]
    public string? ContentHash { get; set; }

    /// <summary>
    ///     Generated alt text
    /// </summary>
    [Required]
    public required string AltText { get; set; }

    /// <summary>
    ///     Extracted text from image (OCR)
    /// </summary>
    public string? ExtractedText { get; set; }

    /// <summary>
    ///     Detected content type (Photograph, Document, Screenshot, etc.)
    /// </summary>
    [MaxLength(50)]
    public string? ContentType { get; set; }

    /// <summary>
    ///     Confidence score for the content type
    /// </summary>
    public double? ContentTypeConfidence { get; set; }

    /// <summary>
    ///     When the entry was created
    /// </summary>
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    /// <summary>
    ///     When the entry was last updated
    /// </summary>
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

    /// <summary>
    ///     Number of times this alt text has been used
    /// </summary>
    public int UsageCount { get; set; } = 1;

    /// <summary>
    ///     Whether this entry has been manually reviewed/edited
    /// </summary>
    public bool IsManuallyReviewed { get; set; } = false;
}