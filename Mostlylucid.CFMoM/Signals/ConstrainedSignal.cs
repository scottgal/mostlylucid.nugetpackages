using System.Collections.Immutable;
using System.Text.Json;

namespace Mostlylucid.CFMoM.Signals;

/// <summary>
///     The core signal contract for CFMoM.
///     Signals are immutable, typed records with:
///     - Schema-validated facts
///     - Pointer-verifiable evidence
///     - Source attribution
///     - Optional embeddings for similarity
///
///     Invariants:
///     1. Signals are immutable after creation
///     2. Facts validate against their schema at ingestion
///     3. Evidence is pointer-verifiable (not necessarily verified at ingestion)
///     4. Only the constrainer can trigger side effects
/// </summary>
public sealed record ConstrainedSignal
{
    /// <summary>
    ///     Unique identifier for this signal.
    /// </summary>
    public required string Id { get; init; }

    /// <summary>
    ///     Identifier of the proposer that emitted this signal.
    /// </summary>
    public required string SourceId { get; init; }

    /// <summary>
    ///     Schema ID for the Facts field. Used for validation at ingestion.
    ///     Example: "detection.v1", "classification.v2", "entity-extract.v1"
    /// </summary>
    public required string FactsSchemaId { get; init; }

    /// <summary>
    ///     When this signal was emitted.
    /// </summary>
    public required DateTimeOffset At { get; init; }

    /// <summary>
    ///     Optional correlation ID for grouping related signals.
    /// </summary>
    public string? CorrelationId { get; init; }

    /// <summary>
    ///     Optional subject ID (what this signal is about).
    /// </summary>
    public string? SubjectId { get; init; }

    /// <summary>
    ///     The facts payload. Structure depends on FactsSchemaId.
    ///     Validated at ingestion against the registered schema.
    /// </summary>
    public required JsonElement Facts { get; init; }

    /// <summary>
    ///     Evidence references supporting this signal.
    ///     Can be empty if signal is self-evident.
    /// </summary>
    public IReadOnlyList<EvidenceRef> Evidence { get; init; } = Array.Empty<EvidenceRef>();

    /// <summary>
    ///     Confidence level (0.0 to 1.0) from the proposer.
    ///     Note: This is probabilistic input, not deterministic truth.
    /// </summary>
    public float Confidence { get; init; }

    /// <summary>
    ///     Optional embeddings for similarity search.
    /// </summary>
    public IReadOnlyList<EmbeddingRef>? Embeddings { get; init; }

    /// <summary>
    ///     Additional metadata (tags, processing hints, etc.)
    /// </summary>
    public ImmutableDictionary<string, object> Metadata { get; init; } =
        ImmutableDictionary<string, object>.Empty;

    /// <summary>
    ///     Whether this signal triggers early exit in orchestration.
    /// </summary>
    public bool TriggerEarlyExit { get; init; }

    /// <summary>
    ///     Early exit classification if TriggerEarlyExit is true.
    /// </summary>
    public string? EarlyExitClassification { get; init; }

    /// <summary>
    ///     Create a new signal with a generated ID.
    /// </summary>
    public static ConstrainedSignal Create(
        string sourceId,
        string factsSchemaId,
        JsonElement facts,
        float confidence = 0.5f,
        IReadOnlyList<EvidenceRef>? evidence = null,
        string? correlationId = null,
        string? subjectId = null)
    {
        return new ConstrainedSignal
        {
            Id = Guid.NewGuid().ToString("N"),
            SourceId = sourceId,
            FactsSchemaId = factsSchemaId,
            At = DateTimeOffset.UtcNow,
            CorrelationId = correlationId,
            SubjectId = subjectId,
            Facts = facts,
            Evidence = evidence ?? Array.Empty<EvidenceRef>(),
            Confidence = Math.Clamp(confidence, 0f, 1f)
        };
    }

    /// <summary>
    ///     Create a signal from a typed facts object.
    /// </summary>
    public static ConstrainedSignal Create<TFacts>(
        string sourceId,
        string factsSchemaId,
        TFacts facts,
        float confidence = 0.5f,
        IReadOnlyList<EvidenceRef>? evidence = null,
        string? correlationId = null,
        string? subjectId = null)
    {
        var factsJson = JsonSerializer.SerializeToElement(facts);
        return Create(sourceId, factsSchemaId, factsJson, confidence, evidence, correlationId, subjectId);
    }

    /// <summary>
    ///     Get facts as a typed object.
    /// </summary>
    public TFacts? GetFacts<TFacts>()
    {
        return JsonSerializer.Deserialize<TFacts>(Facts);
    }

    /// <summary>
    ///     Create a copy with additional metadata.
    /// </summary>
    public ConstrainedSignal WithMetadata(string key, object value)
    {
        return this with { Metadata = Metadata.SetItem(key, value) };
    }

    /// <summary>
    ///     Create a copy marked as early exit.
    /// </summary>
    public ConstrainedSignal WithEarlyExit(string classification)
    {
        return this with
        {
            TriggerEarlyExit = true,
            EarlyExitClassification = classification
        };
    }
}
