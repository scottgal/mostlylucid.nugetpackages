using System.Collections.Immutable;
using Mostlylucid.BotDetection.Models;
using Mostlylucid.BotDetection.Policies;

namespace Mostlylucid.BotDetection.Orchestration;

/// <summary>
///     First-class evidence object emitted by detectors.
///     Immutable, auditable, and composable.
///
///     Detectors emit contributions (evidence), not verdicts.
///     The orchestrator aggregates contributions into a final risk decision.
/// </summary>
public sealed record DetectionContribution
{
    /// <summary>
    ///     Which detector emitted this contribution
    /// </summary>
    public required string DetectorName { get; init; }

    /// <summary>
    ///     Category of evidence (e.g., "UserAgent", "Behavioral", "Inconsistency")
    /// </summary>
    public required string Category { get; init; }

    /// <summary>
    ///     Confidence delta: positive = more likely bot, negative = more likely human
    ///     Range: -1.0 to +1.0
    /// </summary>
    public required double ConfidenceDelta { get; init; }

    /// <summary>
    ///     Weight of this contribution in the final score.
    ///     Higher weight = more influence on final decision.
    ///     Default: 1.0
    /// </summary>
    public double Weight { get; init; } = 1.0;

    /// <summary>
    ///     Human-readable reason for this contribution
    /// </summary>
    public required string Reason { get; init; }

    /// <summary>
    ///     Detailed explanation (for logging/UI)
    /// </summary>
    public string? Detail { get; init; }

    /// <summary>
    ///     Bot type if this contribution identifies a specific type
    /// </summary>
    public BotType? BotType { get; init; }

    /// <summary>
    ///     Bot name if known (e.g., "Googlebot", "BadBot/1.0")
    /// </summary>
    public string? BotName { get; init; }

    /// <summary>
    ///     Signals produced by this contribution (for triggering other detectors)
    /// </summary>
    public ImmutableDictionary<string, object> Signals { get; init; } =
        ImmutableDictionary<string, object>.Empty;

    /// <summary>
    ///     Timestamp when this contribution was created
    /// </summary>
    public DateTimeOffset Timestamp { get; init; } = DateTimeOffset.UtcNow;

    /// <summary>
    ///     Processing time in milliseconds for this detector
    /// </summary>
    public double ProcessingTimeMs { get; init; }

    /// <summary>
    ///     Whether this contribution should trigger early exit (e.g., verified good bot)
    /// </summary>
    public bool TriggerEarlyExit { get; init; }

    /// <summary>
    ///     If early exit, what the final verdict should be
    /// </summary>
    public EarlyExitVerdict? EarlyExitVerdict { get; init; }

    /// <summary>
    ///     Helper to create a bot evidence contribution
    /// </summary>
    public static DetectionContribution Bot(
        string detector,
        string category,
        double confidence,
        string reason,
        BotType? botType = null,
        string? botName = null,
        double weight = 1.0) => new()
    {
        DetectorName = detector,
        Category = category,
        ConfidenceDelta = Math.Clamp(confidence, 0, 1),
        Weight = weight,
        Reason = reason,
        BotType = botType,
        BotName = botName
    };

    /// <summary>
    ///     Helper to create a human evidence contribution
    /// </summary>
    public static DetectionContribution Human(
        string detector,
        string category,
        double confidence,
        string reason,
        double weight = 1.0) => new()
    {
        DetectorName = detector,
        Category = category,
        ConfidenceDelta = -Math.Clamp(confidence, 0, 1),
        Weight = weight,
        Reason = reason
    };

    /// <summary>
    ///     Helper to create a neutral/informational contribution
    /// </summary>
    public static DetectionContribution Info(
        string detector,
        string category,
        string reason,
        ImmutableDictionary<string, object>? signals = null) => new()
    {
        DetectorName = detector,
        Category = category,
        ConfidenceDelta = 0,
        Weight = 0,
        Reason = reason,
        Signals = signals ?? ImmutableDictionary<string, object>.Empty
    };

