using System.Collections.Concurrent;
using System.Diagnostics;
using Microsoft.Extensions.Logging;
using Mostlylucid.CFMoM.Aggregation;
using Mostlylucid.CFMoM.ConsensusSpace;
using Mostlylucid.CFMoM.Constrainers;
using Mostlylucid.CFMoM.Proposers;
using Mostlylucid.CFMoM.Signals;
using Mostlylucid.Ephemeral;

namespace Mostlylucid.CFMoM.Orchestration;

/// <summary>
///     The CFMoM Orchestrator coordinates proposers in waves,
///     aggregates their signals, and consults the constrainer for decisions.
///
///     Architecture:
///     1. Proposers run in waves (parallel within wave, sequential across waves)
///     2. Signals are ingested into the consensus space
///     3. Aggregator computes scores from signals
///     4. Constrainer makes deterministic decisions
/// </summary>
/// <typeparam name="TContext">The context type passed to proposers.</typeparam>
/// <typeparam name="TDecision">The decision type from the constrainer.</typeparam>
public sealed class CFMoMOrchestrator<TContext, TDecision>
{
    private readonly IEnumerable<IProposer<TContext>> _proposers;
    private readonly IConsensusSpace _consensusSpace;
    private readonly IAggregator _aggregator;
    private readonly IConstrainer<TContext, TDecision> _constrainer;
    private readonly CFMoMOptions _options;
    private readonly ILogger<CFMoMOrchestrator<TContext, TDecision>>? _logger;
    private readonly SignalSink? _signalSink;

    // Circuit breaker state per proposer
    private readonly ConcurrentDictionary<string, CircuitState> _circuitStates = new();

    public CFMoMOrchestrator(
        IEnumerable<IProposer<TContext>> proposers,
        IConsensusSpace consensusSpace,
        IAggregator aggregator,
        IConstrainer<TContext, TDecision> constrainer,
        CFMoMOptions? options = null,
        ILogger<CFMoMOrchestrator<TContext, TDecision>>? logger = null,
        SignalSink? signalSink = null)
    {
        _proposers = proposers;
        _consensusSpace = consensusSpace;
        _aggregator = aggregator;
        _constrainer = constrainer;
        _options = options ?? new CFMoMOptions();
        _logger = logger;
        _signalSink = signalSink;
    }

