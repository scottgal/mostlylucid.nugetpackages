using System.Collections.Immutable;
using Microsoft.Extensions.Logging;
using Mostlylucid.BotDetection.Detectors;
using Mostlylucid.BotDetection.Models;

namespace Mostlylucid.BotDetection.Orchestration.ContributingDetectors;

/// <summary>
///     ONNX ML model contributor - uses trained ML model for bot classification.
///     Runs in Wave 1+ after initial detectors have run and when AI escalation is triggered.
/// </summary>
public class OnnxContributor : ContributingDetectorBase
{
    private readonly ILogger<OnnxContributor> _logger;
    private readonly OnnxDetector _detector;

    public OnnxContributor(
        ILogger<OnnxContributor> logger,
        OnnxDetector detector)
    {
        _logger = logger;
        _detector = detector;
    }

    public override string Name => "Onnx";
    public override int Priority => 50; // Run after basic detectors

    // Trigger when we have enough signals and want AI classification
    public override IReadOnlyList<TriggerCondition> TriggerConditions =>
    [
        // Run when UserAgent signal exists (basic info available)
        Triggers.WhenSignalExists(SignalKeys.UserAgent)
    ];

    public override async Task<IReadOnlyList<DetectionContribution>> ContributeAsync(
        BlackboardState state,
        CancellationToken cancellationToken = default)
    {
        var contributions = new List<DetectionContribution>();

        try
        {
            var result = await _detector.DetectAsync(state.HttpContext, cancellationToken);

            if (result.Reasons.Count == 0)
            {
                // ONNX disabled or skipped
                contributions.Add(new DetectionContribution
                {
                    DetectorName = Name,
                    Category = "AI",
                    ConfidenceDelta = 0,
                    Weight = 0,
                    Reason = "ONNX detection disabled or skipped",
                    Signals = ImmutableDictionary<string, object>.Empty
                });
            }
            else
            {
                // ONNX made a prediction (use reason's ConfidenceImpact which is negative for human)
                var reason = result.Reasons.First();
                var isBot = reason.ConfidenceImpact > 0;

                contributions.Add(new DetectionContribution
                {
                    DetectorName = Name,
                    Category = "AI",
                    ConfidenceDelta = reason.ConfidenceImpact,
                    Weight = 2.0, // AI predictions are weighted heavily
                    Reason = reason.Detail,
                    BotType = result.BotType,
                    BotName = result.BotName,
                    Signals = ImmutableDictionary<string, object>.Empty
                        .Add(SignalKeys.AiPrediction, isBot ? "bot" : "human")
                        .Add(SignalKeys.AiConfidence, result.Confidence)
                });
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "ONNX detection failed");
        }

        return contributions;
    }
}
