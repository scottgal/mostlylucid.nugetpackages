using System.Diagnostics;
using System.Text.Json;
using Microsoft.Data.Sqlite;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Mostlylucid.RagLlmSearch.Configuration;
using Mostlylucid.RagLlmSearch.LlmServices;
using Mostlylucid.RagLlmSearch.Models;
using Mostlylucid.RagLlmSearch.Telemetry;

namespace Mostlylucid.RagLlmSearch.Rag;

/// <summary>
/// SQLite-based RAG service with vector similarity search
/// </summary>
public class SqliteRagService : IRagService, IAsyncDisposable
{
    private readonly RagLlmSearchOptions _options;
    private readonly ILlmService _llmService;
    private readonly ILogger<SqliteRagService> _logger;
    private SqliteConnection? _connection;
    private readonly SemaphoreSlim _lock = new(1, 1);
    private bool _initialized;

    public SqliteRagService(
        IOptions<RagLlmSearchOptions> options,
        ILlmService llmService,
        ILogger<SqliteRagService> logger)
    {
        _options = options.Value;
        _llmService = llmService;
        _logger = logger;
    }

    public async Task InitializeAsync(CancellationToken cancellationToken = default)
    {
        if (_initialized) return;

        await _lock.WaitAsync(cancellationToken);
        try
        {
            if (_initialized) return;

            var connectionString = $"Data Source={_options.DatabasePath}";
            _connection = new SqliteConnection(connectionString);
            await _connection.OpenAsync(cancellationToken);

            // Create documents table
            var createTableSql = """
                CREATE TABLE IF NOT EXISTS rag_documents (
                    id TEXT PRIMARY KEY,
                    content TEXT NOT NULL,
                    title TEXT,
                    source_url TEXT,
                    document_type TEXT,
                    created_at TEXT NOT NULL,
                    last_accessed_at TEXT NOT NULL,
                    access_count INTEGER DEFAULT 0,
                    metadata TEXT,
                    embedding BLOB
                );

                CREATE INDEX IF NOT EXISTS idx_rag_documents_type ON rag_documents(document_type);
                CREATE INDEX IF NOT EXISTS idx_rag_documents_created ON rag_documents(created_at);
                """;

            await using var command = new SqliteCommand(createTableSql, _connection);
            await command.ExecuteNonQueryAsync(cancellationToken);

            _initialized = true;
            _logger.LogInformation("RAG service initialized with database: {Path}", _options.DatabasePath);
        }
        finally
        {
            _lock.Release();
        }
    }

    public async Task AddDocumentAsync(RagDocument document, CancellationToken cancellationToken = default)
    {
        using var activity = RagSearchTelemetry.StartAddDocumentActivity(document.Id, document.DocumentType);

        try
        {
            await EnsureInitializedAsync(cancellationToken);

            // Generate embedding if not provided
            if (document.Embedding == null || document.Embedding.Length == 0)
            {
                document.Embedding = await _llmService.GenerateEmbeddingAsync(document.Content, cancellationToken);
            }

            await _lock.WaitAsync(cancellationToken);
            try
            {
                var sql = """
                    INSERT OR REPLACE INTO rag_documents
                    (id, content, title, source_url, document_type, created_at, last_accessed_at, access_count, metadata, embedding)
                    VALUES (@id, @content, @title, @source_url, @document_type, @created_at, @last_accessed_at, @access_count, @metadata, @embedding)
                    """;

                await using var command = new SqliteCommand(sql, _connection);
                command.Parameters.AddWithValue("@id", document.Id);
                command.Parameters.AddWithValue("@content", document.Content);
                command.Parameters.AddWithValue("@title", document.Title ?? (object)DBNull.Value);
                command.Parameters.AddWithValue("@source_url", document.SourceUrl ?? (object)DBNull.Value);
                command.Parameters.AddWithValue("@document_type", document.DocumentType);
                command.Parameters.AddWithValue("@created_at", document.CreatedAt.ToString("O"));
                command.Parameters.AddWithValue("@last_accessed_at", document.LastAccessedAt.ToString("O"));
                command.Parameters.AddWithValue("@access_count", document.AccessCount);
                command.Parameters.AddWithValue("@metadata", JsonSerializer.Serialize(document.Metadata));
                command.Parameters.AddWithValue("@embedding", SerializeEmbedding(document.Embedding));

                await command.ExecuteNonQueryAsync(cancellationToken);
                _logger.LogDebug("Added document {Id} to RAG store", document.Id);
                RagSearchTelemetry.RecordDocumentAdded(activity);
            }
            finally
            {
                _lock.Release();
            }
        }
        catch (Exception ex)
        {
            RagSearchTelemetry.RecordException(activity, ex);
            throw;
        }
    }

