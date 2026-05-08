using AddressValidation.Api.Domain.Events;
using AddressValidation.Api.Infrastructure.Services.Audit;
using AddressValidation.Api.Infrastructure.Services.Caching;

namespace AddressValidation.Api.Features.Cache;

/// <summary>
/// Orchestrates invalidation of a single cache key across all cache layers.
/// SRS Ref: FR-003 ("removes from Redis, marks stale in CosmosDB"). Issue #76.
/// </summary>
public sealed class InvalidateCacheHandler
{
    private readonly IEnumerable<ICacheManagementService> _layers;
    private readonly IAuditEventStore _auditEventStore;
    private readonly ILogger<InvalidateCacheHandler> _logger;

    /// <summary>
    /// Initializes a new instance of <see cref="InvalidateCacheHandler"/>.
    /// </summary>
    public InvalidateCacheHandler(
        IEnumerable<ICacheManagementService> layers,
        IAuditEventStore auditEventStore,
        ILogger<InvalidateCacheHandler> logger)
    {
        ArgumentNullException.ThrowIfNull(layers);
        ArgumentNullException.ThrowIfNull(auditEventStore);
        ArgumentNullException.ThrowIfNull(logger);

        _layers = layers;
        _auditEventStore = auditEventStore;
        _logger = logger;
    }

    /// <summary>
    /// Invalidates the specified key across all cache layers.
    /// </summary>
    /// <param name="key">The cache key to invalidate.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>
    /// Result containing whether the key was found and which layers were invalidated.
    /// </returns>
    public async Task<InvalidateCacheResult> HandleAsync(string key, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNullOrEmpty(key);

        _logger.LogInformation("Invalidating cache key {Key}", key);

        var invalidatedLayers = new List<string>();
        var anyFound = false;

        foreach (var layer in _layers)
        {
            var found = await layer.InvalidateAsync(key, cancellationToken);
            if (found)
            {
                invalidatedLayers.Add(layer.LayerName);
                anyFound = true;
            }
        }

        if (anyFound)
        {
            await _auditEventStore.AppendAsync(new CacheEntryInvalidated
            {
                AggregateId = key,
                CacheKey = key,
                CacheLayers = invalidatedLayers,
            }, cancellationToken);

            _logger.LogInformation("Cache key {Key} invalidated from layers: {Layers}", key, string.Join(", ", invalidatedLayers));
        }
        else
        {
            _logger.LogInformation("Cache key {Key} not found in any layer", key);
        }

        return new InvalidateCacheResult(anyFound, key, invalidatedLayers);
    }
}
