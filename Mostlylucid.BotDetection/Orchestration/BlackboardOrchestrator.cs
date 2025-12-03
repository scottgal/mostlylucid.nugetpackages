using System.Collections.Concurrent;
using System.Diagnostics;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Mostlylucid.BotDetection.Events;
using Mostlylucid.BotDetection.Models;

namespace Mostlylucid.BotDetection.Orchestration;

/// <summary>
///     Configuration for the blackboard orchestrator
/// </summary>
public class OrchestratorOptions
{
    /// <summary>
    ///     Maximum time for the entire detection pipeline.
    ///     Default: 5 seconds
    /// </summary>
    public TimeSpan TotalTimeout { get; set; } = TimeSpan.FromSeconds(5);

    /// <summary>
    ///     Maximum number of waves before stopping.
    ///     Prevents infinite loops from circular trigger dependencies.
    ///     Default: 10
    /// </summary>
    public int MaxWaves { get; set; } = 10;

    /// <summary>
    ///     Time to wait between waves for new triggers to become satisfied.
    ///     Default: 50ms
    /// </summary>
    public TimeSpan WaveInterval { get; set; } = TimeSpan.FromMilliseconds(50);

    /// <summary>
    ///     Minimum bot probability to trigger expensive detectors.
    ///     Saves resources on obvious humans.
    ///     Default: 0.3
    /// </summary>
    public double ExpensiveDetectorThreshold { get; set; } = 0.3;

    /// <summary>
    ///     Circuit breaker: number of failures before disabling a detector.
    ///     Default: 5
    /// </summary>
    public int CircuitBreakerThreshold { get; set; } = 5;

    /// <summary>
    ///     Circuit breaker: time to wait before retrying a disabled detector.
    ///     Default: 60 seconds
    /// </summary>
    public TimeSpan CircuitBreakerResetTime { get; set; } = TimeSpan.FromSeconds(60);

    /// <summary>
    ///     Whether to enable parallel execution of detectors.
    ///     Default: true
    /// </summary>
    public bool EnableParallelExecution { get; set; } = true;

    /// <summary>
    ///     Maximum parallel detectors per wave.
    ///     Default: 10
    /// </summary>
    public int MaxParallelDetectors { get; set; } = 10;
}

/// <summary>
///     Wave-based parallel orchestrator using blackboard architecture.
///
///     Execution model:
///     1. Wave 0: Run all detectors with no trigger conditions (in parallel)
///     2. Wave N: Run all detectors whose triggers are now satisfied (in parallel)
///     3. Repeat until no more detectors can run, early exit, or timeout
///
///     Key features:
///     - Parallel execution within waves
///     - Circuit breaker per detector
///     - Timeout handling at detector and pipeline level
///     - Early exit on verified bots
///     - Real-time signal aggregation
/// </summary>
public class BlackboardOrchestrator
{
    private readonly ILogger<BlackboardOrchestrator> _logger;
    private readonly OrchestratorOptions _options;
    private readonly IEnumerable<IContributingDetector> _detectors;
    private readonly ILearningEventBus? _learningBus;

    // Circuit breaker state per detector
    private readonly ConcurrentDictionary<string, CircuitState> _circuitStates = new();

    public BlackboardOrchestrator(
        ILogger<BlackboardOrchestrator> logger,
        IOptions<BotDetectionOptions> options,
        IEnumerable<IContributingDetector> detectors,
        ILearningEventBus? learningBus = null)
    {
        _logger = logger;
        _options = options.Value.Orchestrator;
        _detectors = detectors;
        _learningBus = learningBus;
    }