    public async Task AddDocumentsAsync(IEnumerable<RagDocument> documents, CancellationToken cancellationToken = default)
    {
        foreach (var document in documents)
        {
            await AddDocumentAsync(document, cancellationToken);
        }
    }

    public async Task<List<RagSearchResult>> SearchAsync(
        string query,
        int maxResults = 5,
        float minScore = 0.5f,
        CancellationToken cancellationToken = default)
    {
        using var activity = RagSearchTelemetry.StartRagSearchActivity(query, maxResults, minScore);
        var stopwatch = Stopwatch.StartNew();

        try
        {
            await EnsureInitializedAsync(cancellationToken);

            // Generate query embedding
            var queryEmbedding = await _llmService.GenerateEmbeddingAsync(query, cancellationToken);
            if (queryEmbedding.Length == 0)
            {
                _logger.LogWarning("Failed to generate query embedding");
                stopwatch.Stop();
                RagSearchTelemetry.RecordRagSearchResult(activity, 0, stopwatch.ElapsedMilliseconds);
                return new List<RagSearchResult>();
            }

            await _lock.WaitAsync(cancellationToken);
            try
            {
                var sql = "SELECT * FROM rag_documents WHERE embedding IS NOT NULL";
                await using var command = new SqliteCommand(sql, _connection);

                var results = new List<(RagDocument Document, float Score)>();

                await using var reader = await command.ExecuteReaderAsync(cancellationToken);
                while (await reader.ReadAsync(cancellationToken))
                {
                    var document = ReadDocument(reader);
                    if (document.Embedding != null && document.Embedding.Length > 0)
                    {
                        var score = CosineSimilarity(queryEmbedding, document.Embedding);
                        if (score >= minScore)
                        {
                            results.Add((document, score));
                        }
                    }
                }

                // Sort by score descending and take top results
                var topResults = results
                    .OrderByDescending(r => r.Score)
                    .Take(maxResults)
                    .Select(r => new RagSearchResult { Document = r.Document, Score = r.Score })
                    .ToList();

                // Update access count and timestamp for retrieved documents
                foreach (var result in topResults)
                {
                    await UpdateAccessAsync(result.Document.Id, cancellationToken);
                }

                _logger.LogDebug("RAG search found {Count} results for query", topResults.Count);

                stopwatch.Stop();
                RagSearchTelemetry.RecordRagSearchResult(activity, topResults.Count, stopwatch.ElapsedMilliseconds);

                return topResults;
            }
            finally
            {
                _lock.Release();
            }
        }
        catch (Exception ex)
        {
            stopwatch.Stop();
            RagSearchTelemetry.RecordException(activity, ex);
            throw;
        }
    }

    public async Task<RagDocument?> GetDocumentAsync(string id, CancellationToken cancellationToken = default)
    {
        await EnsureInitializedAsync(cancellationToken);

        await _lock.WaitAsync(cancellationToken);
        try
        {
            var sql = "SELECT * FROM rag_documents WHERE id = @id";
            await using var command = new SqliteCommand(sql, _connection);
            command.Parameters.AddWithValue("@id", id);

            await using var reader = await command.ExecuteReaderAsync(cancellationToken);
            if (await reader.ReadAsync(cancellationToken))
            {
                return ReadDocument(reader);
            }

            return null;
        }
        finally
        {
            _lock.Release();
        }
    }

    public async Task DeleteDocumentAsync(string id, CancellationToken cancellationToken = default)
    {
        await EnsureInitializedAsync(cancellationToken);

        await _lock.WaitAsync(cancellationToken);
        try
        {
            var sql = "DELETE FROM rag_documents WHERE id = @id";
            await using var command = new SqliteCommand(sql, _connection);
            command.Parameters.AddWithValue("@id", id);
            await command.ExecuteNonQueryAsync(cancellationToken);
        }
        finally
        {
            _lock.Release();
        }
    }

