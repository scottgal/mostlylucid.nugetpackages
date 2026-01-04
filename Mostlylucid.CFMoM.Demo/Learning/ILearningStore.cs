namespace Mostlylucid.CFMoM.Demo.Learning;

/// <summary>
///     Interface for the learning store.
/// </summary>
public interface ILearningStore
{
    /// <summary>
    ///     Search for similar decisions using hybrid search.
    /// </summary>
    Task<List<LearnedDecision>> SearchSimilarAsync(
        float[] embedding,
        string promptText,
        int topK = 5,
        double minSimilarity = 0.85,
        CancellationToken cancellationToken = default);

    /// <summary>
    ///     Get facts for a decision.
    /// </summary>
    Task<List<LearnedFact>> GetFactsAsync(Guid decisionId, CancellationToken cancellationToken = default);

    /// <summary>
    ///     Store a new decision with facts.
    /// </summary>
    Task StoreDecisionAsync(
        LearnedDecision decision,
        IEnumerable<LearnedFact> facts,
        CancellationToken cancellationToken = default);

    /// <summary>
    ///     Update an existing decision (merge facts, update hit count).
    /// </summary>
    Task UpdateDecisionAsync(
        Guid decisionId,
        IEnumerable<LearnedFact> newFacts,
        CancellationToken cancellationToken = default);

    /// <summary>
    ///     Increment hit count for a decision.
    /// </summary>
    Task IncrementHitCountAsync(Guid decisionId, CancellationToken cancellationToken = default);

    /// <summary>
    ///     Get statistics about the learning store.
    /// </summary>
    Task<LearningStoreStats> GetStatsAsync(CancellationToken cancellationToken = default);
}

public sealed record LearningStoreStats
{
    public int TotalDecisions { get; init; }
    public int TotalFacts { get; init; }
    public int TotalHits { get; init; }
    public DateTimeOffset? OldestEntry { get; init; }
    public DateTimeOffset? NewestEntry { get; init; }
}
