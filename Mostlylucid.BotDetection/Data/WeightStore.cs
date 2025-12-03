using System.Collections.Concurrent;
using Microsoft.Data.Sqlite;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Mostlylucid.BotDetection.Models;

namespace Mostlylucid.BotDetection.Data;

/// <summary>
///     Store for learned weights that feed back to static analyzers.
///     The learning system updates these weights, and detectors read them to adjust confidence.
/// </summary>
/// <remarks>
///     <para>
///         Key concept: Each request gets a "signature" (by path, policy, UA pattern, etc.).
///         The learning system observes patterns and updates weights for signatures.
///         Static analyzers then use these weights to boost/reduce their confidence.
///     </para>
///     <para>
///         Weight types:
///         <list type="bullet">
///             <item>UA patterns: Weight adjustments for specific User-Agent patterns</item>
///             <item>IP ranges: Weight adjustments for IP address ranges</item>
///             <item>Path patterns: Weight adjustments for request path patterns</item>
///             <item>Behavior hashes: Weight adjustments for behavioral signatures</item>
///             <item>Combined signatures: Multi-factor signature weights</item>
///         </list>
///     </para>
/// </remarks>
public interface IWeightStore
{
    /// <summary>
    ///     Get the learned weight adjustment for a signature.
    ///     Returns 0.0 if no learned weight exists.
    /// </summary>
    Task<double> GetWeightAsync(string signatureType, string signature, CancellationToken ct = default);

    /// <summary>
    ///     Get multiple weights at once (batch lookup for efficiency).
    /// </summary>
    Task<IReadOnlyDictionary<string, double>> GetWeightsAsync(
        string signatureType,
        IEnumerable<string> signatures,
        CancellationToken ct = default);

    /// <summary>
    ///     Update a learned weight. Called by the learning system in the slow path.
    /// </summary>
    Task UpdateWeightAsync(
        string signatureType,
        string signature,
        double weight,
        double confidence,
        int observationCount,
        CancellationToken ct = default);

    /// <summary>
    ///     Increment observation count and optionally adjust weight.
    ///     Used when we see a signature again and want to reinforce/decay the weight.
    /// </summary>
    Task RecordObservationAsync(
        string signatureType,
        string signature,
        bool wasBot,
        double detectionConfidence,
        CancellationToken ct = default);

    /// <summary>
    ///     Get all weights for a signature type (for bulk loading into memory cache).
    /// </summary>
    Task<IReadOnlyList<LearnedWeight>> GetAllWeightsAsync(
        string signatureType,
        CancellationToken ct = default);

    /// <summary>
    ///     Get statistics about the weight store.
    /// </summary>
    Task<WeightStoreStats> GetStatsAsync(CancellationToken ct = default);

    /// <summary>
    ///     Decay old weights that haven't been seen recently.
    /// </summary>
    Task DecayOldWeightsAsync(TimeSpan maxAge, double decayFactor, CancellationToken ct = default);
}

/// <summary>
///     A learned weight for a signature.
/// </summary>
public record LearnedWeight
{
    public required string SignatureType { get; init; }
    public required string Signature { get; init; }

    /// <summary>
    ///     The weight adjustment (-1.0 to +1.0).
    ///     Positive = more likely bot, negative = more likely human.
    /// </summary>
    public required double Weight { get; init; }

    /// <summary>
    ///     Confidence in this weight (0.0 to 1.0).
    ///     Based on observation count and consistency.
    /// </summary>
    public required double Confidence { get; init; }

    /// <summary>
    ///     Number of times this signature was observed.
    /// </summary>
    public required int ObservationCount { get; init; }

    /// <summary>
    ///     When this weight was first created.
    /// </summary>
    public required DateTimeOffset FirstSeen { get; init; }

    /// <summary>
    ///     When this weight was last updated.
    /// </summary>
    public required DateTimeOffset LastSeen { get; init; }
}

