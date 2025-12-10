using Microsoft.Extensions.Logging;
using Mostlylucid.Ephemeral;
using Mostlylucid.Ephemeral.Atoms.SlidingCache;
using Mostlylucid.BotDetection.Orchestration.Signals;

namespace Mostlylucid.BotDetection.Orchestration;

/// <summary>
///     Cache of signature response coordinators (LRU).
///     Each coordinator is TIGHT with its own sink.
/// </summary>
public sealed class SignatureResponseCoordinatorCache : IAsyncDisposable
{
    private readonly SlidingCacheAtom<string, SignatureResponseCoordinator> _cache;
    private readonly ILogger<SignatureResponseCoordinatorCache> _logger;

    public SignatureResponseCoordinatorCache(
        ILogger<SignatureResponseCoordinatorCache> logger,
        int maxSignatures = 5000,
        TimeSpan? ttl = null)
    {
        _logger = logger;

        _cache = new SlidingCacheAtom<string, SignatureResponseCoordinator>(
            factory: async (signature, ct) =>
            {
                _logger.LogDebug("Creating SignatureResponseCoordinator for {Signature}", signature);

                // Create coordinator with its own sink (TIGHT coupling)
                return new SignatureResponseCoordinator(signature, logger);
            },
            slidingExpiration: ttl ?? TimeSpan.FromMinutes(30),
            absoluteExpiration: (ttl ?? TimeSpan.FromMinutes(30)) * 2,
            maxSize: maxSignatures,
            maxConcurrency: Environment.ProcessorCount,
            sampleRate: 10,
            signals: null); // No external signals
    }

    public async Task<SignatureResponseCoordinator> GetOrCreateAsync(
        string signature,
        CancellationToken cancellationToken = default)
    {
        return await _cache.GetOrComputeAsync(signature, cancellationToken);
    }

    public async ValueTask DisposeAsync()
    {
        await _cache.DisposeAsync();
        _logger.LogInformation("SignatureResponseCoordinatorCache disposed");
    }
}
