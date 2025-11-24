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

    // ===== File-based overloads =====

    /// <summary>
    ///     Generate descriptive alt text for an image file
    /// </summary>
    /// <param name="filePath">Path to the image file</param>
    /// <param name="taskType">Vision task type: CAPTION, DETAILED_CAPTION, or MORE_DETAILED_CAPTION</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Generated alt text description</returns>
    Task<string> GenerateAltTextFromFileAsync(string filePath, string taskType = "MORE_DETAILED_CAPTION",
        CancellationToken cancellationToken = default);

    /// <summary>
    ///     Extract text content from an image file using OCR
    /// </summary>
    /// <param name="filePath">Path to the image file</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Extracted text content</returns>
    Task<string> ExtractTextFromFileAsync(string filePath, CancellationToken cancellationToken = default);

    /// <summary>
    ///     Perform complete image analysis on a file: generate alt text and extract any text
    /// </summary>
    /// <param name="filePath">Path to the image file</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Tuple containing both alt text and extracted text</returns>
    Task<(string AltText, string ExtractedText)> AnalyzeImageFromFileAsync(string filePath,
        CancellationToken cancellationToken = default);

    /// <summary>
    ///     Perform complete image analysis on a file with content type classification
    /// </summary>
    /// <param name="filePath">Path to the image file</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Full analysis result including content type</returns>
    Task<ImageAnalysisResult> AnalyzeWithClassificationFromFileAsync(string filePath,
        CancellationToken cancellationToken = default);

    // ===== URL-based overloads =====

    /// <summary>
    ///     Generate descriptive alt text for an image from a URL
    /// </summary>
    /// <param name="imageUrl">URL of the image</param>
    /// <param name="taskType">Vision task type: CAPTION, DETAILED_CAPTION, or MORE_DETAILED_CAPTION</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Generated alt text description</returns>
    Task<string> GenerateAltTextFromUrlAsync(string imageUrl, string taskType = "MORE_DETAILED_CAPTION",
        CancellationToken cancellationToken = default);

    /// <summary>
    ///     Generate descriptive alt text for an image from a URL
    /// </summary>
    /// <param name="imageUrl">URI of the image</param>
    /// <param name="taskType">Vision task type: CAPTION, DETAILED_CAPTION, or MORE_DETAILED_CAPTION</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Generated alt text description</returns>
    Task<string> GenerateAltTextFromUrlAsync(Uri imageUrl, string taskType = "MORE_DETAILED_CAPTION",
        CancellationToken cancellationToken = default);

    /// <summary>
    ///     Extract text content from an image at a URL using OCR
    /// </summary>
    /// <param name="imageUrl">URL of the image</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Extracted text content</returns>
    Task<string> ExtractTextFromUrlAsync(string imageUrl, CancellationToken cancellationToken = default);

    /// <summary>
    ///     Extract text content from an image at a URL using OCR
    /// </summary>
    /// <param name="imageUrl">URI of the image</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Extracted text content</returns>
    Task<string> ExtractTextFromUrlAsync(Uri imageUrl, CancellationToken cancellationToken = default);

    /// <summary>
    ///     Perform complete image analysis on an image from URL: generate alt text and extract any text
    /// </summary>
    /// <param name="imageUrl">URL of the image</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Tuple containing both alt text and extracted text</returns>
    Task<(string AltText, string ExtractedText)> AnalyzeImageFromUrlAsync(string imageUrl,
        CancellationToken cancellationToken = default);

    /// <summary>
    ///     Perform complete image analysis on an image from URL: generate alt text and extract any text
    /// </summary>
    /// <param name="imageUrl">URI of the image</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Tuple containing both alt text and extracted text</returns>
    Task<(string AltText, string ExtractedText)> AnalyzeImageFromUrlAsync(Uri imageUrl,
        CancellationToken cancellationToken = default);

    /// <summary>
    ///     Perform complete image analysis on an image from URL with content type classification
    /// </summary>
    /// <param name="imageUrl">URL of the image</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Full analysis result including content type</returns>
    Task<ImageAnalysisResult> AnalyzeWithClassificationFromUrlAsync(string imageUrl,
        CancellationToken cancellationToken = default);

    /// <summary>
    ///     Perform complete image analysis on an image from URL with content type classification
    /// </summary>
    /// <param name="imageUrl">URI of the image</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Full analysis result including content type</returns>
    Task<ImageAnalysisResult> AnalyzeWithClassificationFromUrlAsync(Uri imageUrl,
        CancellationToken cancellationToken = default);

    // ===== Byte array overloads =====

    /// <summary>
    ///     Generate descriptive alt text for an image from byte array
    /// </summary>
    /// <param name="imageData">Image data as byte array</param>
    /// <param name="taskType">Vision task type: CAPTION, DETAILED_CAPTION, or MORE_DETAILED_CAPTION</param>
    /// <returns>Generated alt text description</returns>
    Task<string> GenerateAltTextAsync(byte[] imageData, string taskType = "MORE_DETAILED_CAPTION");

    /// <summary>
    ///     Extract text content from an image byte array using OCR
    /// </summary>
    /// <param name="imageData">Image data as byte array</param>
    /// <returns>Extracted text content</returns>
    Task<string> ExtractTextAsync(byte[] imageData);

    /// <summary>
    ///     Perform complete image analysis on byte array: generate alt text and extract any text
    /// </summary>
    /// <param name="imageData">Image data as byte array</param>
    /// <returns>Tuple containing both alt text and extracted text</returns>
    Task<(string AltText, string ExtractedText)> AnalyzeImageAsync(byte[] imageData);

    /// <summary>
    ///     Perform complete image analysis on byte array with content type classification
    /// </summary>
    /// <param name="imageData">Image data as byte array</param>
    /// <returns>Full analysis result including content type</returns>
    Task<ImageAnalysisResult> AnalyzeWithClassificationAsync(byte[] imageData);
}