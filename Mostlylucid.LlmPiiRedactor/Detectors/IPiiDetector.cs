using Mostlylucid.LlmPiiRedactor.Models;

namespace Mostlylucid.LlmPiiRedactor.Detectors;

/// <summary>
///     Interface for PII detection implementations.
/// </summary>
public interface IPiiDetector
{
    /// <summary>
    ///     The type of PII this detector handles.
    /// </summary>
    PiiType PiiType { get; }

    /// <summary>
    ///     Name of this detector for logging/debugging.
    /// </summary>
    string Name { get; }

    /// <summary>
    ///     Priority for detection order (lower = higher priority).
    /// </summary>
    int Priority { get; }

    /// <summary>
    ///     Whether this detector is enabled.
    /// </summary>
    bool IsEnabled { get; }

    /// <summary>
    ///     Detect PII matches in the given text.
    /// </summary>
    /// <param name="text">Text to scan for PII.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Collection of PII matches found.</returns>
    IEnumerable<PiiMatch> Detect(string text, CancellationToken cancellationToken = default);
}