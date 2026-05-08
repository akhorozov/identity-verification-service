using System.Diagnostics;

namespace AddressValidation.Api.Infrastructure.Services.Caching;

/// <summary>
/// Cache source metadata for identifying where a cached value originated.
/// </summary>
public record CacheSourceMetadata(
    string Source,
    DateTimeOffset RetrievedAt,
    long? ElapsedMilliseconds = null)
{
    /// <summary>
    /// Identifies the cache source (L1: Redis, L2: CosmosDB, Provider: External Service).
    /// </summary>
    public string Source { get; } = Source;

    /// <summary>
    /// Timestamp when the value was retrieved.
    /// </summary>
    public DateTimeOffset RetrievedAt { get; } = RetrievedAt;

    /// <summary>
    /// Elapsed time in milliseconds to retrieve the value.
    /// </summary>
    public long? ElapsedMilliseconds { get; } = ElapsedMilliseconds;
}

/// <summary>
/// Delegate for provider lookups when cache misses occur at all levels.
/// </summary>
/// <typeparam name="T">The type of value provided by the external service.</typeparam>
/// <param name="key">The cache key being looked up.</param>
/// <param name="cancellationToken">Cancellation token for the async operation.</param>
/// <returns>The value from the provider, or null if not found.</returns>
public delegate Task<T?> ProviderLookupDelegate<T>(string key, CancellationToken cancellationToken) where T : class;

/// <summary>
/// Result from CacheOrchestrator lookup operations.
/// </summary>
/// <typeparam name="T">The type of cached value.</typeparam>
public record CacheResult<T>(T? Value, CacheSourceMetadata? Source = null, bool IsHit = false)
    where T : class;

/// <summary>
/// Multi-level cache orchestrator implementing L1 → L2 → Provider lookup strategy.
/// Manages cache hierarchy: L1 (Redis) → L2 (CosmosDB) → External Provider
/// On misses, populates both levels with write-through strategy.
/// </summary>
/// <typeparam name="T">The type of value to cache. Must be serializable.</typeparam>
public class CacheOrchestrator<T> where T : class
{
    private readonly ICacheService<T> _l1Cache; // Redis
    private readonly ICacheService<T> _l2Cache; // CosmosDB
    private readonly ILogger<CacheOrchestrator<T>> _logger;
    private readonly TimeSpan? _l1Ttl;
    private readonly TimeSpan? _l2Ttl;

    /// <summary>
    /// Initializes a new instance of the CacheOrchestrator class.
    /// </summary>
    /// <param name="l1Cache">L1 cache service (Redis). Required.</param>
    /// <param name="l2Cache">L2 cache service (CosmosDB). Required.</param>
    /// <param name="logger">Logger instance.</param>
    /// <param name="l1Ttl">Optional TTL override for L1 cache. If null, uses cache default.</param>
    /// <param name="l2Ttl">Optional TTL override for L2 cache. If null, uses cache default.</param>
    /// <exception cref="ArgumentNullException">Thrown when cache services or logger are null.</exception>
    public CacheOrchestrator(
        ICacheService<T> l1Cache,
        ICacheService<T> l2Cache,
        ILogger<CacheOrchestrator<T>> logger,
        TimeSpan? l1Ttl = null,
        TimeSpan? l2Ttl = null)
    {
        ArgumentNullException.ThrowIfNull(l1Cache);
        ArgumentNullException.ThrowIfNull(l2Cache);
        ArgumentNullException.ThrowIfNull(logger);

        _l1Cache = l1Cache;
        _l2Cache = l2Cache;
        _logger = logger;
        _l1Ttl = l1Ttl;
        _l2Ttl = l2Ttl;
    }

