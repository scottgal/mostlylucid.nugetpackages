using Mostlylucid.LlmPiiRedactor.Models;

namespace Mostlylucid.LlmPiiRedactor.Services;

/// <summary>
///     Service for detecting and redacting PII from text.
/// </summary>
public interface IPiiRedactionService
{
    /// <summary>
    ///     Redact PII from the given text.
    /// </summary>
    /// <param name="text">Text to redact.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Result containing redacted text and match information.</returns>
    RedactionResult Redact(string text, CancellationToken cancellationToken = default);

    /// <summary>
    ///     Redact PII from the given text asynchronously.
    /// </summary>
    Task<RedactionResult> RedactAsync(string text, CancellationToken cancellationToken = default);

    /// <summary>
    ///     Detect PII in the given text without redacting.
    /// </summary>
    /// <param name="text">Text to scan.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Collection of detected PII matches.</returns>
    IReadOnlyList<PiiMatch> Detect(string text, CancellationToken cancellationToken = default);

    /// <summary>
    ///     Check if text contains any PII.
    /// </summary>
    bool ContainsPii(string text, CancellationToken cancellationToken = default);

    /// <summary>
    ///     Get redaction statistics.
    /// </summary>
    PiiRedactionStatistics GetStatistics();
}

/// <summary>
///     Statistics about PII redaction operations.
/// </summary>
public class PiiRedactionStatistics
{
    public long TotalScans { get; set; }
    public long TotalRedactions { get; set; }
    public Dictionary<PiiType, long> RedactionsByType { get; set; } = new();
    public long TotalCharactersScanned { get; set; }
    public long TotalCharactersRedacted { get; set; }
}