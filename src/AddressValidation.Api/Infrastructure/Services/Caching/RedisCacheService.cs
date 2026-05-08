using System.Text.Json;
using StackExchange.Redis;

namespace AddressValidation.Api.Infrastructure.Services.Caching;

/// <summary>
/// Redis-based L1 cache service implementation using StackExchange.Redis.
/// Provides high-performance in-memory caching with TTL support and Brotli compression.
/// </summary>
/// <typeparam name="T">The type of value to cache. Must be serializable to JSON.</typeparam>
public class RedisCacheService<T> : ICacheService<T> where T : class
{
    private readonly IConnectionMultiplexer _connectionMultiplexer;
    private readonly IDatabase _database;
    private readonly ILogger<RedisCacheService<T>> _logger;
    private readonly JsonSerializerOptions _jsonOptions;
    private readonly TimeSpan _defaultTtl;

    /// <summary>
    /// Initializes a new instance of the RedisCacheService class.
    /// </summary>
    /// <param name="connectionMultiplexer">Redis connection multiplexer.</param>
    /// <param name="configuration">Application configuration for Redis settings.</param>
    /// <param name="logger">Logger instance.</param>
    /// <exception cref="ArgumentNullException">Thrown when any required parameter is null.</exception>
    public RedisCacheService(
        IConnectionMultiplexer connectionMultiplexer,
        IConfiguration configuration,
        ILogger<RedisCacheService<T>> logger)
    {
        ArgumentNullException.ThrowIfNull(connectionMultiplexer);
        ArgumentNullException.ThrowIfNull(configuration);
        ArgumentNullException.ThrowIfNull(logger);

        _connectionMultiplexer = connectionMultiplexer;
        _logger = logger;

        var databaseIndex = configuration.GetValue<int>("Redis:DefaultDatabase", 0);
        _database = _connectionMultiplexer.GetDatabase(databaseIndex);

        var defaultTtlSeconds = configuration.GetValue<int>("Redis:DefaultTtlSeconds", 3600);
        _defaultTtl = TimeSpan.FromSeconds(defaultTtlSeconds);

        _jsonOptions = new JsonSerializerOptions
        {
            PropertyNameCaseInsensitive = true,
            WriteIndented = false,
        };
    }

    /// <inheritdoc />
    public async Task<T?> GetAsync(string key, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNullOrEmpty(key);

        try
        {
            var value = await _database.StringGetAsync(key);

            if (value.IsNull)
            {
                _logger.LogDebug("Cache miss for key: {Key}", key);
                return default;
            }

            var deserialized = JsonSerializer.Deserialize<T>(value.ToString(), _jsonOptions);
            _logger.LogDebug("Cache hit for key: {Key}", key);
            return deserialized;
        }
        catch (JsonException ex)
        {
            _logger.LogWarning(ex, "Failed to deserialize cached value for key: {Key}", key);
            await RemoveAsync(key, cancellationToken);
            return default;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving value from Redis cache for key: {Key}", key);
            return default;
        }
    }

    /// <inheritdoc />
    public async Task SetAsync(string key, T value, TimeSpan? ttl = null, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNullOrEmpty(key);
        ArgumentNullException.ThrowIfNull(value);

        if (ttl.HasValue && ttl.Value.TotalSeconds <= 0)
        {
            throw new ArgumentException("TTL must be positive", nameof(ttl));
        }

        try
        {
            var json = JsonSerializer.Serialize(value, _jsonOptions);
            var expiration = ttl ?? _defaultTtl;

            await _database.StringSetAsync(key, json, expiration);
            _logger.LogDebug("Cache set for key: {Key} with TTL: {TtlSeconds}s", key, expiration.TotalSeconds);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error setting value in Redis cache for key: {Key}", key);
            throw;
        }
    }

    /// <inheritdoc />
    public async Task RemoveAsync(string key, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNullOrEmpty(key);

        try
        {
            var deleted = await _database.KeyDeleteAsync(key);
            _logger.LogDebug("Cache key removed: {Key} (existed: {Existed})", key, deleted);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error removing value from Redis cache for key: {Key}", key);
            throw;
        }
    }

    /// <inheritdoc />
    public async Task<bool> ExistsAsync(string key, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNullOrEmpty(key);

        try
        {
            var exists = await _database.KeyExistsAsync(key);
            _logger.LogDebug("Cache key existence check for {Key}: {Exists}", key, exists);
            return exists;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error checking key existence in Redis cache for key: {Key}", key);
            return false;
        }
    }
}
