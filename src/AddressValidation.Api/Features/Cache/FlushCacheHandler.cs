using AddressValidation.Api.Domain.Events;
using AddressValidation.Api.Infrastructure.Services.Audit;
using AddressValidation.Api.Infrastructure.Services.Caching;

namespace AddressValidation.Api.Features.Cache;

/// <summary>
/// Orchestrates flushing of the L1 (Redis) cache layer.
/// Per SRS FR-003: "flush only affects Redis; CosmosDB (L2) retained."
/// Issue #77.
/// </summary>
public sealed class FlushCacheHandler
{
    private readonly IEnumerable<ICacheManagementService> _layers;
    private readonly IAuditEventStore _auditEventStore;
    private readonly ILogger<FlushCacheHandler> _logger;

    /// <summary>
    /// Initializes a new instance of <see cref="FlushCacheHandler"/>.
    /// </summary>
    public FlushCacheHandler(
        IEnumerable<ICacheManagementService> layers,
        IAuditEventStore auditEventStore,
        ILogger<FlushCacheHandler> logger)
    {
        ArgumentNullException.ThrowIfNull(layers);
        ArgumentNullException.ThrowIfNull(auditEventStore);
        ArgumentNullException.ThrowIfNull(logger);

        _layers = layers;
        _auditEventStore = auditEventStore;
        _logger = logger;
    }

    /// <summary>
    /// Flushes all cache layers that support flushing (L1/Redis).
    /// Cosmos DB (L2) is intentionally skipped per SRS FR-003.
    /// </summary>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Result describing which layers were flushed and how many entries were removed.</returns>
    public async Task<FlushCacheResult> HandleAsync(CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Flushing L1 cache layer(s)");

        long totalRemoved = 0;
        var flushedLayers = new List<string>();

        // Only flush L1; CosmosCacheManagementService.FlushAsync is a no-op by design.
        foreach (var layer in _layers)
        {
            var removed = await layer.FlushAsync(cancellationToken);
            if (removed > 0 || layer.LayerName.StartsWith("L1", StringComparison.Ordinal))
            {
                flushedLayers.Add(layer.LayerName);
                totalRemoved += removed;
            }
        }

        await _auditEventStore.AppendAsync(new CacheFlushed
        {
            AggregateId = "cache-flush",
            CacheLayers = flushedLayers,
            EntriesRemoved = totalRemoved,
        }, cancellationToken);

        _logger.LogInformation("Cache flush complete: {Count} entries removed from {Layers}",
            totalRemoved, string.Join(", ", flushedLayers));

        return new FlushCacheResult(totalRemoved, flushedLayers);
    }
}
