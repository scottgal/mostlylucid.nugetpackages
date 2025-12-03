using Microsoft.AspNetCore.Http;
using Mostlylucid.BotDetection.Orchestration;

namespace Mostlylucid.BotDetection.Detectors;

/// <summary>
///     Feature extraction for ML-based bot detection.
///     Consumes results from other detectors - does NOT duplicate their work.
/// </summary>
/// <remarks>
///     <para>
///         <b>FIXED VECTOR SIZE:</b> Always produces exactly 64 features.
///         Empty slots are filled with 0.0f to maintain consistent input shape.
///     </para>
///     <para>
///         Key design principle: This extractor ONLY consumes evidence from other detectors.
///         It does not perform its own detection logic. Features are:
///         <list type="bullet">
///             <item>Basic request metadata (lengths, counts) - raw data only</item>
///             <item>Detector results from AggregatedEvidence (dynamic, ordered by confidence)</item>
///             <item>Category breakdown scores</item>
///             <item>Aggregated statistics</item>
///         </list>
///     </para>
///     <para>
///         IMPORTANT: Detector slots are filled dynamically, sorted by confidence descending.
///         This means the feature vector captures the N most confident detectors regardless
///         of which specific detectors contributed. This is intentional for learning.
///     </para>
/// </remarks>
public static class OnnxFeatureExtractor
{
    /// <summary>
    ///     Current feature count. Used by ONNX model input layer.
    /// </summary>
    public const int FeatureCount = 64;

    /// <summary>
    ///     Number of detector slots for dynamic detector results.
    /// </summary>
    public const int DetectorSlots = 16;

    /// <summary>
    ///     Number of category slots for dynamic category breakdown.
    /// </summary>
    public const int CategorySlots = 8;

    /// <summary>
    ///     Feature names for debugging and model introspection.
    /// </summary>
    public static readonly string[] FeatureNames =
    [
        // === Basic Request Metadata (0-11) - raw data only ===
        "ua_length_norm",           // 0: User-Agent length normalized
        "has_accept_language",      // 1: Has Accept-Language header
        "has_accept",               // 2: Has Accept header
        "has_referer",              // 3: Has Referer header
        "has_cookies",              // 4: Has cookies
        "header_count_norm",        // 5: Header count normalized
        "path_length_norm",         // 6: Request path length normalized
        "query_param_count",        // 7: Query parameter count normalized
        "content_length_norm",      // 8: Content-Length normalized (for POST)
        "is_https",                 // 9: Request is HTTPS
        "is_ajax",                  // 10: X-Requested-With: XMLHttpRequest
        "has_origin",               // 11: Has Origin header

        // === Dynamic Detector Results (12-27) - sorted by confidence DESC ===
        // Slots filled dynamically with top N detectors by confidence
        "detector_1_confidence",    // 12: Highest confidence detector
        "detector_2_confidence",    // 13: 2nd highest
        "detector_3_confidence",    // 14: 3rd highest
        "detector_4_confidence",    // 15: 4th highest
        "detector_5_confidence",    // 16: 5th highest
        "detector_6_confidence",    // 17: 6th highest
        "detector_7_confidence",    // 18: 7th highest
        "detector_8_confidence",    // 19: 8th highest
        "detector_9_confidence",    // 20: 9th highest
        "detector_10_confidence",   // 21: 10th highest
        "detector_11_confidence",   // 22: 11th highest
        "detector_12_confidence",   // 23: 12th highest
        "detector_13_confidence",   // 24: 13th highest
        "detector_14_confidence",   // 25: 14th highest
        "detector_15_confidence",   // 26: 15th highest
        "detector_16_confidence",   // 27: 16th highest

        // === Dynamic Category Breakdown (28-35) - sorted by score DESC ===
        "category_1_score",         // 28: Highest category score
        "category_2_score",         // 29: 2nd highest
        "category_3_score",         // 30: 3rd highest
        "category_4_score",         // 31: 4th highest
        "category_5_score",         // 32: 5th highest
        "category_6_score",         // 33: 6th highest
        "category_7_score",         // 34: 7th highest
        "category_8_score",         // 35: 8th highest

        // === Aggregated Statistics (36-47) ===
        "detector_count",           // 36: Number of detectors that ran
        "detector_flagged_count",   // 37: Detectors with confidence > 0.3
        "detector_max",             // 38: Maximum detector confidence
        "detector_avg",             // 39: Average detector confidence
        "detector_variance",        // 40: Variance in detector scores
        "category_count",           // 41: Number of categories with scores
        "category_max",             // 42: Maximum category score
        "category_avg",             // 43: Average category score
        "contribution_count",       // 44: Total number of contributions
        "signal_count",             // 45: Number of signals in evidence
        "failed_detector_count",    // 46: Number of failed detectors
        "processing_time_norm",     // 47: Total processing time normalized

        // === Final Aggregated Results (48-55) ===
        "bot_probability",          // 48: Overall bot probability
        "confidence",               // 49: Detection confidence
        "early_exit",               // 50: Early exit was triggered
        "risk_band_norm",           // 51: Risk band normalized
        "primary_bot_type",         // 52: Primary bot type (encoded)
        "reserved_53",              // 53: Reserved
        "reserved_54",              // 54: Reserved
        "reserved_55",              // 55: Reserved

        // === Contribution Details (56-63) - from top contributions ===
        "contrib_1_weight",         // 56: Weight of top contribution
        "contrib_2_weight",         // 57: 2nd contribution weight
        "contrib_3_weight",         // 58: 3rd contribution weight
        "contrib_4_weight",         // 59: 4th contribution weight
        "contrib_5_weight",         // 60: 5th contribution weight
        "contrib_6_weight",         // 61: 6th contribution weight
        "contrib_7_weight",         // 62: 7th contribution weight
        "contrib_8_weight",         // 63: 8th contribution weight
    ];

