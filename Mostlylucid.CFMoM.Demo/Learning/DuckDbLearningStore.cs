using System.Data;
using DuckDB.NET.Data;
using Mostlylucid.CFMoM.Demo.Embedding;
using Mostlylucid.CFMoM.Demo.Retrieval;

namespace Mostlylucid.CFMoM.Demo.Learning;

/// <summary>
///     DuckDB-based learning store with hybrid search.
/// </summary>
public sealed class DuckDbLearningStore : ILearningStore, IDisposable
{
    private readonly DuckDBConnection _connection;
    private readonly IEmbeddingService _embeddingService;
    private readonly BM25Scorer _bm25Scorer;
    private readonly HybridRRF _hybridRrf;
    private readonly List<LearnedDecision> _cachedDecisions = [];
    private readonly object _lock = new();

    public DuckDbLearningStore(
        IEmbeddingService embeddingService,
        string dbPath = "cfmom_learning.duckdb")
    {
        _embeddingService = embeddingService;
        _bm25Scorer = new BM25Scorer();
        _hybridRrf = new HybridRRF(embeddingService, _bm25Scorer);

        var connectionString = $"Data Source={dbPath}";
        _connection = new DuckDBConnection(connectionString);
        _connection.Open();

        InitializeSchema();
        LoadCache();
    }

    private void InitializeSchema()
    {
        using var cmd = _connection.CreateCommand();
        cmd.CommandText = """
            CREATE TABLE IF NOT EXISTS learned_decisions (
                id UUID PRIMARY KEY,
                prompt_text TEXT NOT NULL,
                prompt_embedding BLOB,
                decision TEXT NOT NULL,
                reason TEXT,
                score DOUBLE,
                confidence DOUBLE,
                created_at TIMESTAMP DEFAULT CURRENT_TIMESTAMP,
                updated_at TIMESTAMP DEFAULT CURRENT_TIMESTAMP,
                hit_count INTEGER DEFAULT 0,
                feedback_score DOUBLE
            );

            CREATE TABLE IF NOT EXISTS learned_facts (
                id UUID PRIMARY KEY,
                decision_id UUID NOT NULL,
                schema_id TEXT NOT NULL,
                fact_key TEXT NOT NULL,
                fact_value TEXT NOT NULL,
                confidence DOUBLE,
                occurrence_count INTEGER DEFAULT 1,
                UNIQUE(decision_id, schema_id, fact_key)
            );

            CREATE INDEX IF NOT EXISTS idx_facts_decision ON learned_facts(decision_id);
            """;
        cmd.ExecuteNonQuery();
    }

    private void LoadCache()
    {
        lock (_lock)
        {
            _cachedDecisions.Clear();

            using var cmd = _connection.CreateCommand();
            cmd.CommandText = "SELECT * FROM learned_decisions ORDER BY hit_count DESC";

            using var reader = cmd.ExecuteReader();
            while (reader.Read())
            {
                var decision = ReadDecision(reader);
                _cachedDecisions.Add(decision);
            }

            // Initialize BM25 with cached decisions
            _bm25Scorer.Initialize(_cachedDecisions.Select(d => (d.Id.ToString(), d.PromptText)));
        }
    }

    public Task<List<LearnedDecision>> SearchSimilarAsync(
        float[] embedding,
        string promptText,
        int topK = 5,
        double minSimilarity = 0.85,
        CancellationToken cancellationToken = default)
    {
        lock (_lock)
        {
            if (_cachedDecisions.Count == 0)
                return Task.FromResult<List<LearnedDecision>>([]);

            var results = _hybridRrf.Search(
                promptText,
                embedding,
                _cachedDecisions,
                d => d.PromptEmbedding,
                d => d.PromptText,
                d => SalienceScorer.Calculate(d.Confidence, d.HitCount),
                topK);

            // Filter by minimum similarity
            var filtered = results
                .Where(r => r.DenseSimilarity >= minSimilarity)
                .Select(r => r.Item)
                .ToList();

            return Task.FromResult(filtered);
        }
    }

    public async Task<List<LearnedFact>> GetFactsAsync(Guid decisionId, CancellationToken cancellationToken = default)
    {
        var facts = new List<LearnedFact>();

        await using var cmd = _connection.CreateCommand();
        cmd.CommandText = "SELECT * FROM learned_facts WHERE decision_id = ?";
        cmd.Parameters.Add(new DuckDBParameter { Value = decisionId.ToString() });

        await using var reader = await cmd.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
        {
            facts.Add(ReadFact(reader));
        }

        return facts;
    }