    /// <summary>
    ///     Run the full CFMoM pipeline.
    /// </summary>
    public async Task<CFMoMResult<TDecision>> ExecuteAsync(
        TContext context,
        CancellationToken cancellationToken = default)
    {
        var stopwatch = Stopwatch.StartNew();
        var correlationId = Guid.NewGuid().ToString("N");

        // Clear consensus space for new run
        _consensusSpace.Clear();

        using var cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        cts.CancelAfter(_options.TotalTimeout);

        var orchestrationSignals = new ConcurrentDictionary<string, object>();
        var completedProposers = new ConcurrentDictionary<string, bool>();
        var failedProposers = new ConcurrentDictionary<string, bool>();
        var ranProposers = new HashSet<string>();

        _signalSink?.Raise($"cfmom.started:correlation={correlationId}");

        // Get enabled proposers (respecting circuit breakers)
        var availableProposers = _proposers
            .Where(p => p.IsEnabled && IsCircuitClosed(p.Name))
            .OrderBy(p => p.Priority)
            .ToList();

        _logger?.LogDebug(
            "Starting CFMoM orchestration {CorrelationId} with {ProposerCount} available proposers",
            correlationId, availableProposers.Count);

        var waveNumber = 0;
        ConstrainerResult<TDecision>? constrainerResult = null;

        try
        {
            while (waveNumber < _options.MaxWaves && !cts.Token.IsCancellationRequested)
            {
                // Build current state
                var state = BuildState(
                    context,
                    orchestrationSignals,
                    completedProposers.Keys.ToHashSet(),
                    failedProposers.Keys.ToHashSet(),
                    correlationId,
                    stopwatch.Elapsed);

                // Find proposers that can run in this wave
                var readyProposers = availableProposers
                    .Where(p => !ranProposers.Contains(p.Name))
                    .Where(p => CanRun(p, orchestrationSignals))
                    .ToArray();

                if (readyProposers.Length == 0)
                {
                    _logger?.LogDebug("Wave {Wave}: No more proposers ready, finishing", waveNumber);
                    break;
                }

                _logger?.LogDebug(
                    "Wave {Wave}: Running {Count} proposers: {Names}",
                    waveNumber,
                    readyProposers.Length,
                    string.Join(", ", readyProposers.Select(p => p.Name)));

                // Mark as ran
                foreach (var proposer in readyProposers)
                    ranProposers.Add(proposer.Name);

                // Execute wave
                await ExecuteWaveAsync(
                    readyProposers,
                    state,
                    orchestrationSignals,
                    completedProposers,
                    failedProposers,
                    waveNumber,
                    cts.Token);

                // Update system signals
                orchestrationSignals[ProposerCountTrigger.CompletedProposersSignal] = completedProposers.Count;

                // Aggregate and check constrainer
                var aggregatedResult = _aggregator.Aggregate(_consensusSpace.GetSignals());
                orchestrationSignals[ScoreThresholdTrigger.CurrentScoreSignal] = aggregatedResult.Score;

                constrainerResult = _constrainer.Evaluate(aggregatedResult, context);

                if (!constrainerResult.ShouldContinue)
                {
                    _logger?.LogDebug(
                        "Constrainer stopped orchestration at wave {Wave}: {Reason}",
                        waveNumber, constrainerResult.Reason);
                    break;
                }

                // Check consensus space early exit
                if (_consensusSpace.HasEarlyExit)
                {
                    _logger?.LogDebug(
                        "Early exit triggered at wave {Wave}: {Classification}",
                        waveNumber, _consensusSpace.EarlyExitSignal?.EarlyExitClassification);
                    break;
                }

                waveNumber++;

                // Delay between waves
                if (waveNumber < _options.MaxWaves && _options.WaveInterval > TimeSpan.Zero)
                {
                    await Task.Delay(_options.WaveInterval, cts.Token);
                }
            }
        }
        catch (OperationCanceledException) when (cts.IsCancellationRequested && !cancellationToken.IsCancellationRequested)
        {
            _logger?.LogWarning(
                "CFMoM orchestration timed out after {Elapsed}ms for {CorrelationId}",
                stopwatch.ElapsedMilliseconds, correlationId);
        }

        stopwatch.Stop();

        // Final aggregation
        var finalAggregation = _aggregator.Aggregate(_consensusSpace.GetSignals());

        // Final constrainer evaluation if not already done
        constrainerResult ??= _constrainer.Evaluate(finalAggregation, context);

        _signalSink?.Raise($"cfmom.completed:correlation={correlationId}:duration={stopwatch.ElapsedMilliseconds}ms");

        _logger?.LogDebug(
            "CFMoM completed for {CorrelationId}: Decision={Decision}, Score={Score:F2} in {Elapsed}ms, {Waves} waves",
            correlationId,
            constrainerResult.Decision,
            finalAggregation.Score,
            stopwatch.ElapsedMilliseconds,
            waveNumber);

        return new CFMoMResult<TDecision>
        {
            CorrelationId = correlationId,
            Decision = constrainerResult.Decision,
            Reason = constrainerResult.Reason,
            Aggregation = finalAggregation,
            Signals = _consensusSpace.GetSignals(),
            CompletedProposers = completedProposers.Keys.ToHashSet(),
            FailedProposers = failedProposers.Keys.ToHashSet(),
            WaveCount = waveNumber,
            TotalDurationMs = stopwatch.Elapsed.TotalMilliseconds
        };
    }

    private async Task ExecuteWaveAsync(
        IReadOnlyList<IProposer<TContext>> proposers,
        ProposerState<TContext> state,
        ConcurrentDictionary<string, object> signals,
        ConcurrentDictionary<string, bool> completedProposers,
        ConcurrentDictionary<string, bool> failedProposers,
        int waveNumber,
        CancellationToken cancellationToken)
    {
        var maxParallel = _options.MaxParallelProposers;

        // Check for per-wave parallelism override
        if (_options.ParallelismPerWave.TryGetValue(waveNumber, out var overrideValue))
        {
            maxParallel = overrideValue;
        }

        _signalSink?.Raise($"wave.started:wave={waveNumber}:proposers={proposers.Count}");

        if (maxParallel <= 1 || proposers.Count == 1)
        {
            // Sequential execution
            foreach (var proposer in proposers)
            {
                await ExecuteProposerAsync(
                    proposer, state, signals, completedProposers, failedProposers, cancellationToken);

                if (_consensusSpace.HasEarlyExit)
                    break;
            }
        }
        else
        {
            // Parallel execution with semaphore
            using var semaphore = new SemaphoreSlim(maxParallel);
            var tasks = proposers.Select(async proposer =>
            {
                await semaphore.WaitAsync(cancellationToken);
                try
                {
                    await ExecuteProposerAsync(
                        proposer, state, signals, completedProposers, failedProposers, cancellationToken);
                }
                finally
                {
                    semaphore.Release();
                }
            });

            await Task.WhenAll(tasks);
        }
    }

