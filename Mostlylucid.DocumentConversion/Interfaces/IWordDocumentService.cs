using Mostlylucid.DocumentConversion.Models;

namespace Mostlylucid.DocumentConversion.Interfaces;

/// <summary>
/// Service for reading and writing Word documents (.docx)
/// </summary>
public interface IWordDocumentService
{
    /// <summary>
    /// Read a Word document from a file path
    /// </summary>
    /// <param name="filePath">Path to the .docx file</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Parsed document</returns>
    Task<Document> ReadAsync(string filePath, CancellationToken cancellationToken = default);

    /// <summary>
    /// Read a Word document from a stream
    /// </summary>
    /// <param name="stream">Stream containing the .docx data</param>
    /// <param name="fileName">Optional file name for metadata</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Parsed document</returns>
    Task<Document> ReadAsync(Stream stream, string? fileName = null, CancellationToken cancellationToken = default);

    /// <summary>
    /// Read a Word document from bytes
    /// </summary>
    /// <param name="data">Byte array containing the .docx data</param>
    /// <param name="fileName">Optional file name for metadata</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Parsed document</returns>
    Task<Document> ReadAsync(byte[] data, string? fileName = null, CancellationToken cancellationToken = default);

    /// <summary>
    /// Create a Word document from a Document model
    /// </summary>
    /// <param name="document">Document to convert</param>
    /// <param name="options">Conversion options</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Word document as bytes</returns>
    Task<byte[]> CreateAsync(Document document, ConversionOptions? options = null, CancellationToken cancellationToken = default);

    /// <summary>
    /// Write a Word document to a file
    /// </summary>
    /// <param name="document">Document to write</param>
    /// <param name="filePath">Output file path</param>
    /// <param name="options">Conversion options</param>
    /// <param name="cancellationToken">Cancellation token</param>
    Task WriteAsync(Document document, string filePath, ConversionOptions? options = null, CancellationToken cancellationToken = default);

    /// <summary>
    /// Extract all text from a Word document
    /// </summary>
    /// <param name="stream">Stream containing the .docx data</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Plain text content</returns>
    Task<string> ExtractTextAsync(Stream stream, CancellationToken cancellationToken = default);

    /// <summary>
    /// Extract all images from a Word document
    /// </summary>
    /// <param name="stream">Stream containing the .docx data</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>List of extracted images</returns>
    Task<List<ImageElement>> ExtractImagesAsync(Stream stream, CancellationToken cancellationToken = default);
}
