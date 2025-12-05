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
    ///     Priority of the detector (lower = runs earlier).
    ///     Used to show execution order in the pipeline.
    /// </summary>
    public int Priority { get; init; }

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
    ///     Helper to create a neutral contribution (no impact, just reports detector ran)
    /// </summary>
    public static DetectionContribution Neutral(
        string detector,
        string reason,
        ImmutableDictionary<string, object>? signals = null) => new()
    {
        DetectorName = detector,
        Category = detector,
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
    Blacklisted,

    /// <summary>Policy allowed early exit (fastpath confident decision)</summary>
    PolicyAllowed,

    /// <summary>Policy blocked early exit (fastpath confident decision)</summary>
    PolicyBlocked
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

    /// <summary>
    ///     Whether AI detectors (ONNX, LLM) contributed to this decision.
    ///     When false, probability/risk are clamped to avoid overconfidence.
    /// </summary>
    public bool AiRan { get; init; }
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
///     Risk bands for final classification.
///     This is the canonical RiskBand enum - use this throughout the codebase.
/// </summary>
public enum RiskBand
{
    /// <summary>Detection hasn't run or no data available</summary>
    Unknown = 0,

    /// <summary>Very low risk - likely human</summary>
    VeryLow = 1,

    /// <summary>Low risk - probably human</summary>
    Low = 2,

    /// <summary>Elevated risk - consider throttling or soft challenge</summary>
    Elevated = 3,

    /// <summary>Medium risk - uncertain, recommend challenge</summary>
    Medium = 4,

    /// <summary>High risk - probably bot</summary>
    High = 5,

    /// <summary>Very high risk - almost certainly bot</summary>
    VeryHigh = 6,

    /// <summary>Verified - confirmed by external verification (good or bad bot)</summary>
    Verified = 7
}

/// <summary>
///     Recommended actions based on risk assessment.
///     This is the canonical RecommendedAction enum - use this throughout the codebase.
/// </summary>
public enum RecommendedAction
{
    /// <summary>Allow the request normally</summary>
    Allow,

    /// <summary>Apply rate limiting or throttling</summary>
    Throttle,

    /// <summary>Present a challenge (CAPTCHA, proof-of-work, JS challenge)</summary>
    Challenge,

    /// <summary>Block the request</summary>
    Block
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
            // Check if AI detectors contributed
            var aiDetectors = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { "Onnx", "Llm" };
            var detectorNames = _contributions.Select(c => c.DetectorName).ToHashSet();
            var aiRan = detectorNames.Any(d => aiDetectors.Contains(d));

            // Handle early exit
            if (_earlyExitContribution != null)
            {
                return CreateEarlyExitResult(_earlyExitContribution, aiRan);
            }

            // Calculate weighted score
            var (rawBotProbability, rawConfidence) = CalculateWeightedScore();

            // CRITICAL: Clamp probability when AI hasn't run
            // Without AI, we can't be confident about extreme probabilities
            var botProbability = aiRan
                ? rawBotProbability
                : Math.Clamp(rawBotProbability, 0.20, 0.80);

            // Compute coverage-based confidence (how many detector types ran)
            var coverageConfidence = ComputeCoverageConfidence(detectorNames, aiRan);

            // Final confidence is the minimum of evidence strength and coverage
            var confidence = Math.Min(rawConfidence, coverageConfidence);

            // Determine risk band (uses AI-aware mapping)
            var riskBand = DetermineRiskBand(botProbability, confidence, aiRan);

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
                ContributingDetectors = detectorNames,
                FailedDetectors = _failedDetectors.ToHashSet(),
                AiRan = aiRan
            };
        }
    }

    /// <summary>
    ///     Compute confidence based on detector coverage.
    ///     More detector types = higher confidence.
    /// </summary>
    private static double ComputeCoverageConfidence(IReadOnlySet<string> detectorsRan, bool aiRan)
    {
        var maxScore = 0.0;
        var score = 0.0;

        void Add(string name, double weight)
        {
            maxScore += weight;
            if (detectorsRan.Contains(name))
                score += weight;
        }

        Add("UserAgent", 1.0);
        Add("Ip", 0.5);
        Add("Header", 1.0);
        Add("ClientSide", 1.0);
        Add("Behavioral", 1.0);
        Add("VersionAge", 0.8);
        Add("Inconsistency", 0.8);
        Add("Heuristic", 2.0);  // Meta-layer that consumes all evidence

        // AI/LLM is worth a lot for confidence (escalation)
        if (aiRan)
        {
            maxScore += 2.5;
            score += 2.5;
        }
        else
        {
            maxScore += 2.5; // Still count toward max even if not ran
        }

        return maxScore == 0 ? 0 : score / maxScore;
    }

    private AggregatedEvidence CreateEarlyExitResult(DetectionContribution exitContribution, bool aiRan)
    {
        var isGood = exitContribution.EarlyExitVerdict is
            EarlyExitVerdict.VerifiedGoodBot or EarlyExitVerdict.Whitelisted;

        // Note: Verified good bots (like Googlebot) ARE bots (botProbability = 1.0)
        // They're just allowed. The "good" part affects the action (Allow), not the classification.
        // Whitelisted items might be bots or humans depending on what was whitelisted.
        var isBot = exitContribution.EarlyExitVerdict is
            EarlyExitVerdict.VerifiedGoodBot or EarlyExitVerdict.VerifiedBadBot;

        return new AggregatedEvidence
        {
            Contributions = _contributions.ToList(),
            BotProbability = isBot ? 1.0 : 0.0, // Bots are bots, even if allowed
            Confidence = 1.0, // Verified = high confidence
            RiskBand = isGood ? RiskBand.Verified : RiskBand.VeryHigh,
            EarlyExit = true,
            EarlyExitVerdict = exitContribution.EarlyExitVerdict,
            PrimaryBotType = exitContribution.BotType,
            PrimaryBotName = exitContribution.BotName,
            Signals = new Dictionary<string, object>(_signals),
            TotalProcessingTimeMs = _contributions.Sum(c => c.ProcessingTimeMs),
            CategoryBreakdown = BuildCategoryBreakdown(),
            ContributingDetectors = _contributions.Select(c => c.DetectorName).ToHashSet(),
            FailedDetectors = _failedDetectors.ToHashSet(),
            AiRan = aiRan
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

        // Use sigmoid to map weighted sum to [0, 1] probability
        // This properly leverages strong signals from high-weight detectors (like AI)
        //
        // weightedSum is the sum of (delta * weight) where:
        //   - Positive values indicate bot evidence
        //   - Negative values indicate human evidence
        //
        // Sigmoid: 1 / (1 + e^(-x)) maps any real number to (0, 1)
        // We scale the input so that typical evidence ranges produce reasonable outputs:
        //   - Strong human signal (weightedSum = -3) → ~5% bot probability
        //   - Neutral (weightedSum = 0) → 50% bot probability
        //   - Strong bot signal (weightedSum = +3) → ~95% bot probability
        var botProbability = 1.0 / (1.0 + Math.Exp(-weightedSum));

        // Confidence based on total evidence weight and signal strength
        // More evidence = higher confidence
        // Stronger signals (further from 0.5) = higher confidence
        var evidenceStrength = Math.Abs(botProbability - 0.5) * 2; // 0 at 0.5, 1 at extremes
        var weightFactor = Math.Min(1.0, totalWeight / 5.0);
        var confidence = Math.Max(weightFactor, evidenceStrength);

        return (botProbability, confidence);
    }

    private static RiskBand DetermineRiskBand(double botProbability, double confidence, bool aiRan)
    {
        // If low confidence, be conservative
        if (confidence < 0.3)
            return RiskBand.Medium;

        // AI-aware risk mapping
        // VeryLow is ONLY possible with AI confirmation
        if (aiRan)
        {
            return botProbability switch
            {
                < 0.05 => RiskBand.VeryLow, // AI confirms very human
                < 0.2 => RiskBand.Low,
                < 0.5 => RiskBand.Medium,
                < 0.8 => RiskBand.High,
                _ => RiskBand.VeryHigh
            };
        }
        else
        {
            // Without AI, we can't confidently say VeryLow
            // The probability is already clamped to 0.20-0.80, so these thresholds
            // mean we'll rarely go below Medium without AI
            return botProbability switch
            {
                < 0.25 => RiskBand.Low, // Best we can say without AI
                < 0.55 => RiskBand.Medium,
                < 0.75 => RiskBand.High,
                _ => RiskBand.VeryHigh
            };
        }
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