    /// <summary>
    ///     Run the full detection pipeline and aggregate results.
    /// </summary>
    public async Task<AggregatedEvidence> DetectAsync(
        HttpContext httpContext,
        CancellationToken cancellationToken = default)
    {
        var stopwatch = Stopwatch.StartNew();
        var requestId = httpContext.TraceIdentifier;

        using var cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        cts.CancelAfter(_options.TotalTimeout);

        var aggregator = new EvidenceAggregator();
        var signals = new ConcurrentDictionary<string, object>();
        var completedDetectors = new ConcurrentDictionary<string, bool>();
        var failedDetectors = new ConcurrentDictionary<string, bool>();
        var ranDetectors = new HashSet<string>();

        // Get enabled detectors (respecting circuit breakers)
        var availableDetectors = _detectors
            .Where(d => d.IsEnabled && IsCircuitClosed(d.Name))
            .OrderBy(d => d.Priority)
            .ToList();

        _logger.LogDebug(
            "Starting detection for {RequestId} with {DetectorCount} available detectors",
            requestId, availableDetectors.Count);

        var waveNumber = 0;

        try
        {
            while (waveNumber < _options.MaxWaves && !cts.Token.IsCancellationRequested)
            {
                // Build current blackboard state
                var state = BuildState(
                    httpContext,
                    signals,
                    completedDetectors.Keys.ToHashSet(),
                    failedDetectors.Keys.ToHashSet(),
                    aggregator,
                    requestId,
                    stopwatch.Elapsed);

                // Find detectors that can run in this wave
                var readyDetectors = availableDetectors
                    .Where(d => !ranDetectors.Contains(d.Name))
                    .Where(d => CanRun(d, state.Signals))
                    .ToList();

                if (readyDetectors.Count == 0)
                {
                    _logger.LogDebug(
                        "Wave {Wave}: No more detectors ready, finishing",
                        waveNumber);
                    break;
                }

                _logger.LogDebug(
                    "Wave {Wave}: Running {Count} detectors: {Names}",
                    waveNumber,
                    readyDetectors.Count,
                    string.Join(", ", readyDetectors.Select(d => d.Name)));

                // Mark as ran (before execution to prevent re-triggering)
                foreach (var detector in readyDetectors)
                {
                    ranDetectors.Add(detector.Name);
                }

                // Execute wave
                await ExecuteWaveAsync(
                    readyDetectors,
                    state,
                    aggregator,
                    signals,
                    completedDetectors,
                    failedDetectors,
                    cts.Token);

                // Check for early exit
                if (aggregator.ShouldEarlyExit)
                {
                    var exitContrib = aggregator.EarlyExitContribution!;
                    _logger.LogInformation(
                        "Early exit triggered by {Detector}: {Verdict} - {Reason}",
                        exitContrib.DetectorName,
                        exitContrib.EarlyExitVerdict,
                        exitContrib.Reason);
                    break;
                }

                // Update system signals for next wave
                signals[DetectorCountTrigger.CompletedDetectorsSignal] = completedDetectors.Count;
                signals[RiskThresholdTrigger.CurrentRiskSignal] = aggregator.Aggregate().BotProbability;

                waveNumber++;

                // Small delay between waves to allow signals to propagate
                if (waveNumber < _options.MaxWaves)
                {
                    await Task.Delay(_options.WaveInterval, cts.Token);
                }
            }
        }
        catch (OperationCanceledException) when (cts.IsCancellationRequested && !cancellationToken.IsCancellationRequested)
        {
            _logger.LogWarning(
                "Detection timed out after {Elapsed}ms for {RequestId}",
                stopwatch.ElapsedMilliseconds, requestId);
        }

        var result = aggregator.Aggregate();

        // Publish learning event
        PublishLearningEvent(result, requestId, stopwatch.Elapsed);

        _logger.LogDebug(
            "Detection complete for {RequestId}: {RiskBand} (prob={Probability:F2}, conf={Confidence:F2}) in {Elapsed}ms, {Waves} waves, {Detectors} detectors",
            requestId,
            result.RiskBand,
            result.BotProbability,
            result.Confidence,
            stopwatch.ElapsedMilliseconds,
            waveNumber,
            result.ContributingDetectors.Count);

        return result;
    }

