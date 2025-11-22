using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Mostlylucid.GeoDetection.Data;
using Mostlylucid.GeoDetection.Models;

namespace Mostlylucid.GeoDetection.Services;

/// <summary>
///     Wrapper service that adds caching (memory and optionally database) to any geo provider
/// </summary>
public class CachedGeoLocationService(
    IGeoLocationService innerService,
    IMemoryCache memoryCache,
    IOptions<GeoLite2Options> options,
    IOptions<GeoCacheOptions> cacheOptions,
    ILogger<CachedGeoLocationService> logger,
    GeoDbContext? dbContext = null) : IGeoLocationService
{
    private readonly GeoCacheOptions _cacheOptions = cacheOptions.Value;
    private readonly GeoLite2Options _options = options.Value;
    private readonly GeoLocationStatistics _stats = new();

    public async Task<GeoLocation?> GetLocationAsync(string ipAddress, CancellationToken cancellationToken = default)
    {
        _stats.TotalLookups++;

        // 1. Check memory cache first (fastest)
        var cacheKey = $"geo:{ipAddress}";
        if (memoryCache.TryGetValue(cacheKey, out GeoLocation? cached))
        {
            _stats.CacheHits++;
            return cached;
        }

        // 2. Check database cache if enabled
        if (_cacheOptions.Enabled && dbContext != null)
        {
            var dbCached = await GetFromDatabaseCacheAsync(ipAddress, cancellationToken);
            if (dbCached != null)
            {
                _stats.CacheHits++;
                // Also cache in memory for faster subsequent access
                CacheInMemory(cacheKey, dbCached);
                return dbCached;
            }
        }

        // 3. Look up from provider
        var location = await innerService.GetLocationAsync(ipAddress, cancellationToken);

        if (location != null)
        {
            // Cache in memory
            CacheInMemory(cacheKey, location);

            // Cache in database if enabled
            if (_cacheOptions.Enabled && dbContext != null)
            {
                await SaveToDatabaseCacheAsync(ipAddress, location, cancellationToken);
            }
        }

        return location;
    }

    private void CacheInMemory(string cacheKey, GeoLocation location)
    {
        var memoryCacheOptions = new MemoryCacheEntryOptions()
            .SetAbsoluteExpiration(_options.CacheDuration)
            .SetSize(1);
        memoryCache.Set(cacheKey, location, memoryCacheOptions);
    }

    private async Task<GeoLocation?> GetFromDatabaseCacheAsync(string ipAddress, CancellationToken cancellationToken)
    {
        if (dbContext == null) return null;

        try
        {
            var cached = await dbContext.CachedLocations
                .AsNoTracking()
                .FirstOrDefaultAsync(c => c.IpAddress == ipAddress && c.ExpiresAt > DateTime.UtcNow, cancellationToken);

            if (cached == null) return null;

            return new GeoLocation
            {
                CountryCode = cached.CountryCode,
                CountryName = cached.CountryName,
                ContinentCode = cached.ContinentCode,
                RegionCode = cached.RegionCode,
                City = cached.City,
                Latitude = cached.Latitude,
                Longitude = cached.Longitude,
                TimeZone = cached.TimeZone,
                IsVpn = cached.IsVpn,
                IsHosting = cached.IsHosting
            };
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Failed to read from geo cache database");
            return null;
        }
    }

    private async Task SaveToDatabaseCacheAsync(string ipAddress, GeoLocation location, CancellationToken cancellationToken)
    {
        if (dbContext == null) return;

        try
        {
            var now = DateTime.UtcNow;
            var cached = new CachedGeoLocation
            {
                IpAddress = ipAddress,
                CountryCode = location.CountryCode,
                CountryName = location.CountryName,
                ContinentCode = location.ContinentCode,
                RegionCode = location.RegionCode,
                City = location.City,
                Latitude = location.Latitude,
                Longitude = location.Longitude,
                TimeZone = location.TimeZone,
                IsVpn = location.IsVpn,
                IsHosting = location.IsHosting,
                CachedAt = now,
                ExpiresAt = now.Add(_cacheOptions.CacheExpiration),
                Provider = _options.Provider.ToString()
            };

            var existing = await dbContext.CachedLocations.FindAsync(new object[] { ipAddress }, cancellationToken);
            if (existing != null)
            {
                dbContext.Entry(existing).CurrentValues.SetValues(cached);
            }
            else
            {
                dbContext.CachedLocations.Add(cached);
            }

            await dbContext.SaveChangesAsync(cancellationToken);
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Failed to save to geo cache database");
        }
    }

    public async Task<bool> IsFromCountryAsync(string ipAddress, string countryCode,
        CancellationToken cancellationToken = default)
    {
        var location = await GetLocationAsync(ipAddress, cancellationToken);
        return location?.CountryCode.Equals(countryCode, StringComparison.OrdinalIgnoreCase) ?? false;
    }

    public GeoLocationStatistics GetStatistics()
    {
        var innerStats = innerService.GetStatistics();
        return new GeoLocationStatistics
        {
            TotalLookups = _stats.TotalLookups,
            CacheHits = _stats.CacheHits,
            CachedEntries = innerStats.CachedEntries,
            DatabaseLoaded = innerStats.DatabaseLoaded,
            DatabasePath = innerStats.DatabasePath,
            LastDatabaseUpdate = innerStats.LastDatabaseUpdate
        };
    }

    /// <summary>
    ///     Clean up expired cache entries
    /// </summary>
    public async Task CleanupExpiredAsync(CancellationToken cancellationToken = default)
    {
        if (dbContext == null) return;

        try
        {
            var now = DateTime.UtcNow;
            var expired = await dbContext.CachedLocations
                .Where(c => c.ExpiresAt < now)
                .ToListAsync(cancellationToken);

            if (expired.Count > 0)
            {
                dbContext.CachedLocations.RemoveRange(expired);
                await dbContext.SaveChangesAsync(cancellationToken);
                logger.LogInformation("Cleaned up {Count} expired geo cache entries", expired.Count);
            }
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Failed to cleanup expired geo cache entries");
        }
    }
}
