using Mostlylucid.LLMContentModeration.Models;

namespace Mostlylucid.LLMContentModeration.Services;

/// <summary>
///     Client interface for Ollama-based content moderation
/// </summary>
public interface IModerationOllamaClient
{
    /// <summary>
    ///     Classify content for toxicity, spam, self-harm, and NSFW
    /// </summary>
    /// <param name="content">Content to classify</param>
    /// <param name="options">Classification options</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>List of content flags found</returns>
    Task<List<ContentFlag>> ClassifyContentAsync(
        string content,
        ContentClassificationOptions options,
        CancellationToken cancellationToken = default);

    /// <summary>
    ///     Enhance PII detection using LLM analysis
    /// </summary>
    /// <param name="content">Content to analyze</param>
    /// <param name="regexMatches">Matches already found via regex</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Enhanced list of PII matches</returns>
    Task<List<PiiMatch>> EnhancePiiDetectionAsync(
        string content,
        List<PiiMatch> regexMatches,
        CancellationToken cancellationToken = default);

    /// <summary>
    ///     Check if the Ollama service is available
    /// </summary>
    Task<bool> IsAvailableAsync(CancellationToken cancellationToken = default);

    /// <summary>
    ///     Get available models from Ollama
    /// </summary>
    Task<List<string>> GetModelsAsync(CancellationToken cancellationToken = default);
}