    public async Task ClearAsync(CancellationToken cancellationToken = default)
    {
        await EnsureInitializedAsync(cancellationToken);

        await _lock.WaitAsync(cancellationToken);
        try
        {
            var sql = "DELETE FROM rag_documents";
            await using var command = new SqliteCommand(sql, _connection);
            await command.ExecuteNonQueryAsync(cancellationToken);
            _logger.LogInformation("Cleared all documents from RAG store");
        }
        finally
        {
            _lock.Release();
        }
    }

    public async Task<int> GetDocumentCountAsync(CancellationToken cancellationToken = default)
    {
        await EnsureInitializedAsync(cancellationToken);

        await _lock.WaitAsync(cancellationToken);
        try
        {
            var sql = "SELECT COUNT(*) FROM rag_documents";
            await using var command = new SqliteCommand(sql, _connection);
            var result = await command.ExecuteScalarAsync(cancellationToken);
            return Convert.ToInt32(result);
        }
        finally
        {
            _lock.Release();
        }
    }

    private async Task EnsureInitializedAsync(CancellationToken cancellationToken)
    {
        if (!_initialized)
        {
            await InitializeAsync(cancellationToken);
        }
    }

    private async Task UpdateAccessAsync(string id, CancellationToken cancellationToken)
    {
        var sql = """
            UPDATE rag_documents
            SET last_accessed_at = @last_accessed_at, access_count = access_count + 1
            WHERE id = @id
            """;
        await using var command = new SqliteCommand(sql, _connection);
        command.Parameters.AddWithValue("@id", id);
        command.Parameters.AddWithValue("@last_accessed_at", DateTime.UtcNow.ToString("O"));
        await command.ExecuteNonQueryAsync(cancellationToken);
    }

    private RagDocument ReadDocument(SqliteDataReader reader)
    {
        var metadataJson = reader["metadata"] as string ?? "{}";
        var embeddingBytes = reader["embedding"] as byte[];

        return new RagDocument
        {
            Id = reader["id"].ToString() ?? string.Empty,
            Content = reader["content"].ToString() ?? string.Empty,
            Title = reader["title"] as string ?? string.Empty,
            SourceUrl = reader["source_url"] as string,
            DocumentType = reader["document_type"].ToString() ?? "general",
            CreatedAt = DateTime.Parse(reader["created_at"].ToString() ?? DateTime.UtcNow.ToString("O")),
            LastAccessedAt = DateTime.Parse(reader["last_accessed_at"].ToString() ?? DateTime.UtcNow.ToString("O")),
            AccessCount = Convert.ToInt32(reader["access_count"]),
            Metadata = JsonSerializer.Deserialize<Dictionary<string, string>>(metadataJson) ?? new(),
            Embedding = embeddingBytes != null ? DeserializeEmbedding(embeddingBytes) : null
        };
    }

    private static byte[] SerializeEmbedding(float[] embedding)
    {
        var bytes = new byte[embedding.Length * sizeof(float)];
        Buffer.BlockCopy(embedding, 0, bytes, 0, bytes.Length);
        return bytes;
    }

    private static float[] DeserializeEmbedding(byte[] bytes)
    {
        var embedding = new float[bytes.Length / sizeof(float)];
        Buffer.BlockCopy(bytes, 0, embedding, 0, bytes.Length);
        return embedding;
    }

    private static float CosineSimilarity(float[] a, float[] b)
    {
        if (a.Length != b.Length || a.Length == 0) return 0;

        float dotProduct = 0;
        float normA = 0;
        float normB = 0;

        for (int i = 0; i < a.Length; i++)
        {
            dotProduct += a[i] * b[i];
            normA += a[i] * a[i];
            normB += b[i] * b[i];
        }

        var denominator = Math.Sqrt(normA) * Math.Sqrt(normB);
        if (denominator == 0) return 0;

        return (float)(dotProduct / denominator);
    }

    public async ValueTask DisposeAsync()
    {
        if (_connection != null)
        {
            await _connection.DisposeAsync();
        }
        _lock.Dispose();
    }
}
