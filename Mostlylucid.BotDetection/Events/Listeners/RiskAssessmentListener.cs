using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Mostlylucid.BotDetection.Models;

namespace Mostlylucid.BotDetection.Events.Listeners;

/// <summary>
///     Listens for Finalising signal to compute final risk assessment.
///     Aggregates all prior signals into RiskBand and RecommendedAction.
/// </summary>
public class RiskAssessmentListener : IBotSignalListener, ISignalSubscriber
{
    private readonly ILogger<RiskAssessmentListener> _logger;
    private readonly BotDetectionOptions _options;

    public RiskAssessmentListener(
        ILogger<RiskAssessmentListener> logger,
        IOptions<BotDetectionOptions> options)
    {
        _logger = logger;
        _options = options.Value;
    }

    public IEnumerable<BotSignalType> SubscribedSignals => new[] { BotSignalType.Finalising };

    public ValueTask OnSignalAsync(
        BotSignalType signal,
        DetectionContext context,
        CancellationToken ct = default)
    {
        if (signal != BotSignalType.Finalising)
            return ValueTask.CompletedTask;

        // Read all accumulated scores
        var scores = context.Scores;
        var maxScore = scores.Values.DefaultIfEmpty(0).Max();
        var avgScore = scores.Values.DefaultIfEmpty(0).Average();

        // Check for specific signals
        var inconsistencyScore = context.GetSignal<double>(SignalKeys.InconsistencyScore);
        var headlessLikelihood = context.GetSignal<double>(SignalKeys.FingerprintHeadlessScore);
        var isDatacenter = context.GetSignal<bool>(SignalKeys.IpIsDatacenter);

        // Compute risk band
        var riskBand = ComputeRiskBand(maxScore, inconsistencyScore, headlessLikelihood, isDatacenter);
        var recommendedAction = MapRiskToAction(riskBand);

        // Store in context for result building
        context.SetSignal(SignalKeys.RiskBand, riskBand);
        context.SetSignal(SignalKeys.RiskScore, maxScore);

        _logger.LogDebug(
            "Risk assessment complete: band={Band}, action={Action}, maxScore={Score:F2}",
            riskBand, recommendedAction, maxScore);

        return ValueTask.CompletedTask;
    }

    private RiskBand ComputeRiskBand(
        double maxScore,
        double? inconsistencyScore,
        double? headlessLikelihood,
        bool? isDatacenter)
    {
        // Base band from max score
        var band = maxScore switch
        {
            >= 0.9 => RiskBand.High,
            >= 0.7 => RiskBand.Medium,
            >= 0.5 => RiskBand.Elevated,
            _ => RiskBand.Low
        };

        // Boost if multiple signals agree
        var boostCount = 0;
        if (inconsistencyScore > 0.5) boostCount++;
        if (headlessLikelihood > 0.7) boostCount++;
        if (isDatacenter == true) boostCount++;

        // Multi-signal agreement can boost one level
        if (boostCount >= 2 && band < RiskBand.High)
        {
            band = (RiskBand)((int)band + 1);
        }

        return band;
    }

    private RecommendedAction MapRiskToAction(RiskBand band) => band switch
    {
        RiskBand.High => RecommendedAction.Block,
        RiskBand.Medium => RecommendedAction.Challenge,
        RiskBand.Elevated => RecommendedAction.Throttle,
        _ => RecommendedAction.Allow
    };
}

/// <summary>
///     Risk bands for categorizing requests
/// </summary>
public enum RiskBand
{
    Low = 0,
    Elevated = 1,
    Medium = 2,
    High = 3
}

/// <summary>
///     Recommended actions based on risk
/// </summary>
public enum RecommendedAction
{
    Allow,
    Throttle,
    Challenge,
    Block
}
