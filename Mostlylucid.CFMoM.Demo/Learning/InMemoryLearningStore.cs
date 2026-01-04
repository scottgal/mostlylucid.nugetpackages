using Mostlylucid.CFMoM.Demo.Embedding;
using Mostlylucid.CFMoM.Demo.Retrieval;

namespace Mostlylucid.CFMoM.Demo.Learning;

/// <summary>
///     In-memory learning store for when DuckDB is not available.
///     Uses the same hybrid RRF search as the DuckDB store.
/// </summary>
public sealed class InMemoryLearningStore : ILearningStore
{
    private readonly IEmbeddingService _embeddingService;
    private readonly BM25Scorer _bm25Scorer;
    private readonly HybridRRF _hybridRrf;
    private readonly List<LearnedDecision> _decisions = [];
    private readonly Dictionary<Guid, List<LearnedFact>> _facts = [];
    private readonly object _lock = new();

    public InMemoryLearningStore(IEmbeddingService embeddingService)
    {
        _embeddingService = embeddingService;
        _bm25Scorer = new BM25Scorer();
        _hybridRrf = new HybridRRF(embeddingService, _bm25Scorer);

        Console.Error.WriteLine("[Learning] Using in-memory store (DuckDB not available)");
    }

    public Task<List<LearnedDecision>> SearchSimilarAsync(
        float[] embedding,
        string promptText,
        int topK = 5,
        double minSimilarity = 0.85,
        CancellationToken ct = default)
    {
        lock (_lock)
        {
            if (_decisions.Count == 0)
                return Task.FromResult(new List<LearnedDecision>());

            // Use hybrid RRF search
            var results = _hybridRrf.Search(
                promptText,
                embedding,
                _decisions,
                d => d.PromptEmbedding,
                d => d.PromptText,
                d => SalienceScorer.Calculate(d.Confidence, d.HitCount),
                topK);

            return Task.FromResult(results
                .Where(r => r.DenseSimilarity >= minSimilarity)
                .Select(r => r.Item)
                .ToList());
        }
    }

    public Task<List<LearnedFact>> GetFactsAsync(Guid decisionId, CancellationToken ct = default)
    {
        lock (_lock)
        {
            return Task.FromResult(_facts.GetValueOrDefault(decisionId) ?? []);
        }
    }

    public Task StoreDecisionAsync(
        LearnedDecision decision,
        IEnumerable<LearnedFact> facts,
        CancellationToken ct = default)
    {
        lock (_lock)
        {
            _decisions.Add(decision);
            _facts[decision.Id] = facts.ToList();

            // Rebuild BM25 index
            _bm25Scorer.Initialize(_decisions.Select(d => (d.Id.ToString(), d.PromptText)));
        }

        return Task.CompletedTask;
    }

    public Task IncrementHitCountAsync(Guid decisionId, CancellationToken ct = default)
    {
        lock (_lock)
        {
            var decision = _decisions.FirstOrDefault(d => d.Id == decisionId);
            if (decision != null)
            {
                decision.HitCount++;
                decision.UpdatedAt = DateTimeOffset.UtcNow;
            }
        }

        return Task.CompletedTask;
    }

    public Task UpdateDecisionAsync(
        Guid decisionId,
        IEnumerable<LearnedFact> newFacts,
        CancellationToken ct = default)
    {
        lock (_lock)
        {
            if (!_facts.TryGetValue(decisionId, out var existingFacts))
            {
                existingFacts = [];
                _facts[decisionId] = existingFacts;
            }

            foreach (var newFact in newFacts)
            {
                var existing = existingFacts.FirstOrDefault(f =>
                    f.SchemaId == newFact.SchemaId && f.FactKey == newFact.FactKey);

                if (existing != null)
                {
                    existing.FactValue = newFact.FactValue;
                    existing.OccurrenceCount++;
                }
                else
                {
                    existingFacts.Add(newFact);
                }
            }

            var decision = _decisions.FirstOrDefault(d => d.Id == decisionId);
            if (decision != null)
            {
                decision.UpdatedAt = DateTimeOffset.UtcNow;
            }
        }

        return Task.CompletedTask;
    }

    public Task<LearningStoreStats> GetStatsAsync(CancellationToken ct = default)
    {
        lock (_lock)
        {
            return Task.FromResult(new LearningStoreStats
            {
                TotalDecisions = _decisions.Count,
                TotalHits = _decisions.Sum(d => d.HitCount)
            });
        }
    }

    public void Dispose()
    {
        // Nothing to dispose
    }
}