/// <summary>
///     Statistics about the weight store.
/// </summary>
public record WeightStoreStats
{
    public int TotalWeights { get; init; }
    public int UaPatternWeights { get; init; }
    public int IpRangeWeights { get; init; }
    public int PathPatternWeights { get; init; }
    public int BehaviorHashWeights { get; init; }
    public int CombinedSignatureWeights { get; init; }
    public double AverageConfidence { get; init; }
    public int HighConfidenceWeights { get; init; }
    public DateTimeOffset? OldestWeight { get; init; }
    public DateTimeOffset? NewestWeight { get; init; }
}

/// <summary>
///     Well-known signature types for the weight store.
/// </summary>
public static class SignatureTypes
{
    public const string UaPattern = "ua_pattern";
    public const string IpRange = "ip_range";
    public const string PathPattern = "path_pattern";
    public const string BehaviorHash = "behavior_hash";
    public const string CombinedSignature = "combined";
    public const string DetectorName = "detector";
    public const string HeaderPattern = "header_pattern";
}

/// <summary>
///     SQLite implementation of the weight store with in-memory caching.
/// </summary>
public class SqliteWeightStore : IWeightStore, IAsyncDisposable
{
    private readonly ILogger<SqliteWeightStore> _logger;
    private readonly string _connectionString;
    private readonly SemaphoreSlim _initLock = new(1, 1);
    private readonly ConcurrentDictionary<string, ConcurrentDictionary<string, LearnedWeight>> _cache = new();
    private bool _initialized;

    private const string TableName = "learned_weights";

    public SqliteWeightStore(
        ILogger<SqliteWeightStore> logger,
        IOptions<BotDetectionOptions> options)
    {
        _logger = logger;

        var dbPath = options.Value.DatabasePath
                     ?? Path.Combine(AppContext.BaseDirectory, "botdetection.db");

        _connectionString = $"Data Source={dbPath}";
    }

    private async Task EnsureInitializedAsync(CancellationToken ct)
    {
        if (_initialized) return;

        await _initLock.WaitAsync(ct);
        try
        {
            if (_initialized) return;

            await using var conn = new SqliteConnection(_connectionString);
            await conn.OpenAsync(ct);

            var sql = $@"
                CREATE TABLE IF NOT EXISTS {TableName} (
                    signature_type TEXT NOT NULL,
                    signature TEXT NOT NULL,
                    weight REAL NOT NULL,
                    confidence REAL NOT NULL,
                    observation_count INTEGER NOT NULL DEFAULT 1,
                    first_seen TEXT NOT NULL,
                    last_seen TEXT NOT NULL,
                    PRIMARY KEY (signature_type, signature)
                );

                CREATE INDEX IF NOT EXISTS idx_signature_type ON {TableName}(signature_type);
                CREATE INDEX IF NOT EXISTS idx_confidence ON {TableName}(confidence);
                CREATE INDEX IF NOT EXISTS idx_last_seen ON {TableName}(last_seen);
            ";

            await using var cmd = new SqliteCommand(sql, conn);
            await cmd.ExecuteNonQueryAsync(ct);

            _initialized = true;
            _logger.LogDebug("Weight store initialized");
        }
        finally
        {
            _initLock.Release();
        }
    }

    public async Task<double> GetWeightAsync(string signatureType, string signature, CancellationToken ct = default)
    {
        // Check cache first
        if (_cache.TryGetValue(signatureType, out var typeCache) &&
            typeCache.TryGetValue(signature, out var cached))
        {
            return cached.Weight * cached.Confidence; // Weighted by confidence
        }

        await EnsureInitializedAsync(ct);

        await using var conn = new SqliteConnection(_connectionString);
        await conn.OpenAsync(ct);

        var sql = $@"
            SELECT weight, confidence FROM {TableName}
            WHERE signature_type = @type AND signature = @sig
        ";

        await using var cmd = new SqliteCommand(sql, conn);
        cmd.Parameters.AddWithValue("@type", signatureType);
        cmd.Parameters.AddWithValue("@sig", signature);

        await using var reader = await cmd.ExecuteReaderAsync(ct);
        if (await reader.ReadAsync(ct))
        {
            var weight = reader.GetDouble(0);
            var confidence = reader.GetDouble(1);
            return weight * confidence;
        }

        return 0.0;
    }

