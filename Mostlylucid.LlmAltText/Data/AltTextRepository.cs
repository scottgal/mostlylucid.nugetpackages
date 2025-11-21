using System.Security.Cryptography;
using System.Text;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace Mostlylucid.LlmAltText.Data;

/// <summary>
///     EF Core implementation of the alt text repository
/// </summary>
public class AltTextRepository : IAltTextRepository
{
    private readonly AltTextDbContext _context;
    private readonly ILogger<AltTextRepository> _logger;

    public AltTextRepository(AltTextDbContext context, ILogger<AltTextRepository> logger)
    {
        _context = context;
        _logger = logger;
    }

    public async Task<ImageAltTextEntry?> GetBySourceAsync(string imageSource)
    {
        var hash = ComputeHash(imageSource);
        return await _context.ImageAltTexts
            .FirstOrDefaultAsync(e => e.SourceHash == hash);
    }

    public async Task<ImageAltTextEntry?> GetByContentHashAsync(string contentHash)
    {
        return await _context.ImageAltTexts
            .FirstOrDefaultAsync(e => e.ContentHash == contentHash);
    }

    public async Task SaveAsync(ImageAltTextEntry entry)
    {
        var existing = await _context.ImageAltTexts
            .FirstOrDefaultAsync(e => e.SourceHash == entry.SourceHash);

        if (existing != null)
        {
            // Update existing entry
            existing.AltText = entry.AltText;
            existing.ExtractedText = entry.ExtractedText;
            existing.ContentType = entry.ContentType;
            existing.ContentTypeConfidence = entry.ContentTypeConfidence;
            existing.ContentHash = entry.ContentHash;
            existing.UpdatedAt = DateTime.UtcNow;
            existing.UsageCount++;
        }
        else
        {
            // Ensure source hash is set
            if (string.IsNullOrEmpty(entry.SourceHash)) entry.SourceHash = ComputeHash(entry.ImageSource);
            await _context.ImageAltTexts.AddAsync(entry);
        }

        await _context.SaveChangesAsync();
        _logger.LogDebug("Saved alt text for {Source}", entry.ImageSource);
    }

    public async Task IncrementUsageAsync(int id)
    {
        var entry = await _context.ImageAltTexts.FindAsync(id);
        if (entry != null)
        {
            entry.UsageCount++;
            entry.UpdatedAt = DateTime.UtcNow;
            await _context.SaveChangesAsync();
        }
    }

    public async Task<int> CleanupOldEntriesAsync(DateTime olderThan, int minUsageCount = 1)
    {
        var entriesToDelete = await _context.ImageAltTexts
            .Where(e => e.CreatedAt < olderThan && e.UsageCount < minUsageCount && !e.IsManuallyReviewed)
            .ToListAsync();

        _context.ImageAltTexts.RemoveRange(entriesToDelete);
        await _context.SaveChangesAsync();

        _logger.LogInformation("Cleaned up {Count} old alt text entries", entriesToDelete.Count);
        return entriesToDelete.Count;
    }

    public async Task<int> GetCountAsync()
    {
        return await _context.ImageAltTexts.CountAsync();
    }

    /// <summary>
    ///     Compute SHA256 hash of a string
    /// </summary>
    public static string ComputeHash(string input)
    {
        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(input));
        return Convert.ToHexString(bytes).ToLowerInvariant();
    }
}