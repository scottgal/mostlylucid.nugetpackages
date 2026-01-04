using System.Text.Json;

namespace Mostlylucid.CFMoM.Signals;

/// <summary>
///     Reference to evidence that supports a signal.
///     Evidence is pointer-verifiable: the signal claims evidence exists,
///     the consensus space can verify it's still available at decision time.
/// </summary>
/// <param name="Kind">Type of evidence: "chunk", "frame", "timestamp", "request", "row", etc.</param>
/// <param name="Store">Namespace where evidence lives: "documents", "video-frames", "logs", etc.</param>
/// <param name="Id">Locator within the store: chunk-12, frame-0042, t=00:13:21.200, req-abc</param>
/// <param name="Locator">Optional structured locator (span, bbox, row-range) - schema depends on Kind.</param>
/// <param name="ContentHash">Optional hash for version verification. null = trust current version.</param>
public readonly record struct EvidenceRef(
    string Kind,
    string Store,
    string Id,
    JsonElement? Locator = null,
    string? ContentHash = null)
{
    /// <summary>
    ///     Create a chunk evidence reference (for document chunks).
    /// </summary>
    public static EvidenceRef Chunk(string store, string chunkId, int? start = null, int? end = null, string? hash = null)
    {
        JsonElement? locator = null;
        if (start.HasValue || end.HasValue)
        {
            var spanJson = JsonSerializer.Serialize(new { start, end });
            locator = JsonDocument.Parse(spanJson).RootElement.Clone();
        }
        return new EvidenceRef("chunk", store, chunkId, locator, hash);
    }

    /// <summary>
    ///     Create a frame evidence reference (for video frames).
    /// </summary>
    public static EvidenceRef Frame(string store, string frameId, int? x = null, int? y = null, int? w = null, int? h = null, string? hash = null)
    {
        JsonElement? locator = null;
        if (x.HasValue || y.HasValue)
        {
            var bboxJson = JsonSerializer.Serialize(new { x, y, w, h });
            locator = JsonDocument.Parse(bboxJson).RootElement.Clone();
        }
        return new EvidenceRef("frame", store, frameId, locator, hash);
    }

    /// <summary>
    ///     Create a timestamp evidence reference (for audio/video).
    /// </summary>
    public static EvidenceRef Timestamp(string store, TimeSpan position, TimeSpan? duration = null, string? hash = null)
    {
        var timestampJson = JsonSerializer.Serialize(new { position = position.ToString(), duration = duration?.ToString() });
        var locator = JsonDocument.Parse(timestampJson).RootElement.Clone();
        return new EvidenceRef("timestamp", store, $"t={position}", locator, hash);
    }

    /// <summary>
    ///     Create a request evidence reference (for HTTP requests).
    /// </summary>
    public static EvidenceRef Request(string store, string requestId, string? hash = null)
    {
        return new EvidenceRef("request", store, requestId, null, hash);
    }

    /// <summary>
    ///     Create a row evidence reference (for database rows).
    /// </summary>
    public static EvidenceRef Row(string store, string rowId, string? hash = null)
    {
        return new EvidenceRef("row", store, rowId, null, hash);
    }
}

/// <summary>
///     Reference to an embedding vector for similarity search.
/// </summary>
/// <param name="ModelId">ID of the embedding model used.</param>
/// <param name="Vector">The embedding vector.</param>
public readonly record struct EmbeddingRef(string ModelId, float[] Vector);