    private async Task ExecuteWaveAsync(
        IReadOnlyList<IContributingDetector> detectors,
        BlackboardState state,
        EvidenceAggregator aggregator,
        ConcurrentDictionary<string, object> signals,
        ConcurrentDictionary<string, bool> completedDetectors,
        ConcurrentDictionary<string, bool> failedDetectors,
        CancellationToken cancellationToken)
    {
        if (!_options.EnableParallelExecution || detectors.Count == 1)
        {
            // Sequential execution
            foreach (var detector in detectors)
            {
                await ExecuteDetectorAsync(
                    detector, state, aggregator, signals,
                    completedDetectors, failedDetectors, cancellationToken);

                if (aggregator.ShouldEarlyExit)
                    break;
            }
        }
        else
        {
            // Parallel execution with semaphore
            using var semaphore = new SemaphoreSlim(_options.MaxParallelDetectors);
            var tasks = detectors.Select(async detector =>
            {
                await semaphore.WaitAsync(cancellationToken);
                try
                {
                    await ExecuteDetectorAsync(
                        detector, state, aggregator, signals,
                        completedDetectors, failedDetectors, cancellationToken);
                }
                finally
                {
                    semaphore.Release();
                }
            });

            await Task.WhenAll(tasks);
        }
    }

    private async Task ExecuteDetectorAsync(
        IContributingDetector detector,
        BlackboardState state,
        EvidenceAggregator aggregator,
        ConcurrentDictionary<string, object> signals,
        ConcurrentDictionary<string, bool> completedDetectors,
        ConcurrentDictionary<string, bool> failedDetectors,
        CancellationToken cancellationToken)
    {
        var stopwatch = Stopwatch.StartNew();

        try
        {
            using var cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            cts.CancelAfter(detector.ExecutionTimeout);

            var contributions = await detector.ContributeAsync(state, cts.Token);

            stopwatch.Stop();

            foreach (var contribution in contributions)
            {
                // Set processing time
                var withTime = contribution with { ProcessingTimeMs = stopwatch.ElapsedMilliseconds };
                aggregator.AddContribution(withTime);

                // Merge signals
                foreach (var signal in contribution.Signals)
                {
                    signals[signal.Key] = signal.Value;
                }
            }

            completedDetectors[detector.Name] = true;
            RecordSuccess(detector.Name);

            _logger.LogDebug(
                "Detector {Name} completed in {Elapsed}ms with {ContributionCount} contributions",
                detector.Name, stopwatch.ElapsedMilliseconds, contributions.Count);
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            // Detector timeout
            stopwatch.Stop();
            HandleDetectorFailure(detector, aggregator, failedDetectors, "Timeout", stopwatch.ElapsedMilliseconds);
        }
        catch (Exception ex)
        {
            stopwatch.Stop();
            HandleDetectorFailure(detector, aggregator, failedDetectors, ex.Message, stopwatch.ElapsedMilliseconds);

            _logger.LogWarning(ex,
                "Detector {Name} failed after {Elapsed}ms: {Message}",
                detector.Name, stopwatch.ElapsedMilliseconds, ex.Message);
        }
    }

    private void HandleDetectorFailure(
        IContributingDetector detector,
        EvidenceAggregator aggregator,
        ConcurrentDictionary<string, bool> failedDetectors,
        string reason,
        double elapsedMs)
    {
        failedDetectors[detector.Name] = true;
        aggregator.RecordFailure(detector.Name);
        RecordFailure(detector.Name);

        if (!detector.IsOptional)
        {
            _logger.LogError(
                "Required detector {Name} failed: {Reason}",
                detector.Name, reason);
        }
    }

    private static bool CanRun(IContributingDetector detector, IReadOnlyDictionary<string, object> signals)
    {
        // No conditions = can always run
        if (detector.TriggerConditions.Count == 0)
            return true;

        // All conditions must be satisfied
        return detector.TriggerConditions.All(c => c.IsSatisfied(signals));
    }

