using Mostlylucid.LlmI18nAssistant.Models;

namespace Mostlylucid.LlmI18nAssistant.Services;

/// <summary>
///     Interface for parsing resource files
/// </summary>
public interface IResourceFileParser
{
    /// <summary>
    ///     Parses a resource file from a file path
    /// </summary>
    /// <param name="filePath">Path to the resource file</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Parsed resource file</returns>
    Task<ResourceFile> ParseAsync(string filePath, CancellationToken cancellationToken = default);

    /// <summary>
    ///     Parses a resource file from a stream
    /// </summary>
    /// <param name="stream">Stream containing the resource file</param>
    /// <param name="fileType">Type of resource file</param>
    /// <param name="fileName">Optional file name for reference</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Parsed resource file</returns>
    Task<ResourceFile> ParseAsync(Stream stream, ResourceFileType fileType, string? fileName = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    ///     Writes a translated resource file to a file path
    /// </summary>
    /// <param name="result">Translation result</param>
    /// <param name="outputPath">Output file path</param>
    /// <param name="cancellationToken">Cancellation token</param>
    Task WriteAsync(TranslationResult result, string outputPath, CancellationToken cancellationToken = default);

    /// <summary>
    ///     Writes a translated resource file to a stream
    /// </summary>
    /// <param name="result">Translation result</param>
    /// <param name="stream">Output stream</param>
    /// <param name="fileType">Output file type</param>
    /// <param name="cancellationToken">Cancellation token</param>
    Task WriteAsync(TranslationResult result, Stream stream, ResourceFileType fileType,
        CancellationToken cancellationToken = default);

    /// <summary>
    ///     Determines the file type from a file path
    /// </summary>
    /// <param name="filePath">File path</param>
    /// <returns>Resource file type</returns>
    ResourceFileType GetFileType(string filePath);

    /// <summary>
    ///     Checks if the parser supports the given file type
    /// </summary>
    /// <param name="fileType">File type to check</param>
    /// <returns>True if supported</returns>
    bool Supports(ResourceFileType fileType);
}