    /// <summary>
    ///     Extracts the full feature vector from aggregated evidence.
    ///     This is the primary entry point - call after all detectors have run.
    /// </summary>
    /// <remarks>
    ///     Detector and category scores are filled dynamically, sorted by confidence/score descending.
    ///     This means the model learns patterns based on "the most confident detectors" rather than
    ///     specific named detectors, making it robust to adding/removing detectors.
    /// </remarks>
    public static float[] ExtractFeatures(HttpContext context, AggregatedEvidence evidence)
    {
        var features = new float[FeatureCount];

        // === Basic Request Metadata (0-11) ===
        ExtractRequestMetadata(context, features);

        // === Dynamic Detector Results (12-27) - sorted by confidence DESC ===
        ExtractDetectorResults(evidence, features);

        // === Dynamic Category Breakdown (28-35) - sorted by score DESC ===
        ExtractCategoryBreakdown(evidence, features);

        // === Aggregated Statistics (36-47) ===
        ExtractStatistics(evidence, features);

        // === Final Results (48-55) ===
        ExtractFinalResults(evidence, features);

        // === Top Contribution Weights (56-63) ===
        ExtractContributionWeights(evidence, features);

        return features;
    }

    /// <summary>
    ///     Extracts basic request metadata (raw data only, no detection).
    /// </summary>
    private static void ExtractRequestMetadata(HttpContext context, float[] features)
    {
        var headers = context.Request.Headers;
        var userAgent = headers.UserAgent.ToString();

        features[0] = Math.Min(userAgent.Length / 200f, 1f);
        features[1] = headers.ContainsKey("Accept-Language") ? 1f : 0f;
        features[2] = headers.ContainsKey("Accept") ? 1f : 0f;
        features[3] = headers.ContainsKey("Referer") ? 1f : 0f;
        features[4] = context.Request.Cookies.Any() ? 1f : 0f;
        features[5] = Math.Min(headers.Count / 20f, 1f);
        features[6] = Math.Min((context.Request.Path.Value?.Length ?? 0) / 100f, 1f);
        features[7] = Math.Min(context.Request.Query.Count / 10f, 1f);
        features[8] = Math.Min((context.Request.ContentLength ?? 0) / 10000f, 1f);
        features[9] = context.Request.IsHttps ? 1f : 0f;
        features[10] = headers["X-Requested-With"].ToString()
            .Equals("XMLHttpRequest", StringComparison.OrdinalIgnoreCase) ? 1f : 0f;
        features[11] = headers.ContainsKey("Origin") ? 1f : 0f;
    }

    /// <summary>
    ///     Extracts detector results sorted by confidence descending.
    ///     Fills slots dynamically with the top N most confident detectors.
    /// </summary>
    private static void ExtractDetectorResults(AggregatedEvidence evidence, float[] features)
    {
        // Get max confidence delta per detector (using absolute value for sorting)
        var detectorScores = evidence.Contributions
            .GroupBy(c => c.DetectorName, StringComparer.OrdinalIgnoreCase)
            .Select(g => g.Max(c => Math.Abs(c.ConfidenceDelta)))
            .OrderByDescending(c => c)
            .Take(DetectorSlots)
            .ToList();

        // Fill slots (12-27) with sorted detector confidence deltas
        for (int i = 0; i < DetectorSlots; i++)
        {
            features[12 + i] = i < detectorScores.Count ? (float)detectorScores[i] : 0f;
        }
    }

    /// <summary>
    ///     Extracts category breakdown sorted by score descending.
    /// </summary>
    private static void ExtractCategoryBreakdown(AggregatedEvidence evidence, float[] features)
    {
        var categoryScores = evidence.CategoryBreakdown
            .OrderByDescending(kv => kv.Value.Score)
            .Take(CategorySlots)
            .Select(kv => kv.Value.Score)
            .ToList();

        // Fill slots (28-35) with sorted category scores
        for (int i = 0; i < CategorySlots; i++)
        {
            features[28 + i] = i < categoryScores.Count ? (float)categoryScores[i] : 0f;
        }
    }