    public async Task StoreDecisionAsync(
        LearnedDecision decision,
        IEnumerable<LearnedFact> facts,
        CancellationToken cancellationToken = default)
    {
        await using var transaction = _connection.BeginTransaction();

        try
        {
            // Insert decision
            await using (var cmd = _connection.CreateCommand())
            {
                cmd.Transaction = transaction;
                cmd.CommandText = """
                    INSERT INTO learned_decisions
                    (id, prompt_text, prompt_embedding, decision, reason, score, confidence, created_at, updated_at, hit_count)
                    VALUES (?, ?, ?, ?, ?, ?, ?, ?, ?, ?)
                    """;

                cmd.Parameters.Add(new DuckDBParameter { Value = decision.Id.ToString() });
                cmd.Parameters.Add(new DuckDBParameter { Value = decision.PromptText });
                cmd.Parameters.Add(new DuckDBParameter { Value = EmbeddingToBytes(decision.PromptEmbedding) });
                cmd.Parameters.Add(new DuckDBParameter { Value = decision.Decision });
                cmd.Parameters.Add(new DuckDBParameter { Value = decision.Reason });
                cmd.Parameters.Add(new DuckDBParameter { Value = decision.Score });
                cmd.Parameters.Add(new DuckDBParameter { Value = decision.Confidence });
                cmd.Parameters.Add(new DuckDBParameter { Value = decision.CreatedAt.UtcDateTime });
                cmd.Parameters.Add(new DuckDBParameter { Value = decision.UpdatedAt.UtcDateTime });
                cmd.Parameters.Add(new DuckDBParameter { Value = decision.HitCount });

                await cmd.ExecuteNonQueryAsync(cancellationToken);
            }

            // Insert facts
            foreach (var fact in facts)
            {
                await using var cmd = _connection.CreateCommand();
                cmd.Transaction = transaction;
                cmd.CommandText = """
                    INSERT INTO learned_facts
                    (id, decision_id, schema_id, fact_key, fact_value, confidence, occurrence_count)
                    VALUES (?, ?, ?, ?, ?, ?, ?)
                    ON CONFLICT (decision_id, schema_id, fact_key)
                    DO UPDATE SET
                        fact_value = EXCLUDED.fact_value,
                        confidence = (confidence + EXCLUDED.confidence) / 2,
                        occurrence_count = occurrence_count + 1
                    """;

                cmd.Parameters.Add(new DuckDBParameter { Value = fact.Id.ToString() });
                cmd.Parameters.Add(new DuckDBParameter { Value = fact.DecisionId.ToString() });
                cmd.Parameters.Add(new DuckDBParameter { Value = fact.SchemaId });
                cmd.Parameters.Add(new DuckDBParameter { Value = fact.FactKey });
                cmd.Parameters.Add(new DuckDBParameter { Value = fact.FactValue });
                cmd.Parameters.Add(new DuckDBParameter { Value = fact.Confidence });
                cmd.Parameters.Add(new DuckDBParameter { Value = fact.OccurrenceCount });

                await cmd.ExecuteNonQueryAsync(cancellationToken);
            }

            transaction.Commit();

            // Update cache
            lock (_lock)
            {
                _cachedDecisions.Add(decision);
                _bm25Scorer.Initialize(_cachedDecisions.Select(d => (d.Id.ToString(), d.PromptText)));
            }
        }
        catch
        {
            transaction.Rollback();
            throw;
        }
    }

    public async Task UpdateDecisionAsync(
        Guid decisionId,
        IEnumerable<LearnedFact> newFacts,
        CancellationToken cancellationToken = default)
    {
        // Update timestamp
        await using (var cmd = _connection.CreateCommand())
        {
            cmd.CommandText = "UPDATE learned_decisions SET updated_at = ? WHERE id = ?";
            cmd.Parameters.Add(new DuckDBParameter { Value = DateTime.UtcNow });
            cmd.Parameters.Add(new DuckDBParameter { Value = decisionId.ToString() });
            await cmd.ExecuteNonQueryAsync(cancellationToken);
        }

        // Upsert facts
        foreach (var fact in newFacts)
        {
            await using var cmd = _connection.CreateCommand();
            cmd.CommandText = """
                INSERT INTO learned_facts
                (id, decision_id, schema_id, fact_key, fact_value, confidence, occurrence_count)
                VALUES (?, ?, ?, ?, ?, ?, 1)
                ON CONFLICT (decision_id, schema_id, fact_key)
                DO UPDATE SET
                    confidence = (confidence + EXCLUDED.confidence) / 2,
                    occurrence_count = occurrence_count + 1
                """;

            cmd.Parameters.Add(new DuckDBParameter { Value = Guid.NewGuid().ToString() });
            cmd.Parameters.Add(new DuckDBParameter { Value = fact.DecisionId.ToString() });
            cmd.Parameters.Add(new DuckDBParameter { Value = fact.SchemaId });
            cmd.Parameters.Add(new DuckDBParameter { Value = fact.FactKey });
            cmd.Parameters.Add(new DuckDBParameter { Value = fact.FactValue });
            cmd.Parameters.Add(new DuckDBParameter { Value = fact.Confidence });

            await cmd.ExecuteNonQueryAsync(cancellationToken);
        }
    }

