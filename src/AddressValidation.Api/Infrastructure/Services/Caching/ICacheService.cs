namespace AddressValidation.Api.Infrastructure.Services.Caching;

/// <summary>
/// Generic cache service abstraction for distributed caching operations.
/// Supports multi-level caching strategies (L1: Redis, L2: CosmosDB).
/// </summary>
/// <typeparam name="T">The type of value to cache. Must be serializable.</typeparam>
public interface ICacheService<T> where T : class
{
    /// <summary>
    /// Retrieves a value from cache asynchronously.
    /// </summary>
    /// <param name="key">The cache key to retrieve.</param>
    /// <param name="cancellationToken">Cancellation token for the async operation.</param>
    /// <returns>
    /// The cached value if found; otherwise <c>null</c>.
    /// </returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="key"/> is null or empty.</exception>
    Task<T?> GetAsync(string key, CancellationToken cancellationToken = default);

    /// <summary>
    /// Stores a value in cache asynchronously.
    /// </summary>
    /// <param name="key">The cache key to store under.</param>
    /// <param name="value">The value to cache. Must not be null.</param>
    /// <param name="ttl">Time-to-live for the cached value. If null, uses default TTL.</param>
    /// <param name="cancellationToken">Cancellation token for the async operation.</param>
    /// <returns>A task representing the asynchronous operation.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="key"/> or <paramref name="value"/> is null.</exception>
    /// <exception cref="ArgumentException">Thrown when <paramref name="key"/> is empty or TTL is invalid.</exception>
    Task SetAsync(string key, T value, TimeSpan? ttl = null, CancellationToken cancellationToken = default);

    /// <summary>
    /// Removes a value from cache asynchronously.
    /// </summary>
    /// <param name="key">The cache key to remove.</param>
    /// <param name="cancellationToken">Cancellation token for the async operation.</param>
    /// <returns>A task representing the asynchronous operation.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="key"/> is null or empty.</exception>
    Task RemoveAsync(string key, CancellationToken cancellationToken = default);

    /// <summary>
    /// Checks if a key exists in cache asynchronously.
    /// </summary>
    /// <param name="key">The cache key to check.</param>
    /// <param name="cancellationToken">Cancellation token for the async operation.</param>
    /// <returns><c>true</c> if the key exists; otherwise <c>false</c>.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="key"/> is null or empty.</exception>
    Task<bool> ExistsAsync(string key, CancellationToken cancellationToken = default);
}
