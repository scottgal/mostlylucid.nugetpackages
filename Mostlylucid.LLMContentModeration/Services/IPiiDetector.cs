using Mostlylucid.LLMContentModeration.Models;

namespace Mostlylucid.LLMContentModeration.Services;

/// <summary>
/// Service for detecting and masking PII in content
/// </summary>
public interface IPiiDetector
{
    /// <summary>
    /// Detect PII in content using regex patterns
    /// </summary>
    /// <param name="content">Content to scan</param>
    /// <param name="options">Detection options</param>
    /// <returns>List of PII matches found</returns>
    List<PiiMatch> DetectPii(string content, PiiDetectionOptions options);

    /// <summary>
    /// Mask PII in content
    /// </summary>
    /// <param name="content">Original content</param>
    /// <param name="matches">PII matches to mask</param>
    /// <param name="options">Masking options</param>
    /// <returns>Content with PII masked</returns>
    string MaskPii(string content, List<PiiMatch> matches, PiiDetectionOptions options);
}
