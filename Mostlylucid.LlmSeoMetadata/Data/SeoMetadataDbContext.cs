using Microsoft.EntityFrameworkCore;

namespace Mostlylucid.LlmSeoMetadata.Data;

/// <summary>
///     Entity Framework context for SEO metadata caching
/// </summary>
public class SeoMetadataDbContext : DbContext
{
    public SeoMetadataDbContext(DbContextOptions<SeoMetadataDbContext> options) : base(options)
    {
    }

    /// <summary>
    ///     Cached SEO metadata entries
    /// </summary>
    public DbSet<CachedSeoMetadataEntity> CachedMetadata { get; set; } = null!;

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<CachedSeoMetadataEntity>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.HasIndex(e => e.CacheKey).IsUnique();
            entity.HasIndex(e => e.ExpiresAt);
            entity.Property(e => e.MetadataJson).HasColumnType("TEXT");
        });
    }
}

/// <summary>
///     Database entity for cached SEO metadata
/// </summary>
public class CachedSeoMetadataEntity
{
    /// <summary>
    ///     Primary key
    /// </summary>
    public int Id { get; set; }

    /// <summary>
    ///     Cache key (hash of content)
    /// </summary>
    public required string CacheKey { get; set; }

    /// <summary>
    ///     Serialized metadata JSON
    /// </summary>
    public required string MetadataJson { get; set; }

    /// <summary>
    ///     Content type used for generation
    /// </summary>
    public required string ContentType { get; set; }

    /// <summary>
    ///     Original content title
    /// </summary>
    public string? Title { get; set; }

    /// <summary>
    ///     Model used for generation
    /// </summary>
    public string? GeneratedByModel { get; set; }

    /// <summary>
    ///     When the entry was created
    /// </summary>
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    /// <summary>
    ///     When the entry expires
    /// </summary>
    public DateTime ExpiresAt { get; set; }

    /// <summary>
    ///     Number of times this entry was accessed
    /// </summary>
    public int AccessCount { get; set; }

    /// <summary>
    ///     Last time this entry was accessed
    /// </summary>
    public DateTime LastAccessedAt { get; set; } = DateTime.UtcNow;
}