    public async Task<IReadOnlyDictionary<string, double>> GetWeightsAsync(
        string signatureType,
        IEnumerable<string> signatures,
        CancellationToken ct = default)
    {
        var result = new Dictionary<string, double>();
        var sigList = signatures.ToList();

        // Check cache for all
        if (_cache.TryGetValue(signatureType, out var typeCache))
        {
            foreach (var sig in sigList)
            {
                if (typeCache.TryGetValue(sig, out var cached))
                {
                    result[sig] = cached.Weight * cached.Confidence;
                }
            }

            if (result.Count == sigList.Count)
                return result; // All found in cache
        }

        // Fetch missing from DB
        var missing = sigList.Where(s => !result.ContainsKey(s)).ToList();
        if (missing.Count == 0)
            return result;

        await EnsureInitializedAsync(ct);

        await using var conn = new SqliteConnection(_connectionString);
        await conn.OpenAsync(ct);

        // Build parameterized query for missing signatures
        var placeholders = string.Join(",", missing.Select((_, i) => $"@sig{i}"));
        var sql = $@"
            SELECT signature, weight, confidence FROM {TableName}
            WHERE signature_type = @type AND signature IN ({placeholders})
        ";

        await using var cmd = new SqliteCommand(sql, conn);
        cmd.Parameters.AddWithValue("@type", signatureType);
        for (int i = 0; i < missing.Count; i++)
        {
            cmd.Parameters.AddWithValue($"@sig{i}", missing[i]);
        }

        await using var reader = await cmd.ExecuteReaderAsync(ct);
        while (await reader.ReadAsync(ct))
        {
            var sig = reader.GetString(0);
            var weight = reader.GetDouble(1);
            var confidence = reader.GetDouble(2);
            result[sig] = weight * confidence;
        }

        return result;
    }

    public async Task UpdateWeightAsync(
        string signatureType,
        string signature,
        double weight,
        double confidence,
        int observationCount,
        CancellationToken ct = default)
    {
        await EnsureInitializedAsync(ct);

        await using var conn = new SqliteConnection(_connectionString);
        await conn.OpenAsync(ct);

        var now = DateTimeOffset.UtcNow.ToString("O");
        var sql = $@"
            INSERT INTO {TableName} (signature_type, signature, weight, confidence, observation_count, first_seen, last_seen)
            VALUES (@type, @sig, @weight, @conf, @count, @now, @now)
            ON CONFLICT(signature_type, signature) DO UPDATE SET
                weight = @weight,
                confidence = @conf,
                observation_count = @count,
                last_seen = @now
        ";

        await using var cmd = new SqliteCommand(sql, conn);
        cmd.Parameters.AddWithValue("@type", signatureType);
        cmd.Parameters.AddWithValue("@sig", signature);
        cmd.Parameters.AddWithValue("@weight", weight);
        cmd.Parameters.AddWithValue("@conf", confidence);
        cmd.Parameters.AddWithValue("@count", observationCount);
        cmd.Parameters.AddWithValue("@now", now);

        await cmd.ExecuteNonQueryAsync(ct);

        // Update cache
        var typeCache = _cache.GetOrAdd(signatureType, _ => new ConcurrentDictionary<string, LearnedWeight>());
        typeCache[signature] = new LearnedWeight
        {
            SignatureType = signatureType,
            Signature = signature,
            Weight = weight,
            Confidence = confidence,
            ObservationCount = observationCount,
            FirstSeen = DateTimeOffset.UtcNow,
            LastSeen = DateTimeOffset.UtcNow
        };

        _logger.LogDebug(
            "Updated weight: {Type}/{Signature} = {Weight:F3} (conf={Confidence:F2}, count={Count})",
            signatureType, signature, weight, confidence, observationCount);
    }