    /// <summary>
    ///     Helper to create an early exit contribution (verified good bot)
    /// </summary>
    public static DetectionContribution VerifiedGoodBot(
        string detector,
        string botName,
        string reason) => new()
    {
        DetectorName = detector,
        Category = "Verification",
        ConfidenceDelta = 0,
        Weight = 0,
        Reason = reason,
        BotName = botName,
        BotType = Models.BotType.SearchEngine,
        TriggerEarlyExit = true,
        EarlyExitVerdict = Orchestration.EarlyExitVerdict.VerifiedGoodBot
    };

    /// <summary>
    ///     Helper to create an early exit contribution (verified bad bot)
    /// </summary>
    public static DetectionContribution VerifiedBadBot(
        string detector,
        string botName,
        string reason,
        BotType botType = Models.BotType.MaliciousBot) => new()
    {
        DetectorName = detector,
        Category = "Verification",
        ConfidenceDelta = 1.0,
        Weight = 10.0, // High weight for verified bad
        Reason = reason,
        BotName = botName,
        BotType = botType,
        TriggerEarlyExit = true,
        EarlyExitVerdict = Orchestration.EarlyExitVerdict.VerifiedBadBot
    };
}

/// <summary>
///     Verdict for early exit scenarios
/// </summary>
public enum EarlyExitVerdict
{
    /// <summary>Verified good bot (e.g., Googlebot with valid DNS)</summary>
    VerifiedGoodBot,

    /// <summary>Verified bad bot (e.g., known malicious signature)</summary>
    VerifiedBadBot,

    /// <summary>Whitelisted client (IP, UA, etc.)</summary>
    Whitelisted,

    /// <summary>Blacklisted client</summary>
    Blacklisted
}

/// <summary>
///     Aggregated result from all contributions
/// </summary>
public sealed record AggregatedEvidence
{
    /// <summary>
    ///     All contributions received
    /// </summary>
    public required IReadOnlyList<DetectionContribution> Contributions { get; init; }

    /// <summary>
    ///     Final aggregated bot probability (0.0 = human, 1.0 = bot)
    /// </summary>
    public required double BotProbability { get; init; }

    /// <summary>
    ///     Confidence in the final decision (based on evidence strength)
    /// </summary>
    public required double Confidence { get; init; }

    /// <summary>
    ///     Risk band based on bot probability
    /// </summary>
    public required RiskBand RiskBand { get; init; }

    /// <summary>
    ///     Whether an early exit was triggered
    /// </summary>
    public bool EarlyExit { get; init; }

    /// <summary>
    ///     Early exit verdict if applicable
    /// </summary>
    public EarlyExitVerdict? EarlyExitVerdict { get; init; }

    /// <summary>
    ///     Primary bot type if identified
    /// </summary>
    public BotType? PrimaryBotType { get; init; }

    /// <summary>
    ///     Primary bot name if identified
    /// </summary>
    public string? PrimaryBotName { get; init; }

    /// <summary>
    ///     All signals collected from contributions
    /// </summary>
    public required IReadOnlyDictionary<string, object> Signals { get; init; }

    /// <summary>
    ///     Total processing time in milliseconds
    /// </summary>
    public double TotalProcessingTimeMs { get; init; }

    /// <summary>
    ///     Breakdown by category for explainability
    /// </summary>
    public required IReadOnlyDictionary<string, CategoryScore> CategoryBreakdown { get; init; }

    /// <summary>
    ///     Which detectors contributed
    /// </summary>
    public required IReadOnlySet<string> ContributingDetectors { get; init; }

    /// <summary>
    ///     Which detectors failed or timed out
    /// </summary>
    public IReadOnlySet<string> FailedDetectors { get; init; } = new HashSet<string>();

    /// <summary>
    ///     Policy that was used for this detection
    /// </summary>
    public string? PolicyName { get; init; }

