using Mostlylucid.LLMContentModeration.Models;

namespace Mostlylucid.LLMContentModeration.Services;

/// <summary>
///     Main service for content moderation
/// </summary>
public interface IContentModerationService
{
    /// <summary>
    ///     Moderate content using configured policies
    /// </summary>
    /// <param name="content">Content to moderate</param>
    /// <param name="mode">Override moderation mode (null uses default)</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Moderation result</returns>
    Task<ModerationResult> ModerateAsync(
        string content,
        ModerationMode? mode = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    ///     Moderate content with custom options
    /// </summary>
    /// <param name="content">Content to moderate</param>
    /// <param name="customOptions">Custom moderation options</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Moderation result</returns>
    Task<ModerationResult> ModerateAsync(
        string content,
        ModerationOptions customOptions,
        CancellationToken cancellationToken = default);

    /// <summary>
    ///     Check if the moderation service is available (Ollama connectivity)
    /// </summary>
    Task<bool> IsAvailableAsync(CancellationToken cancellationToken = default);

    /// <summary>
    ///     Get service health status
    /// </summary>
    Task<ModerationServiceStatus> GetStatusAsync(CancellationToken cancellationToken = default);
}

/// <summary>
///     Status information for the moderation service
/// </summary>
public class ModerationServiceStatus
{
    public bool IsAvailable { get; set; }
    public string? OllamaEndpoint { get; set; }
    public string? OllamaModel { get; set; }
    public List<string>? AvailableModels { get; set; }
    public bool ContentClassificationEnabled { get; set; }
    public bool PiiDetectionEnabled { get; set; }
    public ModerationMode DefaultMode { get; set; }
}