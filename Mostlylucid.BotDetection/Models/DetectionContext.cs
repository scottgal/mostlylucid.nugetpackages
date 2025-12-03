using System.Collections.Concurrent;
using Microsoft.AspNetCore.Http;
using Mostlylucid.BotDetection.Detectors;

namespace Mostlylucid.BotDetection.Models;

/// <summary>
///     Shared context bus for detection pipeline.
///     Allows detectors to share signals and read results from earlier stages.
/// </summary>
public class DetectionContext
{
    private readonly ConcurrentDictionary<string, object> _signals = new();
    private readonly ConcurrentDictionary<string, double> _scores = new();
    private readonly ConcurrentBag<DetectionReason> _reasons = new();
    private readonly ConcurrentBag<LearnedSignal> _learnings = new();
    private readonly ConcurrentDictionary<string, DetectorResult> _detectorResults = new();

    /// <summary>
    ///     The HTTP context being analyzed
    /// </summary>
    public required HttpContext HttpContext { get; init; }

    /// <summary>
    ///     Cancellation token for the detection pipeline
    /// </summary>
    public CancellationToken CancellationToken { get; init; }

    #region Signal Bus

    /// <summary>
    ///     Set a signal value for other detectors to read
    /// </summary>
    public void SetSignal<T>(string key, T value) where T : notnull
    {
        _signals[key] = value;
    }

    /// <summary>
    ///     Get a signal value from an earlier detector
    /// </summary>
    public T? GetSignal<T>(string key)
    {
        if (_signals.TryGetValue(key, out var value) && value is T typed)
            return typed;
        return default;
    }

    /// <summary>
    ///     Check if a signal exists
    /// </summary>
    public bool HasSignal(string key) => _signals.ContainsKey(key);

    /// <summary>
    ///     Get all signal keys
    /// </summary>
    public IEnumerable<string> SignalKeys => _signals.Keys;

    #endregion

    #region Score Aggregation

    /// <summary>
    ///     Record a score from a detector
    /// </summary>
    public void SetScore(string detectorName, double score)
    {
        _scores[detectorName] = score;
    }

    /// <summary>
    ///     Get a specific detector's score
    /// </summary>
    public double? GetScore(string detectorName)
    {
        return _scores.TryGetValue(detectorName, out var score) ? score : null;
    }

    /// <summary>
    ///     Get all scores
    /// </summary>
    public IReadOnlyDictionary<string, double> Scores => _scores;

    /// <summary>
    ///     Get the maximum score from all detectors so far
    /// </summary>
    public double MaxScore => _scores.Values.DefaultIfEmpty(0).Max();

    /// <summary>
    ///     Get the average score from all detectors so far
    /// </summary>
    public double AverageScore => _scores.Values.DefaultIfEmpty(0).Average();

    #endregion

    #region Reason Accumulation

    /// <summary>
    ///     Add a detection reason
    /// </summary>
    public void AddReason(DetectionReason reason)
    {
        _reasons.Add(reason);
    }

    /// <summary>
    ///     Add multiple detection reasons
    /// </summary>
    public void AddReasons(IEnumerable<DetectionReason> reasons)
    {
        foreach (var reason in reasons)
            _reasons.Add(reason);
    }

    /// <summary>
    ///     Get all accumulated reasons
    /// </summary>
    public IReadOnlyList<DetectionReason> Reasons => _reasons.ToList();

    #endregion

    #region Detector Results

    /// <summary>
    ///     Store a detector's full result
    /// </summary>
    public void SetDetectorResult(string detectorName, DetectorResult result)
    {
        _detectorResults[detectorName] = result;
    }

    /// <summary>
    ///     Get a specific detector's result
    /// </summary>
    public DetectorResult? GetDetectorResult(string detectorName)
    {
        return _detectorResults.TryGetValue(detectorName, out var result) ? result : null;
    }

    /// <summary>
    ///     Get all detector results
    /// </summary>
    public IReadOnlyDictionary<string, DetectorResult> DetectorResults => _detectorResults;

    #endregion

    #region Learning Signals

    /// <summary>
    ///     Record a signal that should be fed back to ML for learning
    /// </summary>
    public void AddLearning(LearnedSignal signal)
    {
        _learnings.Add(signal);
    }

    /// <summary>
    ///     Get all learning signals
    /// </summary>
    public IReadOnlyList<LearnedSignal> Learnings => _learnings.ToList();

    #endregion
}

/// <summary>
///     A signal captured for ML feedback/learning
/// </summary>
public class LearnedSignal
{
    /// <summary>
    ///     Which detector generated this signal
    /// </summary>
    public required string SourceDetector { get; init; }

    /// <summary>
    ///     Type of signal (e.g., "Pattern", "Anomaly", "Inconsistency")
    /// </summary>
    public required string SignalType { get; init; }

    /// <summary>
    ///     The signal value/pattern
    /// </summary>
    public required string Value { get; init; }

    /// <summary>
    ///     Confidence in this signal
    /// </summary>
    public double Confidence { get; init; }

    /// <summary>
    ///     Additional metadata
    /// </summary>
    public Dictionary<string, object>? Metadata { get; init; }
}

/// <summary>
///     Well-known signal keys for cross-detector communication
/// </summary>
public static class SignalKeys
{
    // Stage 0 signals (raw detection)
    public const string UserAgent = "ua.raw";
    public const string UserAgentIsBot = "ua.is_bot";
    public const string UserAgentBotType = "ua.bot_type";
    public const string UserAgentBotName = "ua.bot_name";

    public const string HeadersPresent = "headers.present";
    public const string HeadersMissing = "headers.missing";
    public const string HeadersSuspicious = "headers.suspicious";

    public const string ClientIp = "ip.address";
    public const string IpIsDatacenter = "ip.is_datacenter";
    public const string IpProvider = "ip.provider";

    public const string FingerprintHash = "fingerprint.hash";
    public const string FingerprintHeadlessScore = "fingerprint.headless_score";
    public const string FingerprintIntegrityScore = "fingerprint.integrity_score";

    // Stage 1 signals (behavioral)
    public const string BehavioralRateExceeded = "behavioral.rate_exceeded";
    public const string BehavioralAnomalyDetected = "behavioral.anomaly";
    public const string BehavioralRequestCount = "behavioral.request_count";

    // Stage 2 signals (meta-layers)
    public const string InconsistencyScore = "inconsistency.score";
    public const string InconsistencyDetails = "inconsistency.details";

    public const string RiskBand = "risk.band";
    public const string RiskScore = "risk.score";

    public const string AiPrediction = "ai.prediction";
    public const string AiConfidence = "ai.confidence";
    public const string AiLearnedPattern = "ai.learned_pattern";
}
