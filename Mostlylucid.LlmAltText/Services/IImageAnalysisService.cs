namespace Mostlylucid.LlmAltText.Services;

/// <summary>
///     Classification of image content type
/// </summary>
public enum ImageContentType
{
    /// <summary>Unknown or mixed content</summary>
    Unknown,

    /// <summary>Photograph of real-world scene, people, objects</summary>
    Photograph,

    /// <summary>Document, form, or text-heavy content</summary>
    Document,

    /// <summary>Screenshot of software/UI</summary>
    Screenshot,

    /// <summary>Chart, graph, or data visualization</summary>
    Chart,

    /// <summary>Illustration, drawing, or artwork</summary>
    Illustration,

    /// <summary>Diagram or schematic</summary>
    Diagram
}

/// <summary>
///     Result of image analysis including classification
/// </summary>
public class ImageAnalysisResult
{
    /// <summary>Generated alt text description</summary>
    public required string AltText { get; set; }

    /// <summary>Text extracted via OCR (empty if no text found)</summary>
    public required string ExtractedText { get; set; }

    /// <summary>Detected content type of the image</summary>
    public ImageContentType ContentType { get; set; }

    /// <summary>Confidence score for the content type classification (0-1)</summary>
    public double ContentTypeConfidence { get; set; }

    /// <summary>Whether the image contains significant text</summary>
    public bool HasSignificantText { get; set; }
}

/// <summary>
///     Service for AI-powered image analysis including alt text generation and OCR
/// </summary>
public interface IImageAnalysisService
{
    /// <summary>
    ///     Check if the service is initialized and ready to process images
    /// </summary>
    bool IsReady { get; }

    /// <summary>
    ///     Generate descriptive alt text for an image
    /// </summary>
    /// <param name="imageStream">Image data stream (will not be disposed by this method)</param>
    /// <param name="taskType">Vision task type: CAPTION, DETAILED_CAPTION, or MORE_DETAILED_CAPTION</param>
    /// <returns>Generated alt text description</returns>
    Task<string> GenerateAltTextAsync(Stream imageStream, string taskType = "MORE_DETAILED_CAPTION");

    /// <summary>
    ///     Extract text content from an image using OCR
    /// </summary>
    /// <param name="imageStream">Image data stream (will not be disposed by this method)</param>
    /// <returns>Extracted text content</returns>
    Task<string> ExtractTextAsync(Stream imageStream);

    /// <summary>
    ///     Perform complete image analysis: generate alt text and extract any text
    /// </summary>
    /// <param name="imageStream">Image data stream (will not be disposed by this method)</param>
    /// <returns>Tuple containing both alt text and extracted text</returns>
    Task<(string AltText, string ExtractedText)> AnalyzeImageAsync(Stream imageStream);

    /// <summary>
    ///     Perform complete image analysis with content type classification
    /// </summary>
    /// <param name="imageStream">Image data stream (will not be disposed by this method)</param>
    /// <returns>Full analysis result including content type</returns>
    Task<ImageAnalysisResult> AnalyzeWithClassificationAsync(Stream imageStream);

    /// <summary>
    ///     Classify the content type of an image (document, photograph, screenshot, etc.)
    /// </summary>
    /// <param name="imageStream">Image data stream</param>
    /// <returns>Content type and confidence score</returns>
    Task<(ImageContentType Type, double Confidence)> ClassifyContentTypeAsync(Stream imageStream);
}