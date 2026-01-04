using Mostlylucid.CFMoM.Demo.Embedding;

namespace Mostlylucid.CFMoM.Demo.Retrieval;

/// <summary>
///     Hybrid search using Reciprocal Rank Fusion (RRF).
///     Combines dense (embedding), BM25 (lexical), and salience (importance) rankings.
/// </summary>
public sealed class HybridRRF
{
    private const int K = 60;  // Standard RRF constant

    private readonly IEmbeddingService _embeddingService;
    private readonly BM25Scorer _bm25Scorer;

    public HybridRRF(IEmbeddingService embeddingService, BM25Scorer bm25Scorer)
    {
        _embeddingService = embeddingService;
        _bm25Scorer = bm25Scorer;
    }

    /// <summary>
    ///     Perform hybrid search with RRF fusion.
    /// </summary>
    /// <typeparam name="T">Type of items being searched.</typeparam>
    /// <param name="query">The search query.</param>
    /// <param name="queryEmbedding">Pre-computed embedding of the query.</param>
    /// <param name="candidates">Candidate items to search.</param>
    /// <param name="getEmbedding">Function to get embedding from item.</param>
    /// <param name="getText">Function to get text from item.</param>
    /// <param name="getSalience">Function to get salience score (confidence * log(hits+1)).</param>
    /// <param name="topK">Number of results to return.</param>
    /// <returns>Ranked results with RRF scores.</returns>
    public List<RankedResult<T>> Search<T>(
        string query,
        float[] queryEmbedding,
        IEnumerable<T> candidates,
        Func<T, float[]?> getEmbedding,
        Func<T, string> getText,
        Func<T, double> getSalience,
        int topK = 5)
    {
        var candidateList = candidates.ToList();
        if (candidateList.Count == 0)
            return [];

        // Calculate dense similarity ranks
        var denseScores = candidateList
            .Select((c, i) => (Index: i, Score: CalculateDenseSimilarity(queryEmbedding, getEmbedding(c))))
            .OrderByDescending(x => x.Score)
            .Select((x, rank) => (x.Index, Rank: rank + 1))
            .ToDictionary(x => x.Index, x => x.Rank);

        // Calculate BM25 ranks
        var bm25Scores = candidateList
            .Select((c, i) => (Index: i, Score: _bm25Scorer.Score(query, getText(c))))
            .OrderByDescending(x => x.Score)
            .Select((x, rank) => (x.Index, Rank: rank + 1))
            .ToDictionary(x => x.Index, x => x.Rank);

        // Calculate salience ranks
        var salienceScores = candidateList
            .Select((c, i) => (Index: i, Score: getSalience(c)))
            .OrderByDescending(x => x.Score)
            .Select((x, rank) => (x.Index, Rank: rank + 1))
            .ToDictionary(x => x.Index, x => x.Rank);

        // RRF fusion
        var results = new List<RankedResult<T>>();

        for (int i = 0; i < candidateList.Count; i++)
        {
            var denseRank = denseScores.GetValueOrDefault(i, candidateList.Count);
            var bm25Rank = bm25Scores.GetValueOrDefault(i, candidateList.Count);
            var salienceRank = salienceScores.GetValueOrDefault(i, candidateList.Count);

            var rrfScore = 1.0 / (K + denseRank)
                         + 1.0 / (K + bm25Rank)
                         + 1.0 / (K + salienceRank);

            results.Add(new RankedResult<T>
            {
                Item = candidateList[i],
                RrfScore = rrfScore,
                DenseRank = denseRank,
                Bm25Rank = bm25Rank,
                SalienceRank = salienceRank,
                DenseSimilarity = CalculateDenseSimilarity(queryEmbedding, getEmbedding(candidateList[i]))
            });
        }

        return results
            .OrderByDescending(r => r.RrfScore)
            .Take(topK)
            .ToList();
    }

    private double CalculateDenseSimilarity(float[] queryEmbedding, float[]? docEmbedding)
    {
        if (docEmbedding == null || queryEmbedding.Length != docEmbedding.Length)
            return 0;

        return _embeddingService.CosineSimilarity(queryEmbedding, docEmbedding);
    }
}

/// <summary>
///     Result of hybrid RRF search.
/// </summary>
public sealed class RankedResult<T>
{
    public required T Item { get; init; }
    public required double RrfScore { get; init; }
    public required int DenseRank { get; init; }
    public required int Bm25Rank { get; init; }
    public required int SalienceRank { get; init; }
    public required double DenseSimilarity { get; init; }
}