    private static BlackboardState BuildState(
        HttpContext httpContext,
        ConcurrentDictionary<string, object> signals,
        IReadOnlySet<string> completedDetectors,
        IReadOnlySet<string> failedDetectors,
        EvidenceAggregator aggregator,
        string requestId,
        TimeSpan elapsed)
    {
        var contributions = new List<DetectionContribution>();
        var aggregated = aggregator.Aggregate();

        return new BlackboardState
        {
            HttpContext = httpContext,
            Signals = new Dictionary<string, object>(signals),
            CurrentRiskScore = aggregated.BotProbability,
            CompletedDetectors = completedDetectors,
            FailedDetectors = failedDetectors,
            Contributions = aggregated.Contributions,
            RequestId = requestId,
            Elapsed = elapsed
        };
    }

    #region Circuit Breaker

    private bool IsCircuitClosed(string detectorName)
    {
        if (!_circuitStates.TryGetValue(detectorName, out var state))
            return true;

        if (state.State == CircuitBreakerState.Closed)
            return true;

        if (state.State == CircuitBreakerState.Open)
        {
            // Check if enough time has passed to try again
            if (DateTimeOffset.UtcNow - state.LastFailure > _options.CircuitBreakerResetTime)
            {
                state.State = CircuitBreakerState.HalfOpen;
                return true;
            }
            return false;
        }

        // Half-open: allow one attempt
        return true;
    }

    private void RecordSuccess(string detectorName)
    {
        if (_circuitStates.TryGetValue(detectorName, out var state))
        {
            state.FailureCount = 0;
            state.State = CircuitBreakerState.Closed;
        }
    }

    private void RecordFailure(string detectorName)
    {
        var state = _circuitStates.GetOrAdd(detectorName, _ => new CircuitState());

        state.FailureCount++;
        state.LastFailure = DateTimeOffset.UtcNow;

        if (state.FailureCount >= _options.CircuitBreakerThreshold)
        {
            state.State = CircuitBreakerState.Open;
            _logger.LogWarning(
                "Circuit breaker opened for detector {Name} after {Count} failures",
                detectorName, state.FailureCount);
        }
    }

    #endregion

    #region Learning Events

    private void PublishLearningEvent(
        AggregatedEvidence result,
        string requestId,
        TimeSpan elapsed)
    {
        if (_learningBus == null)
            return;

        var eventType = result.BotProbability >= 0.8
            ? LearningEventType.HighConfidenceDetection
            : LearningEventType.FullDetection;

        _learningBus.TryPublish(new LearningEvent
        {
            Type = eventType,
            Source = nameof(BlackboardOrchestrator),
            Confidence = result.Confidence,
            Label = result.BotProbability >= 0.5,
            RequestId = requestId,
            Metadata = new Dictionary<string, object>
            {
                ["botProbability"] = result.BotProbability,
                ["riskBand"] = result.RiskBand.ToString(),
                ["contributingDetectors"] = result.ContributingDetectors.ToList(),
                ["failedDetectors"] = result.FailedDetectors.ToList(),
                ["processingTimeMs"] = elapsed.TotalMilliseconds,
                ["categoryBreakdown"] = result.CategoryBreakdown.ToDictionary(
                    kv => kv.Key,
                    kv => (object)kv.Value.Score)
            }
        });
    }

    #endregion
}

/// <summary>
///     Circuit breaker state for a single detector
/// </summary>
internal class CircuitState
{
    public CircuitBreakerState State { get; set; } = CircuitBreakerState.Closed;
    public int FailureCount { get; set; }
    public DateTimeOffset LastFailure { get; set; }
}

internal enum CircuitBreakerState
{
    Closed,   // Normal operation
    Open,     // Failing, reject requests
    HalfOpen  // Trying one request to see if recovered
}
