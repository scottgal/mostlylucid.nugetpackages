using Microsoft.AspNetCore.Http;
using Mostlylucid.BotDetection.Models;

namespace Mostlylucid.BotDetection.Detectors;

/// <summary>
///     Interface for bot detection strategies
/// </summary>
public interface IDetector
{
    /// <summary>
    ///     Name of the detector
    /// </summary>
    string Name { get; }

    /// <summary>
    ///     Analyze an HTTP request for bot characteristics
    /// </summary>
    /// <param name="context">HTTP context</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Detection result with confidence score and reasons</returns>
    Task<DetectorResult> DetectAsync(HttpContext context, CancellationToken cancellationToken = default);
}

/// <summary>
///     Result from an individual detector
/// </summary>
public class DetectorResult
{
    /// <summary>
    ///     Confidence score from this detector (0.0 to 1.0)
    /// </summary>
    public double Confidence { get; set; }

    /// <summary>
    ///     Reasons found by this detector
    /// </summary>
    public List<DetectionReason> Reasons { get; set; } = new();

    /// <summary>
    ///     Bot type if identified
    /// </summary>
    public BotType? BotType { get; set; }

    /// <summary>
    ///     Bot name if known
    /// </summary>
    public string? BotName { get; set; }
}