using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Mostlylucid.LlmSeoMetadata.Models;
using Mostlylucid.LlmSeoMetadata.Services;

namespace Mostlylucid.LlmSeoMetadata.Data;

/// <summary>
///     SEO metadata service wrapper with database caching
/// </summary>
public class CachedSeoMetadataService : ISeoMetadataService
{
    private readonly SeoCacheOptions _cacheOptions;
    private readonly SeoMetadataDbContext _dbContext;
    private readonly ISeoMetadataService _innerService;
    private readonly ILogger<CachedSeoMetadataService> _logger;

    // Additional stats for database caching
    private long _dbCacheHits;
    private long _dbCacheWrites;

    public CachedSeoMetadataService(
        ISeoMetadataService innerService,
        SeoMetadataDbContext dbContext,
        IOptions<SeoCacheOptions> cacheOptions,
        ILogger<CachedSeoMetadataService> logger)
    {
        _innerService = innerService;
        _dbContext = dbContext;
        _cacheOptions = cacheOptions.Value;
        _logger = logger;
    }

    /// <inheritdoc />
    public bool IsReady => _innerService.IsReady;

    /// <inheritdoc />
    public async Task<GenerationResponse> GenerateMetadataAsync(GenerationRequest request,
        CancellationToken cancellationToken = default)
    {
        var cacheKey = request.Content.GetCacheKey();

        // Check database cache first if enabled and not forcing regeneration
        if (request.UseCache && !request.ForceRegenerate)
        {
            var cached = await GetFromDatabaseAsync(cacheKey, cancellationToken);
            if (cached != null)
            {
                Interlocked.Increment(ref _dbCacheHits);
                return new GenerationResponse
                {
                    Success = true,
                    Metadata = cached,
                    FromCache = true,
                    CacheKey = cacheKey
                };
            }
        }

        // Generate new metadata
        var result = await _innerService.GenerateMetadataAsync(request, cancellationToken);

        // Store in database cache if successful
        if (result.Success && result.Metadata != null && request.UseCache)
        {
            await SaveToDatabaseAsync(cacheKey, result.Metadata, request.Content, cancellationToken);
            Interlocked.Increment(ref _dbCacheWrites);
        }

        return result;
    }

    /// <inheritdoc />
    public Task<string?> GenerateMetaDescriptionAsync(ContentInput content,
        CancellationToken cancellationToken = default)
    {
        return _innerService.GenerateMetaDescriptionAsync(content, cancellationToken);
    }

    /// <inheritdoc />
    public Task<OpenGraphMetadata?> GenerateOpenGraphAsync(ContentInput content,
        CancellationToken cancellationToken = default)
    {
        return _innerService.GenerateOpenGraphAsync(content, cancellationToken);
    }

    /// <inheritdoc />
    public Task<JsonLdMetadata?> GenerateJsonLdAsync(ContentInput content,
        CancellationToken cancellationToken = default)
    {
        return _innerService.GenerateJsonLdAsync(content, cancellationToken);
    }

    /// <inheritdoc />
    public Task<List<string>> GenerateKeywordsAsync(ContentInput content, int maxKeywords = 10,
        CancellationToken cancellationToken = default)
    {
        return _innerService.GenerateKeywordsAsync(content, maxKeywords, cancellationToken);
    }

    /// <inheritdoc />
    public async Task<SeoMetadata?> GetCachedMetadataAsync(string cacheKey,
        CancellationToken cancellationToken = default)
    {
        // Try database cache first
        var dbCached = await GetFromDatabaseAsync(cacheKey, cancellationToken);
        if (dbCached != null)
            return dbCached;

        // Fall back to inner service (memory cache)
        return await _innerService.GetCachedMetadataAsync(cacheKey, cancellationToken);
    }

    /// <inheritdoc />
    public async Task CacheMetadataAsync(string cacheKey, SeoMetadata metadata,
        CancellationToken cancellationToken = default)
    {
        // Store in both database and memory cache
        await SaveToDatabaseAsync(cacheKey, metadata, null, cancellationToken);
        await _innerService.CacheMetadataAsync(cacheKey, metadata, cancellationToken);
    }

    /// <inheritdoc />
    public async Task ClearCacheAsync(string cacheKey, CancellationToken cancellationToken = default)
    {
        // Clear from database
        try
        {
            var entry = await _dbContext.CachedMetadata
                .FirstOrDefaultAsync(e => e.CacheKey == cacheKey, cancellationToken);

            if (entry != null)
            {
                _dbContext.CachedMetadata.Remove(entry);
                await _dbContext.SaveChangesAsync(cancellationToken);
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to clear cache entry from database: {CacheKey}", cacheKey);
        }

        // Clear from memory cache
        await _innerService.ClearCacheAsync(cacheKey, cancellationToken);
    }

    /// <inheritdoc />
    public SeoMetadataStatistics GetStatistics()
    {
        var stats = _innerService.GetStatistics();

        // Add database cache stats
        stats.CacheHits += Interlocked.Read(ref _dbCacheHits);

        // Get database item count
        try
        {
            stats.CachedItems = _dbContext.CachedMetadata.Count();
        }
        catch
        {
            // Ignore errors when getting count
        }

        return stats;
    }

    #region Private Methods

    private async Task<SeoMetadata?> GetFromDatabaseAsync(string cacheKey, CancellationToken cancellationToken)
    {
        try
        {
            var entry = await _dbContext.CachedMetadata
                .FirstOrDefaultAsync(e => e.CacheKey == cacheKey && e.ExpiresAt > DateTime.UtcNow, cancellationToken);

            if (entry == null)
                return null;

            // Update access stats
            entry.AccessCount++;
            entry.LastAccessedAt = DateTime.UtcNow;
            await _dbContext.SaveChangesAsync(cancellationToken);

            // Deserialize metadata
            return JsonSerializer.Deserialize<SeoMetadata>(entry.MetadataJson);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to retrieve cached metadata from database: {CacheKey}", cacheKey);
            return null;
        }
    }

    private async Task SaveToDatabaseAsync(string cacheKey, SeoMetadata metadata, ContentInput? content,
        CancellationToken cancellationToken)
    {
        try
        {
            var json = JsonSerializer.Serialize(metadata);
            var expiresAt = DateTime.UtcNow.Add(_cacheOptions.CacheExpiration);

            // Check if entry exists
            var existing = await _dbContext.CachedMetadata
                .FirstOrDefaultAsync(e => e.CacheKey == cacheKey, cancellationToken);

            if (existing != null)
            {
                existing.MetadataJson = json;
                existing.ExpiresAt = expiresAt;
                existing.GeneratedByModel = metadata.GeneratedByModel;
                existing.Title = content?.Title ?? existing.Title;
            }
            else
            {
                var entry = new CachedSeoMetadataEntity
                {
                    CacheKey = cacheKey,
                    MetadataJson = json,
                    ContentType = content?.ContentType.ToString() ?? "Unknown",
                    Title = content?.Title,
                    GeneratedByModel = metadata.GeneratedByModel,
                    ExpiresAt = expiresAt
                };
                _dbContext.CachedMetadata.Add(entry);
            }

            await _dbContext.SaveChangesAsync(cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to save metadata to database cache: {CacheKey}", cacheKey);
        }
    }

    #endregion
}