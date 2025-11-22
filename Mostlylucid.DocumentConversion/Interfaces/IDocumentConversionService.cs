using Mostlylucid.DocumentConversion.Models;

namespace Mostlylucid.DocumentConversion.Interfaces;

/// <summary>
/// Main document conversion service that orchestrates all format conversions
/// </summary>
public interface IDocumentConversionService
{
    /// <summary>
    /// Read a document from any supported format
    /// </summary>
    /// <param name="stream">Document stream</param>
    /// <param name="fileName">File name (used to determine format)</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Parsed document</returns>
    Task<Document> ReadDocumentAsync(Stream stream, string fileName, CancellationToken cancellationToken = default);

    /// <summary>
    /// Read a document from a file path
    /// </summary>
    /// <param name="filePath">Path to the document</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Parsed document</returns>
    Task<Document> ReadDocumentAsync(string filePath, CancellationToken cancellationToken = default);

    /// <summary>
    /// Read a document from bytes
    /// </summary>
    /// <param name="data">Document data</param>
    /// <param name="fileName">File name (used to determine format)</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Parsed document</returns>
    Task<Document> ReadDocumentAsync(byte[] data, string fileName, CancellationToken cancellationToken = default);

    /// <summary>
    /// Convert a document to another format
    /// </summary>
    /// <param name="document">Source document</param>
    /// <param name="targetFormat">Target format</param>
    /// <param name="options">Conversion options</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Conversion result</returns>
    Task<ConversionResult> ConvertAsync(Document document, DocFormat targetFormat, ConversionOptions? options = null, CancellationToken cancellationToken = default);

    /// <summary>
    /// Convert a document from one format to another (file to file)
    /// </summary>
    /// <param name="inputPath">Input file path</param>
    /// <param name="outputPath">Output file path</param>
    /// <param name="options">Conversion options</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Conversion result</returns>
    Task<ConversionResult> ConvertFileAsync(string inputPath, string outputPath, ConversionOptions? options = null, CancellationToken cancellationToken = default);

    /// <summary>
    /// Convert Word document to Markdown
    /// </summary>
    /// <param name="wordStream">Word document stream</param>
    /// <param name="options">Conversion options</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Markdown string</returns>
    Task<string> WordToMarkdownAsync(Stream wordStream, ConversionOptions? options = null, CancellationToken cancellationToken = default);

    /// <summary>
    /// Convert Word document to PDF
    /// </summary>
    /// <param name="wordStream">Word document stream</param>
    /// <param name="options">Conversion options</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>PDF bytes</returns>
    Task<byte[]> WordToPdfAsync(Stream wordStream, ConversionOptions? options = null, CancellationToken cancellationToken = default);

    /// <summary>
    /// Convert Markdown to Word document
    /// </summary>
    /// <param name="markdown">Markdown content</param>
    /// <param name="options">Conversion options</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Word document bytes</returns>
    Task<byte[]> MarkdownToWordAsync(string markdown, ConversionOptions? options = null, CancellationToken cancellationToken = default);

    /// <summary>
    /// Convert Markdown to PDF
    /// </summary>
    /// <param name="markdown">Markdown content</param>
    /// <param name="options">Conversion options</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>PDF bytes</returns>
    Task<byte[]> MarkdownToPdfAsync(string markdown, ConversionOptions? options = null, CancellationToken cancellationToken = default);

    /// <summary>
    /// Extract all elements from a document
    /// </summary>
    /// <param name="stream">Document stream</param>
    /// <param name="fileName">File name</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>List of document elements</returns>
    Task<List<DocumentElement>> ExtractElementsAsync(Stream stream, string fileName, CancellationToken cancellationToken = default);

    /// <summary>
    /// Extract text from a document
    /// </summary>
    /// <param name="stream">Document stream</param>
    /// <param name="fileName">File name</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Plain text content</returns>
    Task<string> ExtractTextAsync(Stream stream, string fileName, CancellationToken cancellationToken = default);

    /// <summary>
    /// Extract images from a document
    /// </summary>
    /// <param name="stream">Document stream</param>
    /// <param name="fileName">File name</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>List of images</returns>
    Task<List<ImageElement>> ExtractImagesAsync(Stream stream, string fileName, CancellationToken cancellationToken = default);

    /// <summary>
    /// Determine the document format from a file name
    /// </summary>
    /// <param name="fileName">File name or path</param>
    /// <returns>Detected format</returns>
    DocFormat DetectFormat(string fileName);

    /// <summary>
    /// Check if a format is supported for reading
    /// </summary>
    /// <param name="format">Format to check</param>
    /// <returns>True if supported</returns>
    bool CanRead(DocFormat format);

    /// <summary>
    /// Check if a format is supported for writing
    /// </summary>
    /// <param name="format">Format to check</param>
    /// <returns>True if supported</returns>
    bool CanWrite(DocFormat format);
}