    /// <summary>
    ///     Action determined by policy (if any)
    /// </summary>
    public PolicyAction? PolicyAction { get; init; }
}

/// <summary>
///     Score breakdown for a single category
/// </summary>
public sealed record CategoryScore
{
    public required string Category { get; init; }
    public required double Score { get; init; }
    public required double Weight { get; init; }
    public required int ContributionCount { get; init; }
    public required IReadOnlyList<string> Reasons { get; init; }
}

/// <summary>
///     Risk bands for final classification
/// </summary>
public enum RiskBand
{
    /// <summary>Very low risk - likely human</summary>
    VeryLow,

    /// <summary>Low risk - probably human</summary>
    Low,

    /// <summary>Medium risk - uncertain</summary>
    Medium,

    /// <summary>High risk - probably bot</summary>
    High,

    /// <summary>Very high risk - almost certainly bot</summary>
    VeryHigh,

    /// <summary>Verified - confirmed by external verification</summary>
    Verified
}

/// <summary>
///     Aggregates contributions into a final evidence result.
///     Uses weighted averaging with configurable thresholds.
/// </summary>
public class EvidenceAggregator
{
    private readonly List<DetectionContribution> _contributions = new();
    private readonly Dictionary<string, object> _signals = new();
    private readonly HashSet<string> _failedDetectors = new();
    private readonly object _lock = new();

    private DetectionContribution? _earlyExitContribution;

    /// <summary>
    ///     Add a contribution from a detector
    /// </summary>
    public void AddContribution(DetectionContribution contribution)
    {
        lock (_lock)
        {
            _contributions.Add(contribution);

            // Merge signals
            foreach (var signal in contribution.Signals)
            {
                _signals[signal.Key] = signal.Value;
            }

            // Check for early exit
            if (contribution.TriggerEarlyExit && _earlyExitContribution == null)
            {
                _earlyExitContribution = contribution;
            }
        }
    }

    /// <summary>
    ///     Record a failed detector
    /// </summary>
    public void RecordFailure(string detectorName)
    {
        lock (_lock)
        {
            _failedDetectors.Add(detectorName);
        }
    }

    /// <summary>
    ///     Check if early exit should be triggered
    /// </summary>
    public bool ShouldEarlyExit
    {
        get
        {
            lock (_lock)
            {
                return _earlyExitContribution != null;
            }
        }
    }

    /// <summary>
    ///     Get the early exit contribution if any
    /// </summary>
    public DetectionContribution? EarlyExitContribution
    {
        get
        {
            lock (_lock)
            {
                return _earlyExitContribution;
            }
        }
    }

    /// <summary>
    ///     Get current signals (for triggering reactive detectors)
    /// </summary>
    public IReadOnlyDictionary<string, object> CurrentSignals
    {
        get
        {
            lock (_lock)
            {
                return new Dictionary<string, object>(_signals);
            }
        }
    }

    /// <summary>
    ///     Aggregate all contributions into a final result
    /// </summary>
    public AggregatedEvidence Aggregate()
    {
        lock (_lock)
        {
            // Handle early exit
            if (_earlyExitContribution != null)
            {
                return CreateEarlyExitResult(_earlyExitContribution);
            }

            // Calculate weighted score
            var (botProbability, confidence) = CalculateWeightedScore();

            // Determine risk band
            var riskBand = DetermineRiskBand(botProbability, confidence);

            // Find primary bot type/name
            var (primaryType, primaryName) = FindPrimaryBot();

            // Build category breakdown
            var categoryBreakdown = BuildCategoryBreakdown();

            return new AggregatedEvidence
            {
                Contributions = _contributions.ToList(),
                BotProbability = botProbability,
                Confidence = confidence,
                RiskBand = riskBand,
                EarlyExit = false,
                PrimaryBotType = primaryType,
                PrimaryBotName = primaryName,
                Signals = new Dictionary<string, object>(_signals),
                TotalProcessingTimeMs = _contributions.Sum(c => c.ProcessingTimeMs),
                CategoryBreakdown = categoryBreakdown,
                ContributingDetectors = _contributions.Select(c => c.DetectorName).ToHashSet(),
                FailedDetectors = _failedDetectors.ToHashSet()
            };
        }
    }

