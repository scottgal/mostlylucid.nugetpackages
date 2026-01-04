using Mostlylucid.CFMoM.Signals;

namespace Mostlylucid.CFMoM.Aggregation;

/// <summary>
///     Aggregates signals using weighted averaging with sigmoid normalization.
///     Based on the proven pattern from BotDetection.
/// </summary>
public sealed class WeightedAggregator : IAggregator
{
    private readonly WeightedAggregatorOptions _options;
    private readonly Func<ConstrainedSignal, double>? _weightResolver;

    public WeightedAggregator(
        WeightedAggregatorOptions? options = null,
        Func<ConstrainedSignal, double>? weightResolver = null)
    {
        _options = options ?? new WeightedAggregatorOptions();
        _weightResolver = weightResolver;
    }

    /// <inheritdoc />
    public AggregatedResult Aggregate(IReadOnlyList<ConstrainedSignal> signals)
    {
        if (signals.Count == 0)
        {
            return new AggregatedResult
            {
                Signals = signals,
                Score = _options.DefaultScore,
                Confidence = 0.0,
                Band = ClassificationBand.Unknown,
                ContributingProposers = new HashSet<string>()
            };
        }

        // Check for early exit
        var earlyExitSignal = signals.FirstOrDefault(s => s.TriggerEarlyExit);
        if (earlyExitSignal != null)
        {
            return CreateEarlyExitResult(signals, earlyExitSignal);
        }

        // Calculate weighted score using sigmoid
        var (score, confidence) = CalculateWeightedScore(signals);

        // Determine classification band
        var band = DetermineClassificationBand(score, confidence);

        // Build schema breakdown
        var schemaBreakdown = BuildSchemaBreakdown(signals);

        return new AggregatedResult
        {
            Signals = signals,
            Score = score,
            Confidence = confidence,
            Band = band,
            EarlyExit = false,
            SchemaBreakdown = schemaBreakdown,
            ContributingProposers = signals.Select(s => s.SourceId).ToHashSet()
        };
    }

    private AggregatedResult CreateEarlyExitResult(
        IReadOnlyList<ConstrainedSignal> signals,
        ConstrainedSignal earlyExitSignal)
    {
        return new AggregatedResult
        {
            Signals = signals,
            Score = earlyExitSignal.Confidence,
            Confidence = 1.0,
            Band = ClassificationBand.Verified,
            EarlyExit = true,
            EarlyExitClassification = earlyExitSignal.EarlyExitClassification,
            SchemaBreakdown = BuildSchemaBreakdown(signals),
            ContributingProposers = signals.Select(s => s.SourceId).ToHashSet()
        };
    }

    private (double score, double confidence) CalculateWeightedScore(IReadOnlyList<ConstrainedSignal> signals)
    {
        // Get weighted signals
        var weighted = signals
            .Select(s => (
                // Convert confidence to delta: 0.5 = neutral, <0.5 = negative, >0.5 = positive
                delta: (s.Confidence - 0.5) * 2, // Maps [0,1] to [-1,1]
                weight: GetWeight(s)
            ))
            .Where(w => w.weight > 0)
            .ToList();

        if (weighted.Count == 0)
        {
            return (_options.DefaultScore, 0.0);
        }

        var totalWeight = weighted.Sum(w => w.weight);
        var weightedSum = weighted.Sum(w => w.delta * w.weight);

        // Use sigmoid to map weighted sum to [0, 1] probability
        // weightedSum is the sum of (delta * weight) where:
        //   - Positive values indicate high confidence in positive classification
        //   - Negative values indicate high confidence in negative classification
        var score = 1.0 / (1.0 + Math.Exp(-weightedSum));

        // Confidence based on total evidence weight and signal strength
        var evidenceStrength = Math.Abs(score - 0.5) * 2; // 0 at 0.5, 1 at extremes
        var weightFactor = Math.Min(1.0, totalWeight / _options.ConfidenceWeightDivisor);
        var confidence = Math.Max(weightFactor, evidenceStrength);

        return (score, confidence);
    }

    private double GetWeight(ConstrainedSignal signal)
    {
        if (_weightResolver != null)
        {
            return _weightResolver(signal);
        }

        // Check for weight in metadata
        if (signal.Metadata.TryGetValue("weight", out var weightObj) && weightObj is double weight)
        {
            return weight;
        }

        // Check schema-based weights
        if (_options.SchemaWeights.TryGetValue(signal.FactsSchemaId, out var schemaWeight))
        {
            return schemaWeight;
        }

        return _options.DefaultWeight;
    }

    private ClassificationBand DetermineClassificationBand(double score, double confidence)
    {
        // If low confidence, be conservative
        if (confidence < _options.LowConfidenceThreshold)
        {
            return ClassificationBand.Medium;
        }

        return score switch
        {
            < 0.1 => ClassificationBand.VeryLow,
            < 0.3 => ClassificationBand.Low,
            < 0.5 => ClassificationBand.Medium,
            < 0.7 => ClassificationBand.High,
            _ => ClassificationBand.VeryHigh
        };
    }

    private Dictionary<string, SchemaBreakdown> BuildSchemaBreakdown(IReadOnlyList<ConstrainedSignal> signals)
    {
        return signals
            .GroupBy(s => s.FactsSchemaId)
            .ToDictionary(
                g => g.Key,
                g => new SchemaBreakdown
                {
                    SchemaId = g.Key,
                    Score = g.Average(s => s.Confidence),
                    SignalCount = g.Count(),
                    AverageConfidence = g.Average(s => s.Confidence)
                });
    }
}

/// <summary>
///     Options for the weighted aggregator.
/// </summary>
public sealed class WeightedAggregatorOptions
{
    /// <summary>
    ///     Default score when no signals are present. Default: 0.5
    /// </summary>
    public double DefaultScore { get; set; } = 0.5;

    /// <summary>
    ///     Default weight for signals without explicit weight. Default: 1.0
    /// </summary>
    public double DefaultWeight { get; set; } = 1.0;

    /// <summary>
    ///     Weights by schema ID. Schemas not listed use DefaultWeight.
    /// </summary>
    public Dictionary<string, double> SchemaWeights { get; set; } = new();

    /// <summary>
    ///     Divisor for weight-based confidence calculation. Default: 5.0
    /// </summary>
    public double ConfidenceWeightDivisor { get; set; } = 5.0;

    /// <summary>
    ///     Confidence threshold below which classification is Medium. Default: 0.3
    /// </summary>
    public double LowConfidenceThreshold { get; set; } = 0.3;
}
