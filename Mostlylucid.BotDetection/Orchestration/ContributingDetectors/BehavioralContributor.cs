using System.Collections.Immutable;
using Microsoft.Extensions.Logging;
using Mostlylucid.BotDetection.Detectors;
using Mostlylucid.BotDetection.Models;

namespace Mostlylucid.BotDetection.Orchestration.ContributingDetectors;

/// <summary>
///     Behavioral analysis contributor - detects bots based on request patterns.
///     Runs in Wave 0 (no dependencies) to track all requests.
/// </summary>
public class BehavioralContributor : ContributingDetectorBase
{
    private readonly BehavioralDetector _detector;
    private readonly ILogger<BehavioralContributor> _logger;

    public BehavioralContributor(
        ILogger<BehavioralContributor> logger,
        BehavioralDetector detector)
    {
        _logger = logger;
        _detector = detector;
    }

    public override string Name => "Behavioral";
    public override int Priority => 20; // Run early but after basic detectors

    // No triggers - runs in first wave to track all requests
    public override IReadOnlyList<TriggerCondition> TriggerConditions => Array.Empty<TriggerCondition>();

    public override async Task<IReadOnlyList<DetectionContribution>> ContributeAsync(
        BlackboardState state,
        CancellationToken cancellationToken = default)
    {
        var contributions = new List<DetectionContribution>();

        try
        {
            var result = await _detector.DetectAsync(state.HttpContext, cancellationToken);

            if (result.Reasons.Count == 0)
                // No behavioral issues detected - add negative signal (human indicator)
                contributions.Add(new DetectionContribution
                {
                    DetectorName = Name,
                    Category = "Behavioral",
                    ConfidenceDelta = -0.1,
                    Weight = 1.0,
                    Reason = "Request patterns appear normal",
                    Signals = ImmutableDictionary<string, object>.Empty
                        .Add(SignalKeys.BehavioralAnomalyDetected, false)
                });
            else
                // Convert each reason to a contribution
                foreach (var reason in result.Reasons)
                    contributions.Add(new DetectionContribution
                    {
                        DetectorName = Name,
                        Category = reason.Category,
                        ConfidenceDelta = reason.ConfidenceImpact,
                        Weight = 1.5, // Behavioral is a strong signal
                        Reason = reason.Detail,
                        BotType = result.BotType,
                        Signals = ImmutableDictionary<string, object>.Empty
                            .Add(SignalKeys.BehavioralAnomalyDetected, true)
                            .Add(SignalKeys.BehavioralRateExceeded,
                                reason.Detail.Contains("rate", StringComparison.OrdinalIgnoreCase))
                    });
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Behavioral detection failed");
        }

        return contributions;
    }
}