    /// <summary>
    /// Performs multi-level cache lookup with fallback to provider.
    /// Strategy: L1 (Redis) → L2 (CosmosDB) → Provider → Write-through both levels
    /// </summary>
    /// <param name="key">The cache key to lookup.</param>
    /// <param name="providerLookup">Delegate to fetch from external provider on cache miss.</param>
    /// <param name="cancellationToken">Cancellation token for the async operation.</param>
    /// <returns>CacheResult containing value (if found) and source metadata.</returns>
    /// <exception cref="ArgumentNullException">Thrown when key or providerLookup is null.</exception>
    public async Task<CacheResult<T>> GetAsync(
        string key,
        ProviderLookupDelegate<T> providerLookup,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNullOrEmpty(key);
        ArgumentNullException.ThrowIfNull(providerLookup);

        var stopwatch = Stopwatch.StartNew();

        try
        {
            // L1: Check Redis
            var l1Result = await _l1Cache.GetAsync(key, cancellationToken);
            if (l1Result != null)
            {
                stopwatch.Stop();
                _logger.LogInformation("L1 cache hit for key: {Key} (elapsed: {ElapsedMs}ms)", key, stopwatch.ElapsedMilliseconds);
                return new CacheResult<T>(
                    l1Result,
                    new CacheSourceMetadata("L1:Redis", DateTimeOffset.UtcNow, stopwatch.ElapsedMilliseconds),
                    true);
            }

            // L2: Check CosmosDB
            var l2Result = await _l2Cache.GetAsync(key, cancellationToken);
            if (l2Result != null)
            {
                stopwatch.Stop();
                _logger.LogInformation("L2 cache hit for key: {Key} (elapsed: {ElapsedMs}ms)", key, stopwatch.ElapsedMilliseconds);

                // Warm L1 from L2
                _ = _l1Cache.SetAsync(key, l2Result, _l1Ttl, cancellationToken)
                    .ContinueWith(
                        t =>
                        {
                            if (t.IsFaulted)
                                _logger.LogWarning(t.Exception, "Failed to warm L1 from L2 for key: {Key}", key);
                            else
                                _logger.LogDebug("L1 warmed from L2 for key: {Key}", key);
                        },
                        cancellationToken);

                return new CacheResult<T>(
                    l2Result,
                    new CacheSourceMetadata("L2:CosmosDB", DateTimeOffset.UtcNow, stopwatch.ElapsedMilliseconds),
                    true);
            }

            // Provider: Fetch from external service
            var providerResult = await providerLookup(key, cancellationToken);
            if (providerResult != null)
            {
                stopwatch.Stop();
                _logger.LogInformation("Provider hit for key: {Key} (elapsed: {ElapsedMs}ms)", key, stopwatch.ElapsedMilliseconds);

                // Write-through: L2 then L1
                await WriteThoughAsync(key, providerResult, cancellationToken);

                return new CacheResult<T>(
                    providerResult,
                    new CacheSourceMetadata("Provider:External", DateTimeOffset.UtcNow, stopwatch.ElapsedMilliseconds),
                    true);
            }

            stopwatch.Stop();
            _logger.LogWarning("Cache miss for key: {Key} (elapsed: {ElapsedMs}ms)", key, stopwatch.ElapsedMilliseconds);
            return new CacheResult<T>(
                null,
                new CacheSourceMetadata("Miss:None", DateTimeOffset.UtcNow, stopwatch.ElapsedMilliseconds),
                false);
        }
        catch (OperationCanceledException)
        {
            _logger.LogWarning("Cache lookup cancelled for key: {Key}", key);
            throw;
        }
        catch (Exception ex)
        {
            stopwatch.Stop();
            _logger.LogError(ex, "Error during cache orchestration lookup for key: {Key}", key);
            throw;
        }
    }

    /// <summary>
    /// Sets a value in the cache hierarchy with write-through strategy (L2 then L1).
    /// </summary>
    /// <param name="key">The cache key to set.</param>
    /// <param name="value">The value to cache.</param>
    /// <param name="l1Ttl">Optional L1 TTL override.</param>
    /// <param name="l2Ttl">Optional L2 TTL override.</param>
    /// <param name="cancellationToken">Cancellation token for the async operation.</param>
    /// <returns>A task representing the async operation.</returns>
    /// <exception cref="ArgumentNullException">Thrown when key or value is null.</exception>
    public async Task SetAsync(
        string key,
        T value,
        TimeSpan? l1Ttl = null,
        TimeSpan? l2Ttl = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNullOrEmpty(key);
        ArgumentNullException.ThrowIfNull(value);

        var stopwatch = Stopwatch.StartNew();

        try
        {
            var effectiveL2Ttl = l2Ttl ?? _l2Ttl;
            var effectiveL1Ttl = l1Ttl ?? _l1Ttl;

            // Write-through: L2 first (persistent layer)
            await _l2Cache.SetAsync(key, value, effectiveL2Ttl, cancellationToken);
            _logger.LogDebug("Value set in L2 cache for key: {Key}", key);

            // Then L1 (fast layer)
            await _l1Cache.SetAsync(key, value, effectiveL1Ttl, cancellationToken);
            _logger.LogDebug("Value set in L1 cache for key: {Key}", key);

            stopwatch.Stop();
            _logger.LogInformation("Write-through cache set for key: {Key} (elapsed: {ElapsedMs}ms)", key, stopwatch.ElapsedMilliseconds);
        }
        catch (Exception ex)
        {
            stopwatch.Stop();
            _logger.LogError(ex, "Error during cache orchestration write-through for key: {Key}", key);
            throw;
        }
    }

    /// <summary>
    /// Removes a value from all cache levels.
    /// </summary>
    /// <param name="key">The cache key to remove.</param>
    /// <param name="cancellationToken">Cancellation token for the async operation.</param>
    /// <returns>A task representing the async operation.</returns>
    /// <exception cref="ArgumentNullException">Thrown when key is null or empty.</exception>
    public async Task RemoveAsync(string key, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNullOrEmpty(key);

        try
        {
            // Remove from both levels in parallel
            await Task.WhenAll(
                _l1Cache.RemoveAsync(key, cancellationToken),
                _l2Cache.RemoveAsync(key, cancellationToken));

            _logger.LogInformation("Cache key removed from all levels: {Key}", key);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error during cache removal for key: {Key}", key);
            throw;
        }
    }

    /// <summary>
    /// Internal helper to implement write-through strategy (L2 then L1).
    /// </summary>
    private async Task WriteThoughAsync(string key, T value, CancellationToken cancellationToken)
    {
        try
        {
            // L2 first (persistent)
            await _l2Cache.SetAsync(key, value, _l2Ttl, cancellationToken);

            // L1 second (fast) - fire and forget, don't fail if L1 write fails
            _ = _l1Cache.SetAsync(key, value, _l1Ttl, cancellationToken)
                .ContinueWith(
                    t =>
                    {
                        if (t.IsFaulted)
                            _logger.LogWarning(t.Exception, "Failed to write-through to L1 for key: {Key}", key);
                        else
                            _logger.LogDebug("Write-through to L1 completed for key: {Key}", key);
                    },
                    cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error during write-through for key: {Key}", key);
            throw;
        }
    }
}
