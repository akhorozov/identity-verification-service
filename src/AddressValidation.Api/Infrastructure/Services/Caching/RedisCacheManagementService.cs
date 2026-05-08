using StackExchange.Redis;

namespace AddressValidation.Api.Infrastructure.Services.Caching;

/// <summary>
/// Redis (L1) implementation of <see cref="ICacheManagementService"/>.
/// Provides stats, per-key invalidation, and full database flush via StackExchange.Redis.
/// </summary>
public sealed class RedisCacheManagementService : ICacheManagementService
{
    private readonly IConnectionMultiplexer _connectionMultiplexer;
    private readonly IDatabase _database;
    private readonly ILogger<RedisCacheManagementService> _logger;

    // In-memory counters; reset on process restart.
    private long _hitCount;
    private long _missCount;

    /// <inheritdoc />
    public string LayerName => "L1-Redis";

    /// <summary>
    /// Initializes a new instance of <see cref="RedisCacheManagementService"/>.
    /// </summary>
    public RedisCacheManagementService(
        IConnectionMultiplexer connectionMultiplexer,
        IConfiguration configuration,
        ILogger<RedisCacheManagementService> logger)
    {
        ArgumentNullException.ThrowIfNull(connectionMultiplexer);
        ArgumentNullException.ThrowIfNull(configuration);
        ArgumentNullException.ThrowIfNull(logger);

        _connectionMultiplexer = connectionMultiplexer;
        _logger = logger;

        var databaseIndex = configuration.GetValue<int>("Redis:DefaultDatabase", 0);
        _database = _connectionMultiplexer.GetDatabase(databaseIndex);
    }

    /// <inheritdoc />
    public async Task<CacheLayerStats> GetStatsAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            // Count keys matching the canonical addr: prefix pattern across all endpoints.
            var server = _connectionMultiplexer.GetServers().FirstOrDefault();
            long entryCount = 0;

            if (server is not null)
            {
                // Scan is non-blocking and safe for production Redis.
                await foreach (var _ in server.KeysAsync(pattern: "addr:*"))
                {
                    entryCount++;
                    cancellationToken.ThrowIfCancellationRequested();
                }
            }

            return new CacheLayerStats(
                Layer: LayerName,
                EntryCount: entryCount,
                HitCount: Interlocked.Read(ref _hitCount),
                MissCount: Interlocked.Read(ref _missCount));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving Redis cache stats");
            return new CacheLayerStats(LayerName, -1, 0, 0);
        }
    }

    /// <inheritdoc />
    public async Task<bool> InvalidateAsync(string key, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNullOrEmpty(key);

        try
        {
            var deleted = await _database.KeyDeleteAsync(key);
            _logger.LogInformation("Redis: invalidated key {Key} (existed: {Existed})", key, deleted);
            return deleted;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error invalidating Redis key {Key}", key);
            throw;
        }
    }

    /// <inheritdoc />
    public async Task<long> FlushAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            var server = _connectionMultiplexer.GetServers().FirstOrDefault();
            if (server is null)
            {
                _logger.LogWarning("Redis: no server available for flush");
                return 0;
            }

            // Count before flushing so we can report how many were removed.
            long count = 0;
            var keys = new List<RedisKey>();
            await foreach (var key in server.KeysAsync(pattern: "addr:*"))
            {
                keys.Add(key);
                count++;
                cancellationToken.ThrowIfCancellationRequested();
            }

            if (keys.Count > 0)
            {
                await _database.KeyDeleteAsync([.. keys]);
            }

            _logger.LogInformation("Redis: flushed {Count} cache entries", count);
            return count;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error flushing Redis cache");
            throw;
        }
    }

    /// <summary>
    /// Increments the hit counter (called by cache read paths when a hit occurs).
    /// </summary>
    internal void RecordHit() => Interlocked.Increment(ref _hitCount);

    /// <summary>
    /// Increments the miss counter (called by cache read paths when a miss occurs).
    /// </summary>
    internal void RecordMiss() => Interlocked.Increment(ref _missCount);
}
