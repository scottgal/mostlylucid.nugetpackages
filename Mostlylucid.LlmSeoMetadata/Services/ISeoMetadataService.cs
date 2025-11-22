using Mostlylucid.LlmSeoMetadata.Models;

namespace Mostlylucid.LlmSeoMetadata.Services;

/// <summary>
///     Service for generating SEO metadata using local LLMs
/// </summary>
public interface ISeoMetadataService
{
    /// <summary>
    ///     Whether the service is ready (LLM connection verified)
    /// </summary>
    bool IsReady { get; }

    /// <summary>
    ///     Generate complete SEO metadata for content
    /// </summary>
    /// <param name="request">Generation request with content and options</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Generated SEO metadata</returns>
    Task<GenerationResponse> GenerateMetadataAsync(GenerationRequest request, CancellationToken cancellationToken = default);

    /// <summary>
    ///     Generate only meta description
    /// </summary>
    /// <param name="content">Content input</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Meta description string</returns>
    Task<string?> GenerateMetaDescriptionAsync(ContentInput content, CancellationToken cancellationToken = default);

    /// <summary>
    ///     Generate only OpenGraph metadata
    /// </summary>
    /// <param name="content">Content input</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>OpenGraph metadata</returns>
    Task<OpenGraphMetadata?> GenerateOpenGraphAsync(ContentInput content, CancellationToken cancellationToken = default);

    /// <summary>
    ///     Generate only JSON-LD structured data
    /// </summary>
    /// <param name="content">Content input</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>JSON-LD metadata</returns>
    Task<JsonLdMetadata?> GenerateJsonLdAsync(ContentInput content, CancellationToken cancellationToken = default);

    /// <summary>
    ///     Generate keywords from content
    /// </summary>
    /// <param name="content">Content input</param>
    /// <param name="maxKeywords">Maximum number of keywords (default: 10)</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>List of keywords</returns>
    Task<List<string>> GenerateKeywordsAsync(ContentInput content, int maxKeywords = 10, CancellationToken cancellationToken = default);

    /// <summary>
    ///     Get cached metadata for a content key
    /// </summary>
    /// <param name="cacheKey">Cache key</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Cached metadata or null</returns>
    Task<SeoMetadata?> GetCachedMetadataAsync(string cacheKey, CancellationToken cancellationToken = default);

    /// <summary>
    ///     Store metadata in cache
    /// </summary>
    /// <param name="cacheKey">Cache key</param>
    /// <param name="metadata">Metadata to cache</param>
    /// <param name="cancellationToken">Cancellation token</param>
    Task CacheMetadataAsync(string cacheKey, SeoMetadata metadata, CancellationToken cancellationToken = default);

    /// <summary>
    ///     Clear cached metadata for a key
    /// </summary>
    /// <param name="cacheKey">Cache key</param>
    /// <param name="cancellationToken">Cancellation token</param>
    Task ClearCacheAsync(string cacheKey, CancellationToken cancellationToken = default);

    /// <summary>
    ///     Get service statistics
    /// </summary>
    SeoMetadataStatistics GetStatistics();
}

/// <summary>
///     Statistics for SEO metadata service
/// </summary>
public class SeoMetadataStatistics
{
    /// <summary>
    ///     Total generation requests
    /// </summary>
    public long TotalRequests { get; set; }

    /// <summary>
    ///     Successful generations
    /// </summary>
    public long SuccessfulGenerations { get; set; }

    /// <summary>
    ///     Failed generations
    /// </summary>
    public long FailedGenerations { get; set; }

    /// <summary>
    ///     Cache hits
    /// </summary>
    public long CacheHits { get; set; }

    /// <summary>
    ///     Average generation time in milliseconds
    /// </summary>
    public double AverageGenerationTimeMs { get; set; }

    /// <summary>
    ///     LLM model in use
    /// </summary>
    public string? Model { get; set; }

    /// <summary>
    ///     Whether LLM connection is healthy
    /// </summary>
    public bool LlmConnectionHealthy { get; set; }

    /// <summary>
    ///     Last successful generation time
    /// </summary>
    public DateTime? LastSuccessfulGeneration { get; set; }

    /// <summary>
    ///     Number of items in cache
    /// </summary>
    public int CachedItems { get; set; }
}