    public async Task RecordObservationAsync(
        string signatureType,
        string signature,
        bool wasBot,
        double detectionConfidence,
        CancellationToken ct = default)
    {
        await EnsureInitializedAsync(ct);

        await using var conn = new SqliteConnection(_connectionString);
        await conn.OpenAsync(ct);

        // Use exponential moving average for weight updates
        var weightDelta = wasBot ? detectionConfidence : -detectionConfidence;
        var alpha = 0.1; // Learning rate

        var now = DateTimeOffset.UtcNow.ToString("O");
        var sql = $@"
            INSERT INTO {TableName} (signature_type, signature, weight, confidence, observation_count, first_seen, last_seen)
            VALUES (@type, @sig, @delta, @conf, 1, @now, @now)
            ON CONFLICT(signature_type, signature) DO UPDATE SET
                weight = weight * (1 - @alpha) + @delta * @alpha,
                confidence = MIN(1.0, confidence + @conf * 0.01),
                observation_count = observation_count + 1,
                last_seen = @now
        ";

        await using var cmd = new SqliteCommand(sql, conn);
        cmd.Parameters.AddWithValue("@type", signatureType);
        cmd.Parameters.AddWithValue("@sig", signature);
        cmd.Parameters.AddWithValue("@delta", weightDelta);
        cmd.Parameters.AddWithValue("@conf", detectionConfidence);
        cmd.Parameters.AddWithValue("@alpha", alpha);
        cmd.Parameters.AddWithValue("@now", now);

        await cmd.ExecuteNonQueryAsync(ct);

        // Invalidate cache for this entry
        if (_cache.TryGetValue(signatureType, out var typeCache))
        {
            typeCache.TryRemove(signature, out _);
        }
    }

    public async Task<IReadOnlyList<LearnedWeight>> GetAllWeightsAsync(
        string signatureType,
        CancellationToken ct = default)
    {
        await EnsureInitializedAsync(ct);

        await using var conn = new SqliteConnection(_connectionString);
        await conn.OpenAsync(ct);

        var sql = $@"
            SELECT signature_type, signature, weight, confidence, observation_count, first_seen, last_seen
            FROM {TableName}
            WHERE signature_type = @type
            ORDER BY confidence DESC, observation_count DESC
        ";

        await using var cmd = new SqliteCommand(sql, conn);
        cmd.Parameters.AddWithValue("@type", signatureType);

        var results = new List<LearnedWeight>();
        await using var reader = await cmd.ExecuteReaderAsync(ct);
        while (await reader.ReadAsync(ct))
        {
            results.Add(new LearnedWeight
            {
                SignatureType = reader.GetString(0),
                Signature = reader.GetString(1),
                Weight = reader.GetDouble(2),
                Confidence = reader.GetDouble(3),
                ObservationCount = reader.GetInt32(4),
                FirstSeen = DateTimeOffset.Parse(reader.GetString(5)),
                LastSeen = DateTimeOffset.Parse(reader.GetString(6))
            });
        }

        // Update cache
        var typeCache = _cache.GetOrAdd(signatureType, _ => new ConcurrentDictionary<string, LearnedWeight>());
        foreach (var weight in results)
        {
            typeCache[weight.Signature] = weight;
        }

        return results;
    }

