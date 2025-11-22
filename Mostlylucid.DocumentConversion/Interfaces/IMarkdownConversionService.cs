using Mostlylucid.DocumentConversion.Models;

namespace Mostlylucid.DocumentConversion.Interfaces;

/// <summary>
/// Service for converting to and from Markdown format
/// </summary>
public interface IMarkdownConversionService
{
    /// <summary>
    /// Parse Markdown content into a Document model
    /// </summary>
    /// <param name="markdown">Markdown content</param>
    /// <param name="fileName">Optional file name for metadata</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Parsed document</returns>
    Task<Document> ParseAsync(string markdown, string? fileName = null, CancellationToken cancellationToken = default);

    /// <summary>
    /// Read Markdown from a file
    /// </summary>
    /// <param name="filePath">Path to the .md file</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Parsed document</returns>
    Task<Document> ReadAsync(string filePath, CancellationToken cancellationToken = default);

    /// <summary>
    /// Convert a Document to Markdown format
    /// </summary>
    /// <param name="document">Document to convert</param>
    /// <param name="options">Conversion options</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Markdown string</returns>
    Task<string> ToMarkdownAsync(Document document, ConversionOptions? options = null, CancellationToken cancellationToken = default);

    /// <summary>
    /// Convert Markdown to HTML
    /// </summary>
    /// <param name="markdown">Markdown content</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>HTML string</returns>
    Task<string> ToHtmlAsync(string markdown, CancellationToken cancellationToken = default);

    /// <summary>
    /// Convert a Document to HTML
    /// </summary>
    /// <param name="document">Document to convert</param>
    /// <param name="options">Conversion options</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>HTML string</returns>
    Task<string> ToHtmlAsync(Document document, ConversionOptions? options = null, CancellationToken cancellationToken = default);

    /// <summary>
    /// Convert Markdown to plain text
    /// </summary>
    /// <param name="markdown">Markdown content</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Plain text string</returns>
    Task<string> ToPlainTextAsync(string markdown, CancellationToken cancellationToken = default);
}
