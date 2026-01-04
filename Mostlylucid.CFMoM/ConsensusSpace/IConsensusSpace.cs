using Mostlylucid.CFMoM.Signals;

namespace Mostlylucid.CFMoM.ConsensusSpace;

/// <summary>
///     The consensus space is a bus and memory, not an arbiter.
///     It performs ingestion-time validation but never makes decisions.
///     Signals pass through the ingestion gate where schema validation occurs.
///
///     Key principle: The consensus space validates at ingestion, not at decision time.
///     Cross-schema comparison is impossible by default.
/// </summary>
public interface IConsensusSpace
{
    /// <summary>
    ///     Ingest a signal through the ingestion gate.
    ///     Validates against registered schema.
    ///     Returns false if validation fails.
    /// </summary>
    bool Ingest(ConstrainedSignal signal, out string? rejectionReason);

    /// <summary>
    ///     Get all signals in the consensus space.
    /// </summary>
    IReadOnlyList<ConstrainedSignal> GetSignals();

    /// <summary>
    ///     Get signals from a specific source.
    /// </summary>
    IReadOnlyList<ConstrainedSignal> GetSignalsFrom(string sourceId);

    /// <summary>
    ///     Get signals matching a schema.
    /// </summary>
    IReadOnlyList<ConstrainedSignal> GetSignalsBySchema(string schemaId);

    /// <summary>
    ///     Get signals for a specific subject.
    /// </summary>
    IReadOnlyList<ConstrainedSignal> GetSignalsForSubject(string subjectId);

    /// <summary>
    ///     Get signals with a specific correlation ID.
    /// </summary>
    IReadOnlyList<ConstrainedSignal> GetSignalsByCorrelation(string correlationId);

    /// <summary>
    ///     Check if any signal triggered early exit.
    /// </summary>
    bool HasEarlyExit { get; }

    /// <summary>
    ///     Get the early exit signal if any.
    /// </summary>
    ConstrainedSignal? EarlyExitSignal { get; }

    /// <summary>
    ///     Register a schema validator.
    /// </summary>
    void RegisterSchema(string schemaId, ISchemaValidator validator);

    /// <summary>
    ///     Clear all signals (for new orchestration run).
    /// </summary>
    void Clear();
}

/// <summary>
///     Schema validator interface.
///     Validates signal facts against a schema at ingestion time.
/// </summary>
public interface ISchemaValidator
{
    /// <summary>
    ///     Schema ID this validator handles.
    /// </summary>
    string SchemaId { get; }

    /// <summary>
    ///     Validate a signal's facts.
    /// </summary>
    /// <param name="signal">The signal to validate.</param>
    /// <param name="error">Error message if validation fails.</param>
    /// <returns>True if valid, false if invalid.</returns>
    bool Validate(ConstrainedSignal signal, out string? error);
}

/// <summary>
///     Result of schema validation.
/// </summary>
public readonly record struct ValidationResult(bool IsValid, string? Error = null)
{
    public static ValidationResult Valid => new(true);
    public static ValidationResult Invalid(string error) => new(false, error);
}

/// <summary>
///     Default pass-through validator that accepts all signals.
/// </summary>
public sealed class PassThroughValidator : ISchemaValidator
{
    public PassThroughValidator(string schemaId)
    {
        SchemaId = schemaId;
    }

    public string SchemaId { get; }

    public bool Validate(ConstrainedSignal signal, out string? error)
    {
        error = null;
        return true;
    }
}
