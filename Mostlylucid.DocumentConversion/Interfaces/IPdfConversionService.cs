using Mostlylucid.DocumentConversion.Models;

namespace Mostlylucid.DocumentConversion.Interfaces;

/// <summary>
/// Service for generating PDF documents
/// </summary>
public interface IPdfConversionService
{
    /// <summary>
    /// Convert a Document to PDF format
    /// </summary>
    /// <param name="document">Document to convert</param>
    /// <param name="options">Conversion options</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>PDF as bytes</returns>
    Task<byte[]> ToPdfAsync(Document document, ConversionOptions? options = null, CancellationToken cancellationToken = default);

    /// <summary>
    /// Convert a Document to PDF and save to file
    /// </summary>
    /// <param name="document">Document to convert</param>
    /// <param name="filePath">Output file path</param>
    /// <param name="options">Conversion options</param>
    /// <param name="cancellationToken">Cancellation token</param>
    Task ToPdfFileAsync(Document document, string filePath, ConversionOptions? options = null, CancellationToken cancellationToken = default);

    /// <summary>
    /// Convert Markdown content directly to PDF
    /// </summary>
    /// <param name="markdown">Markdown content</param>
    /// <param name="options">Conversion options</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>PDF as bytes</returns>
    Task<byte[]> MarkdownToPdfAsync(string markdown, ConversionOptions? options = null, CancellationToken cancellationToken = default);

    /// <summary>
    /// Convert HTML content to PDF
    /// </summary>
    /// <param name="html">HTML content</param>
    /// <param name="options">Conversion options</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>PDF as bytes</returns>
    Task<byte[]> HtmlToPdfAsync(string html, ConversionOptions? options = null, CancellationToken cancellationToken = default);
}
