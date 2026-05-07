using System.Text.Json;
using StackExchange.Redis;

namespace AddressValidation.Api.Infrastructure.Redis;

/// <summary>
/// Redis cache implementation
/// </summary>
public interface IRedisCache : Infrastructure.Caching.IDistributedCache
{
}

/// <summary>
/// Redis cache implementation using StackExchange.Redis
/// </summary>
public class RedisCache : IRedisCache
{
    private readonly IConnectionMultiplexer _connectionMultiplexer;
    private readonly IDatabase _database;
    private readonly ILogger<RedisCache> _logger;
    private readonly int _defaultDatabase;

    public RedisCache(
        IConnectionMultiplexer connectionMultiplexer,
        IConfiguration configuration,
        ILogger<RedisCache> logger)
    {
        _connectionMultiplexer = connectionMultiplexer ?? throw new ArgumentNullException(nameof(connectionMultiplexer));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _defaultDatabase = configuration.GetValue<int>("Redis:DefaultDatabase", 0);
        _database = _connectionMultiplexer.GetDatabase(_defaultDatabase);
    }

    public async Task<T?> GetAsync<T>(string key, CancellationToken cancellationToken = default)
    {
        try
        {
            var value = await _database.StringGetAsync(key);
            if (value.IsNull)
            {
                return default;
            }

            return JsonSerializer.Deserialize<T>(value.ToString());
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving value from Redis cache for key: {Key}", key);
            return default;
        }
    }

    public async Task SetAsync<T>(
        string key,
        T value,
        TimeSpan? expiration = null,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var json = JsonSerializer.Serialize(value);
            await _database.StringSetAsync(key, json, expiration);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error setting value in Redis cache for key: {Key}", key);
        }
    }

    public async Task RemoveAsync(string key, CancellationToken cancellationToken = default)
    {
        try
        {
            await _database.KeyDeleteAsync(key);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error removing value from Redis cache for key: {Key}", key);
        }
    }

    public async Task<bool> ExistsAsync(string key, CancellationToken cancellationToken = default)
    {
        try
        {
            return await _database.KeyExistsAsync(key);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error checking key existence in Redis cache for key: {Key}", key);
            return false;
        }
    }

    public async Task<IDictionary<string, T?>> GetManyAsync<T>(
        IEnumerable<string> keys,
        CancellationToken cancellationToken = default)
    {
        var result = new Dictionary<string, T?>();

        try
        {
            var redisKeys = keys.Select(k => (RedisKey)k).ToArray();
            var values = await _database.StringGetAsync(redisKeys);

            for (int i = 0; i < redisKeys.Length; i++)
            {
                if (!values[i].IsNull)
                {
                    result[redisKeys[i].ToString()] = JsonSerializer.Deserialize<T>(values[i].ToString());
                }
                else
                {
                    result[redisKeys[i].ToString()] = default;
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving multiple values from Redis cache");
        }

        return result;
    }

    public async Task RemoveManyAsync(IEnumerable<string> keys, CancellationToken cancellationToken = default)
    {
        try
        {
            var redisKeys = keys.Select(k => (RedisKey)k).ToArray();
            await _database.KeyDeleteAsync(redisKeys);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error removing multiple values from Redis cache");
        }
    }
}