    /// <summary>
    ///     Extracts aggregated statistics from evidence.
    /// </summary>
    private static void ExtractStatistics(AggregatedEvidence evidence, float[] features)
    {
        var detectorScores = evidence.Contributions
            .GroupBy(c => c.DetectorName, StringComparer.OrdinalIgnoreCase)
            .Select(g => g.Max(c => Math.Abs(c.ConfidenceDelta)))
            .ToList();

        var categoryScores = evidence.CategoryBreakdown.Values
            .Select(c => c.Score)
            .ToList();

        // Detector stats
        features[36] = Math.Min(detectorScores.Count / 10f, 1f);
        features[37] = Math.Min(detectorScores.Count(s => s > 0.3) / 6f, 1f);
        features[38] = detectorScores.Count > 0 ? (float)detectorScores.Max() : 0f;
        features[39] = detectorScores.Count > 0 ? (float)detectorScores.Average() : 0f;
        features[40] = detectorScores.Count > 1 ? (float)CalculateVariance(detectorScores) : 0f;

        // Category stats
        features[41] = Math.Min(categoryScores.Count / 8f, 1f);
        features[42] = categoryScores.Count > 0 ? (float)categoryScores.Max() : 0f;
        features[43] = categoryScores.Count > 0 ? (float)categoryScores.Average() : 0f;

        // Other stats
        features[44] = Math.Min(evidence.Contributions.Count / 20f, 1f);
        features[45] = Math.Min(evidence.Signals.Count / 50f, 1f);
        features[46] = Math.Min(evidence.FailedDetectors.Count / 5f, 1f);
        features[47] = Math.Min((float)evidence.TotalProcessingTimeMs / 1000f, 1f);
    }

    /// <summary>
    ///     Extracts final aggregated results.
    /// </summary>
    private static void ExtractFinalResults(AggregatedEvidence evidence, float[] features)
    {
        features[48] = (float)evidence.BotProbability;
        features[49] = (float)evidence.Confidence;
        features[50] = evidence.EarlyExit ? 1f : 0f;
        features[51] = (int)evidence.RiskBand / 5f; // RiskBand has 6 values (0-5)
        features[52] = evidence.PrimaryBotType.HasValue ? ((int)evidence.PrimaryBotType.Value + 1) / 10f : 0f;
        features[53] = 0f; // Reserved
        features[54] = 0f; // Reserved
        features[55] = 0f; // Reserved
    }

    /// <summary>
    ///     Extracts top contribution weights sorted by impact descending.
    /// </summary>
    private static void ExtractContributionWeights(AggregatedEvidence evidence, float[] features)
    {
        var topWeights = evidence.Contributions
            .OrderByDescending(c => c.Weight * Math.Abs(c.ConfidenceDelta))
            .Take(8)
            .Select(c => c.Weight)
            .ToList();

        // Fill slots (56-63) with sorted contribution weights
        for (int i = 0; i < 8; i++)
        {
            features[56 + i] = i < topWeights.Count ? (float)topWeights[i] : 0f;
        }
    }

    private static double CalculateVariance(List<double> values)
    {
        if (values.Count < 2) return 0;
        var avg = values.Average();
        return values.Sum(v => Math.Pow(v - avg, 2)) / values.Count;
    }
}

/// <summary>
///     Extended behavioral metrics for ML feature extraction.
///     Populated by behavioral detectors and passed via signals.
/// </summary>
public class BehavioralMetrics
{
    public double? RequestsPerMinute { get; set; }
    public double? UniquePathsRatio { get; set; }
    public double? SessionDurationSeconds { get; set; }
    public double? AvgTimeBetweenRequestsMs { get; set; }
    public double? TimeVariance { get; set; }
    public double? SequentialAccessScore { get; set; }
    public double? DepthFirstScore { get; set; }
    public double? BreadthFirstScore { get; set; }
    public double? RandomAccessScore { get; set; }
    public double? ErrorRate { get; set; }
    public double? StaticResourceRatio { get; set; }
    public double? ApiRequestRatio { get; set; }
}

/// <summary>
///     Fingerprint data for ML feature extraction.
///     Populated by fingerprint detector from client-side collection.
/// </summary>
public class FingerprintMetrics
{
    public int? IntegrityScore { get; set; }
    public bool WebGlAnomaly { get; set; }
    public bool CanvasAnomaly { get; set; }
    public bool TimezoneMismatch { get; set; }
    public bool LanguageMismatch { get; set; }
    public bool ScreenResolutionAnomaly { get; set; }
    public int? HeadlessIndicatorCount { get; set; }
}