    public async Task IncrementHitCountAsync(Guid decisionId, CancellationToken cancellationToken = default)
    {
        await using var cmd = _connection.CreateCommand();
        cmd.CommandText = "UPDATE learned_decisions SET hit_count = hit_count + 1, updated_at = ? WHERE id = ?";
        cmd.Parameters.Add(new DuckDBParameter { Value = DateTime.UtcNow });
        cmd.Parameters.Add(new DuckDBParameter { Value = decisionId.ToString() });
        await cmd.ExecuteNonQueryAsync(cancellationToken);

        // Update cache
        lock (_lock)
        {
            var cached = _cachedDecisions.FirstOrDefault(d => d.Id == decisionId);
            if (cached != null)
            {
                cached.HitCount++;
                cached.UpdatedAt = DateTimeOffset.UtcNow;
            }
        }
    }

    public Task<LearningStoreStats> GetStatsAsync(CancellationToken cancellationToken = default)
    {
        lock (_lock)
        {
            var stats = new LearningStoreStats
            {
                TotalDecisions = _cachedDecisions.Count,
                TotalHits = _cachedDecisions.Sum(d => d.HitCount),
                OldestEntry = _cachedDecisions.MinBy(d => d.CreatedAt)?.CreatedAt,
                NewestEntry = _cachedDecisions.MaxBy(d => d.CreatedAt)?.CreatedAt
            };

            return Task.FromResult(stats);
        }
    }

    private static LearnedDecision ReadDecision(IDataReader reader)
    {
        return new LearnedDecision
        {
            Id = Guid.Parse(reader.GetString(reader.GetOrdinal("id"))),
            PromptText = reader.GetString(reader.GetOrdinal("prompt_text")),
            PromptEmbedding = BytesToEmbedding((byte[])reader["prompt_embedding"]),
            Decision = reader.GetString(reader.GetOrdinal("decision")),
            Reason = reader.IsDBNull(reader.GetOrdinal("reason")) ? null : reader.GetString(reader.GetOrdinal("reason")),
            Score = reader.GetDouble(reader.GetOrdinal("score")),
            Confidence = reader.GetDouble(reader.GetOrdinal("confidence")),
            CreatedAt = reader.GetDateTime(reader.GetOrdinal("created_at")),
            UpdatedAt = reader.GetDateTime(reader.GetOrdinal("updated_at")),
            HitCount = reader.GetInt32(reader.GetOrdinal("hit_count"))
        };
    }

    private static LearnedFact ReadFact(IDataReader reader)
    {
        return new LearnedFact
        {
            Id = Guid.Parse(reader.GetString(reader.GetOrdinal("id"))),
            DecisionId = Guid.Parse(reader.GetString(reader.GetOrdinal("decision_id"))),
            SchemaId = reader.GetString(reader.GetOrdinal("schema_id")),
            FactKey = reader.GetString(reader.GetOrdinal("fact_key")),
            FactValue = reader.GetString(reader.GetOrdinal("fact_value")),
            Confidence = reader.GetDouble(reader.GetOrdinal("confidence")),
            OccurrenceCount = reader.GetInt32(reader.GetOrdinal("occurrence_count"))
        };
    }

    private static byte[] EmbeddingToBytes(float[] embedding)
    {
        var bytes = new byte[embedding.Length * 4];
        Buffer.BlockCopy(embedding, 0, bytes, 0, bytes.Length);
        return bytes;
    }

    private static float[] BytesToEmbedding(byte[] bytes)
    {
        var embedding = new float[bytes.Length / 4];
        Buffer.BlockCopy(bytes, 0, embedding, 0, bytes.Length);
        return embedding;
    }

    public void Dispose()
    {
        _connection.Dispose();
    }
}