    private async Task ExecuteProposerAsync(
        IProposer<TContext> proposer,
        ProposerState<TContext> state,
        ConcurrentDictionary<string, object> signals,
        ConcurrentDictionary<string, bool> completedProposers,
        ConcurrentDictionary<string, bool> failedProposers,
        CancellationToken cancellationToken)
    {
        var stopwatch = Stopwatch.StartNew();
        _signalSink?.Raise($"proposer.started:{proposer.Name}");

        try
        {
            using var cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            cts.CancelAfter(proposer.ExecutionTimeout);

            var proposedSignals = await proposer.ProposeAsync(state, cts.Token);

            stopwatch.Stop();

            // Ingest signals into consensus space
            foreach (var signal in proposedSignals)
            {
                if (_consensusSpace.Ingest(signal, out var rejectionReason))
                {
                    // Merge any orchestration signals from the signal metadata
                    if (signal.Metadata.TryGetValue("orchestration_signals", out var orchSignals) &&
                        orchSignals is IDictionary<string, object> orchDict)
                    {
                        foreach (var kvp in orchDict)
                            signals[kvp.Key] = kvp.Value;
                    }
                }
                else
                {
                    _logger?.LogWarning(
                        "Signal from {Proposer} rejected: {Reason}",
                        proposer.Name, rejectionReason);
                }
            }

            completedProposers[proposer.Name] = true;
            RecordSuccess(proposer.Name);

            _signalSink?.Raise($"proposer.completed:{proposer.Name}:duration={stopwatch.ElapsedMilliseconds}ms:signals={proposedSignals.Count}");

            _logger?.LogDebug(
                "Proposer {Name} completed in {Elapsed}ms with {SignalCount} signals",
                proposer.Name, stopwatch.ElapsedMilliseconds, proposedSignals.Count);
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            stopwatch.Stop();
            HandleProposerFailure(proposer, failedProposers, "Timeout", stopwatch.ElapsedMilliseconds);
        }
        catch (Exception ex)
        {
            stopwatch.Stop();
            HandleProposerFailure(proposer, failedProposers, ex.Message, stopwatch.ElapsedMilliseconds);

            _logger?.LogWarning(ex,
                "Proposer {Name} failed after {Elapsed}ms: {Message}",
                proposer.Name, stopwatch.ElapsedMilliseconds, ex.Message);
        }
    }

    private void HandleProposerFailure(
        IProposer<TContext> proposer,
        ConcurrentDictionary<string, bool> failedProposers,
        string reason,
        double elapsedMs)
    {
        failedProposers[proposer.Name] = true;
        RecordFailure(proposer.Name);

        _signalSink?.Raise($"proposer.failed:{proposer.Name}:duration={elapsedMs}ms:reason={reason}");

        if (!proposer.IsOptional)
        {
            _logger?.LogError("Required proposer {Name} failed: {Reason}", proposer.Name, reason);
        }
    }

    private ProposerState<TContext> BuildState(
        TContext context,
        IReadOnlyDictionary<string, object> signals,
        IReadOnlySet<string> completedProposers,
        IReadOnlySet<string> failedProposers,
        string correlationId,
        TimeSpan elapsed)
    {
        var collectedSignals = _consensusSpace.GetSignals();
        var aggregation = _aggregator.Aggregate(collectedSignals);

        return new ProposerState<TContext>
        {
            Context = context,
            Signals = signals,
            CollectedSignals = collectedSignals,
            CurrentScore = aggregation.Score,
            CompletedProposers = completedProposers,
            FailedProposers = failedProposers,
            CorrelationId = correlationId,
            Elapsed = elapsed
        };
    }

    private static bool CanRun(IProposer<TContext> proposer, IReadOnlyDictionary<string, object> signals)
    {
        if (proposer.TriggerConditions.Count == 0)
            return true;

        return proposer.TriggerConditions.All(c => c.IsSatisfied(signals));
    }

    #region Circuit Breaker

    private bool IsCircuitClosed(string proposerName)
    {
        if (!_circuitStates.TryGetValue(proposerName, out var state))
            return true;

        if (state.State == CircuitBreakerState.Closed)
            return true;

        if (state.State == CircuitBreakerState.Open)
        {
            if (DateTimeOffset.UtcNow - state.LastFailure > _options.CircuitBreakerResetTime)
            {
                state.State = CircuitBreakerState.HalfOpen;
                return true;
            }
            return false;
        }

        return true; // Half-open: allow one attempt
    }

    private void RecordSuccess(string proposerName)
    {
        if (_circuitStates.TryGetValue(proposerName, out var state))
        {
            state.FailureCount = 0;
            state.State = CircuitBreakerState.Closed;
        }
    }

    private void RecordFailure(string proposerName)
    {
        var state = _circuitStates.GetOrAdd(proposerName, _ => new CircuitState());

        state.FailureCount++;
        state.LastFailure = DateTimeOffset.UtcNow;

        if (state.FailureCount >= _options.CircuitBreakerThreshold)
        {
            state.State = CircuitBreakerState.Open;
            _logger?.LogWarning(
                "Circuit breaker opened for proposer {Name} after {Count} failures",
                proposerName, state.FailureCount);
        }
    }

    #endregion

    private class CircuitState
    {
        public CircuitBreakerState State { get; set; } = CircuitBreakerState.Closed;
        public int FailureCount { get; set; }
        public DateTimeOffset LastFailure { get; set; }
    }

    private enum CircuitBreakerState
    {
        Closed,
        Open,
        HalfOpen
    }
}