    private AggregatedEvidence CreateEarlyExitResult(DetectionContribution exitContribution)
    {
        var isGood = exitContribution.EarlyExitVerdict is
            EarlyExitVerdict.VerifiedGoodBot or EarlyExitVerdict.Whitelisted;

        return new AggregatedEvidence
        {
            Contributions = _contributions.ToList(),
            BotProbability = isGood ? 0.0 : 1.0,
            Confidence = 1.0, // Verified = high confidence
            RiskBand = RiskBand.Verified,
            EarlyExit = true,
            EarlyExitVerdict = exitContribution.EarlyExitVerdict,
            PrimaryBotType = exitContribution.BotType,
            PrimaryBotName = exitContribution.BotName,
            Signals = new Dictionary<string, object>(_signals),
            TotalProcessingTimeMs = _contributions.Sum(c => c.ProcessingTimeMs),
            CategoryBreakdown = BuildCategoryBreakdown(),
            ContributingDetectors = _contributions.Select(c => c.DetectorName).ToHashSet(),
            FailedDetectors = _failedDetectors.ToHashSet()
        };
    }

    private (double botProbability, double confidence) CalculateWeightedScore()
    {
        if (_contributions.Count == 0)
            return (0.3, 0.0); // No evidence = assume more likely human

        // Separate positive (bot) and negative (human) evidence
        var weighted = _contributions
            .Where(c => c.Weight > 0)
            .Select(c => (delta: c.ConfidenceDelta, weight: c.Weight))
            .ToList();

        if (weighted.Count == 0)
            return (0.3, 0.0); // Only info contributions = assume more likely human

        var totalWeight = weighted.Sum(w => w.weight);
        var weightedSum = weighted.Sum(w => w.delta * w.weight);

        // Normalize to 0-1 range (input deltas are -1 to +1)
        // weightedSum / totalWeight gives us a value in [-1, +1]
        // We map this to [0, 1]
        var normalizedScore = (weightedSum / totalWeight + 1) / 2;
        var botProbability = Math.Clamp(normalizedScore, 0, 1);

        // Confidence based on total evidence weight
        // More evidence = higher confidence
        var confidence = Math.Min(1.0, totalWeight / 5.0); // Cap at weight of 5

        return (botProbability, confidence);
    }

    private static RiskBand DetermineRiskBand(double botProbability, double confidence)
    {
        // If low confidence, be conservative
        if (confidence < 0.3)
            return RiskBand.Medium;

        return botProbability switch
        {
            < 0.2 => RiskBand.VeryLow,
            < 0.4 => RiskBand.Low,
            < 0.6 => RiskBand.Medium,
            < 0.8 => RiskBand.High,
            _ => RiskBand.VeryHigh
        };
    }

    private (BotType?, string?) FindPrimaryBot()
    {
        // Find the highest-confidence contribution with a bot type
        var primary = _contributions
            .Where(c => c.BotType.HasValue && c.ConfidenceDelta > 0)
            .OrderByDescending(c => c.ConfidenceDelta * c.Weight)
            .FirstOrDefault();

        return (primary?.BotType, primary?.BotName);
    }

    private IReadOnlyDictionary<string, CategoryScore> BuildCategoryBreakdown()
    {
        return _contributions
            .GroupBy(c => c.Category)
            .ToDictionary(
                g => g.Key,
                g => new CategoryScore
                {
                    Category = g.Key,
                    Score = g.Sum(c => c.ConfidenceDelta * c.Weight),
                    Weight = g.Sum(c => c.Weight),
                    ContributionCount = g.Count(),
                    Reasons = g.Select(c => c.Reason).ToList()
                });
    }
}