    public async Task<WeightStoreStats> GetStatsAsync(CancellationToken ct = default)
    {
        await EnsureInitializedAsync(ct);

        await using var conn = new SqliteConnection(_connectionString);
        await conn.OpenAsync(ct);

        var sql = $@"
            SELECT
                COUNT(*) as total,
                SUM(CASE WHEN signature_type = '{SignatureTypes.UaPattern}' THEN 1 ELSE 0 END) as ua_count,
                SUM(CASE WHEN signature_type = '{SignatureTypes.IpRange}' THEN 1 ELSE 0 END) as ip_count,
                SUM(CASE WHEN signature_type = '{SignatureTypes.PathPattern}' THEN 1 ELSE 0 END) as path_count,
                SUM(CASE WHEN signature_type = '{SignatureTypes.BehaviorHash}' THEN 1 ELSE 0 END) as behavior_count,
                SUM(CASE WHEN signature_type = '{SignatureTypes.CombinedSignature}' THEN 1 ELSE 0 END) as combined_count,
                AVG(confidence) as avg_confidence,
                SUM(CASE WHEN confidence > 0.7 THEN 1 ELSE 0 END) as high_confidence,
                MIN(first_seen) as oldest,
                MAX(last_seen) as newest
            FROM {TableName}
        ";

        await using var cmd = new SqliteCommand(sql, conn);
        await using var reader = await cmd.ExecuteReaderAsync(ct);

        if (await reader.ReadAsync(ct))
        {
            return new WeightStoreStats
            {
                TotalWeights = reader.IsDBNull(0) ? 0 : reader.GetInt32(0),
                UaPatternWeights = reader.IsDBNull(1) ? 0 : reader.GetInt32(1),
                IpRangeWeights = reader.IsDBNull(2) ? 0 : reader.GetInt32(2),
                PathPatternWeights = reader.IsDBNull(3) ? 0 : reader.GetInt32(3),
                BehaviorHashWeights = reader.IsDBNull(4) ? 0 : reader.GetInt32(4),
                CombinedSignatureWeights = reader.IsDBNull(5) ? 0 : reader.GetInt32(5),
                AverageConfidence = reader.IsDBNull(6) ? 0 : reader.GetDouble(6),
                HighConfidenceWeights = reader.IsDBNull(7) ? 0 : reader.GetInt32(7),
                OldestWeight = reader.IsDBNull(8) ? null : DateTimeOffset.Parse(reader.GetString(8)),
                NewestWeight = reader.IsDBNull(9) ? null : DateTimeOffset.Parse(reader.GetString(9))
            };
        }

        return new WeightStoreStats();
    }

    public async Task DecayOldWeightsAsync(TimeSpan maxAge, double decayFactor, CancellationToken ct = default)
    {
        await EnsureInitializedAsync(ct);

        await using var conn = new SqliteConnection(_connectionString);
        await conn.OpenAsync(ct);

        var cutoff = DateTimeOffset.UtcNow.Subtract(maxAge).ToString("O");

        // Decay old weights
        var sql = $@"
            UPDATE {TableName}
            SET weight = weight * @decay,
                confidence = confidence * @decay
            WHERE last_seen < @cutoff
        ";

        await using var cmd = new SqliteCommand(sql, conn);
        cmd.Parameters.AddWithValue("@decay", decayFactor);
        cmd.Parameters.AddWithValue("@cutoff", cutoff);

        var updated = await cmd.ExecuteNonQueryAsync(ct);

        // Delete weights that have decayed below threshold
        var deleteSql = $@"
            DELETE FROM {TableName}
            WHERE confidence < 0.01 OR (ABS(weight) < 0.01 AND observation_count < 5)
        ";

        await using var deleteCmd = new SqliteCommand(deleteSql, conn);
        var deleted = await deleteCmd.ExecuteNonQueryAsync(ct);

        if (updated > 0 || deleted > 0)
        {
            _logger.LogInformation(
                "Weight decay: {Updated} decayed, {Deleted} deleted",
                updated, deleted);

            // Clear cache to force reload
            _cache.Clear();
        }
    }

    public ValueTask DisposeAsync()
    {
        _initLock.Dispose();
        return ValueTask.CompletedTask;
    }
}
