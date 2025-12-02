using Mostlylucid.ArchiveOrg.Models;

namespace Mostlylucid.ArchiveOrg.Services;

public interface ICdxApiClient
{
    /// <summary>
    ///     Get all CDX records for a URL pattern from Archive.org
    /// </summary>
    /// <param name="url">Base URL to search (supports wildcards with *)</param>
    /// <param name="startDate">Optional start date filter</param>
    /// <param name="endDate">Optional end date filter</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>List of CDX records</returns>
    Task<List<CdxRecord>> GetCdxRecordsAsync(
        string url,
        DateTime? startDate = null,
        DateTime? endDate = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    ///     Get all CDX records for multiple URL patterns from Archive.org
    /// </summary>
    /// <param name="urls">Base URLs to search (supports wildcards with *)</param>
    /// <param name="startDate">Optional start date filter</param>
    /// <param name="endDate">Optional end date filter</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Combined list of CDX records (deduplicated by digest)</returns>
    Task<List<CdxRecord>> GetCdxRecordsAsync(
        IEnumerable<string> urls,
        DateTime? startDate = null,
        DateTime? endDate = null,
        CancellationToken cancellationToken = default);
}