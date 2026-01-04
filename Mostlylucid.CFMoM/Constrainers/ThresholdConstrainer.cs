using Mostlylucid.CFMoM.Aggregation;

namespace Mostlylucid.CFMoM.Constrainers;

/// <summary>
///     A simple threshold-based constrainer.
///     Makes decisions based on score thresholds.
///     Fully deterministic: same input always produces same output.
/// </summary>
public sealed class ThresholdConstrainer<TContext> : IConstrainer<TContext, CommonDecision>
{
    private readonly ThresholdConstrainerOptions _options;

    public ThresholdConstrainer(ThresholdConstrainerOptions? options = null)
    {
        _options = options ?? new ThresholdConstrainerOptions();
    }

    /// <inheritdoc />
    public ConstrainerResult<CommonDecision> Evaluate(AggregatedResult result, TContext context)
    {
        // Handle early exit first
        if (result.EarlyExit)
        {
            var earlyDecision = MapEarlyExitToDecision(result.EarlyExitClassification);
            return ConstrainerResult<CommonDecision>.Stop(
                earlyDecision,
                $"Early exit: {result.EarlyExitClassification}",
                "early-exit");
        }

        // Check immediate block threshold
        if (result.Score >= _options.ImmediateBlockThreshold)
        {
            return ConstrainerResult<CommonDecision>.Stop(
                CommonDecision.Block,
                $"Score {result.Score:F2} >= block threshold {_options.ImmediateBlockThreshold:F2}",
                "immediate-block");
        }

        // Check immediate allow threshold
        if (result.Score <= _options.ImmediateAllowThreshold && result.Confidence >= _options.MinConfidenceForAllow)
        {
            return ConstrainerResult<CommonDecision>.Stop(
                CommonDecision.Allow,
                $"Score {result.Score:F2} <= allow threshold {_options.ImmediateAllowThreshold:F2}",
                "immediate-allow");
        }

        // Check challenge threshold
        if (result.Score >= _options.ChallengeThreshold)
        {
            return ConstrainerResult<CommonDecision>.Stop(
                CommonDecision.Challenge,
                $"Score {result.Score:F2} >= challenge threshold {_options.ChallengeThreshold:F2}",
                "challenge");
        }

        // Check escalation threshold (need more analysis)
        if (result.Score >= _options.EscalateThreshold && result.Confidence < _options.MinConfidenceForDecision)
        {
            return ConstrainerResult<CommonDecision>.Continue(
                CommonDecision.Escalate,
                $"Score {result.Score:F2} with low confidence {result.Confidence:F2} - escalating");
        }

        // Default: allow but continue
        return ConstrainerResult<CommonDecision>.Continue(
            CommonDecision.Allow,
            $"Score {result.Score:F2} below all thresholds");
    }

    private CommonDecision MapEarlyExitToDecision(string? classification)
    {
        return classification?.ToLowerInvariant() switch
        {
            "verified-good" or "whitelisted" or "allow" => CommonDecision.Allow,
            "verified-bad" or "blacklisted" or "block" => CommonDecision.Block,
            "challenge" => CommonDecision.Challenge,
            _ => CommonDecision.Allow
        };
    }
}

/// <summary>
///     Options for the threshold constrainer.
/// </summary>
public sealed class ThresholdConstrainerOptions
{
    /// <summary>
    ///     Score threshold for immediate block. Default: 0.85
    /// </summary>
    public double ImmediateBlockThreshold { get; set; } = 0.85;

    /// <summary>
    ///     Score threshold for immediate allow. Default: 0.15
    /// </summary>
    public double ImmediateAllowThreshold { get; set; } = 0.15;

    /// <summary>
    ///     Score threshold for challenge. Default: 0.70
    /// </summary>
    public double ChallengeThreshold { get; set; } = 0.70;

    /// <summary>
    ///     Score threshold for escalation. Default: 0.40
    /// </summary>
    public double EscalateThreshold { get; set; } = 0.40;

    /// <summary>
    ///     Minimum confidence required for allow decision. Default: 0.5
    /// </summary>
    public double MinConfidenceForAllow { get; set; } = 0.5;

    /// <summary>
    ///     Minimum confidence required for any definitive decision. Default: 0.3
    /// </summary>
    public double MinConfidenceForDecision { get; set; } = 0.3;
}
