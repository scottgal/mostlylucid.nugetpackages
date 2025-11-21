namespace Mostlylucid.LlmAltText.Data;

/// <summary>
///     Repository for managing cached alt text entries
/// </summary>
public interface IAltTextRepository
{
    /// <summary>
    ///     Get cached alt text for an image source
    /// </summary>
    /// <param name="imageSource">Image URL or path</param>
    /// <returns>Cached entry or null if not found</returns>
    Task<ImageAltTextEntry?> GetBySourceAsync(string imageSource);

    /// <summary>
    ///     Get cached alt text by content hash
    /// </summary>
    /// <param name="contentHash">SHA256 hash of image content</param>
    /// <returns>Cached entry or null if not found</returns>
    Task<ImageAltTextEntry?> GetByContentHashAsync(string contentHash);

    /// <summary>
    ///     Save or update an alt text entry
    /// </summary>
    /// <param name="entry">Entry to save</param>
    Task SaveAsync(ImageAltTextEntry entry);

    /// <summary>
    ///     Increment usage count for an entry
    /// </summary>
    /// <param name="id">Entry ID</param>
    Task IncrementUsageAsync(int id);

    /// <summary>
    ///     Delete old entries that haven't been used
    /// </summary>
    /// <param name="olderThan">Delete entries older than this date</param>
    /// <param name="minUsageCount">Only delete entries with usage below this count</param>
    /// <returns>Number of entries deleted</returns>
    Task<int> CleanupOldEntriesAsync(DateTime olderThan, int minUsageCount = 1);

    /// <summary>
    ///     Get total count of cached entries
    /// </summary>
    Task<int> GetCountAsync();
}