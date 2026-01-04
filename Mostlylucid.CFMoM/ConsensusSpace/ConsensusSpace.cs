using System.Collections.Concurrent;
using Microsoft.Extensions.Logging;
using Mostlylucid.CFMoM.Signals;

namespace Mostlylucid.CFMoM.ConsensusSpace;

/// <summary>
///     Default implementation of the consensus space.
///     Thread-safe signal storage with schema validation at ingestion.
/// </summary>
public sealed class ConsensusSpace : IConsensusSpace
{
    private readonly ConcurrentDictionary<string, ISchemaValidator> _validators = new();
    private readonly List<ConstrainedSignal> _signals = new();
    private readonly object _lock = new();
    private readonly ILogger<ConsensusSpace>? _logger;
    private readonly ConsensusSpaceOptions _options;

    private ConstrainedSignal? _earlyExitSignal;

    public ConsensusSpace(ConsensusSpaceOptions? options = null, ILogger<ConsensusSpace>? logger = null)
    {
        _options = options ?? new ConsensusSpaceOptions();
        _logger = logger;
    }

    /// <inheritdoc />
    public bool HasEarlyExit
    {
        get
        {
            lock (_lock)
            {
                return _earlyExitSignal != null;
            }
        }
    }

    /// <inheritdoc />
    public ConstrainedSignal? EarlyExitSignal
    {
        get
        {
            lock (_lock)
            {
                return _earlyExitSignal;
            }
        }
    }

    /// <inheritdoc />
    public bool Ingest(ConstrainedSignal signal, out string? rejectionReason)
    {
        rejectionReason = null;

        // Validate schema
        if (_validators.TryGetValue(signal.FactsSchemaId, out var validator))
        {
            if (!validator.Validate(signal, out var error))
            {
                rejectionReason = $"Schema validation failed for '{signal.FactsSchemaId}': {error}";
                _logger?.LogWarning(
                    "Signal {SignalId} from {Source} rejected: {Reason}",
                    signal.Id, signal.SourceId, rejectionReason);
                return false;
            }
        }
        else if (_options.RequireRegisteredSchema)
        {
            rejectionReason = $"No validator registered for schema '{signal.FactsSchemaId}'";
            _logger?.LogWarning(
                "Signal {SignalId} from {Source} rejected: {Reason}",
                signal.Id, signal.SourceId, rejectionReason);
            return false;
        }

        // Add to consensus space
        lock (_lock)
        {
            if (_signals.Count >= _options.MaxSignals)
            {
                rejectionReason = $"Consensus space at capacity ({_options.MaxSignals})";
                _logger?.LogWarning(
                    "Signal {SignalId} from {Source} rejected: {Reason}",
                    signal.Id, signal.SourceId, rejectionReason);
                return false;
            }

            _signals.Add(signal);

            // Check for early exit
            if (signal.TriggerEarlyExit && _earlyExitSignal == null)
            {
                _earlyExitSignal = signal;
                _logger?.LogInformation(
                    "Early exit triggered by {Source}: {Classification}",
                    signal.SourceId, signal.EarlyExitClassification);
            }
        }

        _logger?.LogDebug(
            "Signal {SignalId} ingested from {Source} (schema: {Schema}, confidence: {Confidence:F2})",
            signal.Id, signal.SourceId, signal.FactsSchemaId, signal.Confidence);

        return true;
    }

    /// <inheritdoc />
    public IReadOnlyList<ConstrainedSignal> GetSignals()
    {
        lock (_lock)
        {
            return _signals.ToList();
        }
    }

    /// <inheritdoc />
    public IReadOnlyList<ConstrainedSignal> GetSignalsFrom(string sourceId)
    {
        lock (_lock)
        {
            return _signals.Where(s => s.SourceId == sourceId).ToList();
        }
    }

    /// <inheritdoc />
    public IReadOnlyList<ConstrainedSignal> GetSignalsBySchema(string schemaId)
    {
        lock (_lock)
        {
            return _signals.Where(s => s.FactsSchemaId == schemaId).ToList();
        }
    }

    /// <inheritdoc />
    public IReadOnlyList<ConstrainedSignal> GetSignalsForSubject(string subjectId)
    {
        lock (_lock)
        {
            return _signals.Where(s => s.SubjectId == subjectId).ToList();
        }
    }

    /// <inheritdoc />
    public IReadOnlyList<ConstrainedSignal> GetSignalsByCorrelation(string correlationId)
    {
        lock (_lock)
        {
            return _signals.Where(s => s.CorrelationId == correlationId).ToList();
        }
    }

    /// <inheritdoc />
    public void RegisterSchema(string schemaId, ISchemaValidator validator)
    {
        _validators[schemaId] = validator;
        _logger?.LogDebug("Registered schema validator for '{SchemaId}'", schemaId);
    }

    /// <inheritdoc />
    public void Clear()
    {
        lock (_lock)
        {
            _signals.Clear();
            _earlyExitSignal = null;
        }
    }
}

/// <summary>
///     Options for the consensus space.
/// </summary>
public sealed class ConsensusSpaceOptions
{
    /// <summary>
    ///     Maximum number of signals to store. Default: 1000.
    /// </summary>
    public int MaxSignals { get; set; } = 1000;

    /// <summary>
    ///     Whether to require a registered schema for all signals.
    ///     If false, signals with unknown schemas are accepted.
    ///     Default: false
    /// </summary>
    public bool RequireRegisteredSchema { get; set; } = false;
}
