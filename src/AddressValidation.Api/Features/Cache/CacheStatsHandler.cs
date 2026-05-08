using AddressValidation.Api.Infrastructure.Services.Caching;

namespace AddressValidation.Api.Features.Cache;

/// <summary>
/// Aggregates cache statistics from all registered cache management layers.
/// SRS Ref: FR-003, Issue #73.
/// </summary>
public sealed class CacheStatsHandler
{
    private readonly IEnumerable<ICacheManagementService> _layers;
    private readonly ILogger<CacheStatsHandler> _logger;

    /// <summary>
    /// Initializes a new instance of <see cref="CacheStatsHandler"/>.
    /// </summary>
    public CacheStatsHandler(
        IEnumerable<ICacheManagementService> layers,
        ILogger<CacheStatsHandler> logger)
    {
        ArgumentNullException.ThrowIfNull(layers);
        ArgumentNullException.ThrowIfNull(logger);

        _layers = layers;
        _logger = logger;
    }

    /// <summary>
    /// Aggregates and returns statistics from all cache layers.
    /// </summary>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Combined stats across all layers.</returns>
    public async Task<CacheStatsResponse> HandleAsync(CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Retrieving cache statistics from {LayerCount} layer(s)", _layers.Count());

        var tasks = _layers.Select(layer => layer.GetStatsAsync(cancellationToken));
        var layerStats = await Task.WhenAll(tasks);

        var layerResponses = layerStats
            .Select(s =>
            {
                var total = s.HitCount + s.MissCount;
                var hitRatio = total > 0 ? Math.Round((double)s.HitCount / total, 4) : 0.0;
                return new LayerStatsResponse(s.Layer, s.EntryCount, s.HitCount, s.MissCount, hitRatio);
            })
            .ToList();

        return new CacheStatsResponse(DateTimeOffset.UtcNow, layerResponses);
    }
}
