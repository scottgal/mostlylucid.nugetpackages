using System.Collections.Immutable;
using Microsoft.Extensions.Logging;
using Mostlylucid.BotDetection.Detectors;
using Mostlylucid.BotDetection.Models;

namespace Mostlylucid.BotDetection.Orchestration.ContributingDetectors;

/// <summary>
///     Heuristic model contributor - uses learned weights for bot classification.
///     Runs in Wave 1+ after initial detectors have run.
/// </summary>
public class HeuristicContributor : ContributingDetectorBase
{
    private readonly HeuristicDetector _detector;
    private readonly ILogger<HeuristicContributor> _logger;

    public HeuristicContributor(
        ILogger<HeuristicContributor> logger,
        HeuristicDetector detector)
    {
        _logger = logger;
        _detector = detector;
    }

    public override string Name => "Heuristic";
    public override int Priority => 50; // Run after basic detectors

    // Trigger when we have enough signals and want heuristic classification
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
                // Heuristic disabled or skipped
                contributions.Add(new DetectionContribution
                {
                    DetectorName = Name,
                    Category = "HeuristicEarly",
                    ConfidenceDelta = 0,
                    Weight = 0,
                    Reason = "Heuristic detection disabled or skipped",
                    Signals = ImmutableDictionary<string, object>.Empty
                });
            }
            else
            {
                // Heuristic made a prediction (use reason's ConfidenceImpact which is negative for human)
                var reason = result.Reasons.First();
                var isBot = reason.ConfidenceImpact > 0;

                contributions.Add(new DetectionContribution
                {
                    DetectorName = Name,
                    Category = "HeuristicEarly",
                    ConfidenceDelta = reason.ConfidenceImpact,
                    Weight = 2.0, // Heuristic predictions are weighted heavily
                    Reason = reason.Detail,
                    BotType = result.BotType,
                    BotName = result.BotName,
                    Signals = ImmutableDictionary<string, object>.Empty
                        .Add(SignalKeys.HeuristicPrediction, isBot ? "bot" : "human")
                        .Add(SignalKeys.HeuristicConfidence, result.Confidence)
                        .Add(SignalKeys.HeuristicEarlyCompleted, true) // Signal for late heuristic
                });
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Heuristic detection failed");
        }

        return contributions;
    }
}