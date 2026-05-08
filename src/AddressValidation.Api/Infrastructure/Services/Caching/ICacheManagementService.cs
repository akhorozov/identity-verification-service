namespace AddressValidation.Api.Infrastructure.Services.Caching;

/// <summary>
/// Aggregated cache statistics for a single cache layer.
/// </summary>
/// <param name="Layer">Human-readable layer name (e.g., "L1-Redis" or "L2-CosmosDB").</param>
/// <param name="EntryCount">Current number of tracked entries.</param>
/// <param name="HitCount">Cumulative hit counter since last reset or service start.</param>
/// <param name="MissCount">Cumulative miss counter since last reset or service start.</param>
public sealed record CacheLayerStats(string Layer, long EntryCount, long HitCount, long MissCount);

/// <summary>
/// Management-plane operations for the multi-level cache.
/// Provides stats aggregation, per-key invalidation, and full flush capabilities.
/// </summary>
public interface ICacheManagementService
{
    /// <summary>
    /// Returns statistics from this cache layer.
    /// </summary>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Layer statistics snapshot.</returns>
    Task<CacheLayerStats> GetStatsAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Removes a single entry from this cache layer.
    /// </summary>
    /// <param name="key">The cache key to remove.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns><c>true</c> if the key existed and was removed; <c>false</c> if not found.</returns>
    Task<bool> InvalidateAsync(string key, CancellationToken cancellationToken = default);

    /// <summary>
    /// Flushes all entries from this cache layer.
    /// </summary>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Approximate number of entries removed.</returns>
    Task<long> FlushAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Human-readable name of this layer (e.g., "L1-Redis" or "L2-CosmosDB").
    /// </summary>
    string LayerName { get; }
}
