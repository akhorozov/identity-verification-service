using System.Text.Json;
using Microsoft.Azure.Cosmos;
using Microsoft.Azure.Cosmos.Linq;

namespace AddressValidation.Api.Infrastructure.Services.Caching;

/// <summary>
/// Azure Cosmos DB-based L2 cache service implementation.
/// Provides persistent, distributed caching with TTL management and optimized indexing.
/// </summary>
/// <typeparam name="T">The type of value to cache. Must be serializable to JSON.</typeparam>
public class CosmosCacheService<T> : ICacheService<T> where T : class
{
    private readonly Container _container;
    private readonly ILogger<CosmosCacheService<T>> _logger;
    private readonly JsonSerializerOptions _jsonOptions;
    private readonly TimeSpan _defaultTtl;
    private readonly string _partitionKeyPath;

    /// <summary>
    /// Cosmos DB cache item internal model for storage.
    /// </summary>
    private sealed class CacheItem
    {
        [System.Text.Json.Serialization.JsonPropertyName("id")]
        public string Id { get; set; } = string.Empty;

        [System.Text.Json.Serialization.JsonPropertyName("pk")]
        public string PartitionKey { get; set; } = string.Empty;

        [System.Text.Json.Serialization.JsonPropertyName("value")]
        public string Value { get; set; } = string.Empty;

        [System.Text.Json.Serialization.JsonPropertyName("type")]
        public string Type { get; set; } = string.Empty;

        [System.Text.Json.Serialization.JsonPropertyName("_ts")]
        public long Timestamp { get; set; }

        [System.Text.Json.Serialization.JsonPropertyName("ttl")]
        public int? Ttl { get; set; }
    }

    /// <summary>
    /// Initializes a new instance of the CosmosCacheService class.
    /// </summary>
    /// <param name="cosmosClient">Azure Cosmos DB client.</param>
    /// <param name="configuration">Application configuration for Cosmos DB settings.</param>
    /// <param name="logger">Logger instance.</param>
    /// <exception cref="ArgumentNullException">Thrown when any required parameter is null.</exception>
    /// <exception cref="InvalidOperationException">Thrown when required configuration is missing.</exception>
    public CosmosCacheService(
        CosmosClient cosmosClient,
        IConfiguration configuration,
        ILogger<CosmosCacheService<T>> logger)
    {
        ArgumentNullException.ThrowIfNull(cosmosClient);
        ArgumentNullException.ThrowIfNull(configuration);
        ArgumentNullException.ThrowIfNull(logger);

        _logger = logger;

        var databaseId = configuration["Cosmos:DatabaseId"] 
            ?? throw new InvalidOperationException("Cosmos:DatabaseId configuration is required");
        var containerId = configuration["Cosmos:CacheContainerId"] 
            ?? throw new InvalidOperationException("Cosmos:CacheContainerId configuration is required");

        var database = cosmosClient.GetDatabase(databaseId);
        _container = database.GetContainer(containerId);

        var defaultTtlSeconds = configuration.GetValue<int>("Cosmos:DefaultTtlSeconds", 86400);
        _defaultTtl = TimeSpan.FromSeconds(defaultTtlSeconds);

        _partitionKeyPath = configuration["Cosmos:PartitionKeyPath"] ?? "/pk";

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
            var query = _container.GetItemLinqQueryable<CacheItem>(requestOptions: new QueryRequestOptions { MaxItemCount = 1 })
                .Where(item => item.Id == key)
                .Take(1);

            using var iterator = query.ToFeedIterator();
            if (!iterator.HasMoreResults)
            {
                _logger.LogDebug("Cache miss for key: {Key} in Cosmos DB", key);
                return default;
            }

            var batch = await iterator.ReadNextAsync(cancellationToken);
            var cacheItem = batch.FirstOrDefault();

            if (cacheItem == null)
            {
                _logger.LogDebug("Cache miss for key: {Key} in Cosmos DB", key);
                return default;
            }

            var deserialized = JsonSerializer.Deserialize<T>(cacheItem.Value, _jsonOptions);
            _logger.LogDebug("Cache hit for key: {Key} in Cosmos DB", key);
            return deserialized;
        }
        catch (CosmosException ex) when (ex.StatusCode == System.Net.HttpStatusCode.NotFound)
        {
            _logger.LogDebug("Cache miss for key: {Key} in Cosmos DB", key);
            return default;
        }
        catch (JsonException ex)
        {
            _logger.LogWarning(ex, "Failed to deserialize cached value for key: {Key} in Cosmos DB", key);
            await RemoveAsync(key, cancellationToken);
            return default;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving value from Cosmos DB cache for key: {Key}", key);
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
            var effectiveTtl = ttl ?? _defaultTtl;
            var ttlSeconds = (int)effectiveTtl.TotalSeconds;

            var cacheItem = new CacheItem
            {
                Id = key,
                PartitionKey = ExtractPartitionKey(key),
                Value = json,
                Type = typeof(T).Name,
                Timestamp = DateTimeOffset.UtcNow.ToUnixTimeSeconds(),
                Ttl = ttlSeconds,
            };

            await _container.UpsertItemAsync(cacheItem, new PartitionKey(cacheItem.PartitionKey), cancellationToken: cancellationToken);
            _logger.LogDebug("Cache set for key: {Key} in Cosmos DB with TTL: {TtlSeconds}s", key, ttlSeconds);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error setting value in Cosmos DB cache for key: {Key}", key);
            throw;
        }
    }

    /// <inheritdoc />
    public async Task RemoveAsync(string key, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNullOrEmpty(key);

        try
        {
            var partitionKey = ExtractPartitionKey(key);
            await _container.DeleteItemAsync<CacheItem>(key, new PartitionKey(partitionKey), cancellationToken: cancellationToken);
            _logger.LogDebug("Cache key removed: {Key} from Cosmos DB", key);
        }
        catch (CosmosException ex) when (ex.StatusCode == System.Net.HttpStatusCode.NotFound)
        {
            _logger.LogDebug("Cache key not found: {Key} in Cosmos DB", key);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error removing value from Cosmos DB cache for key: {Key}", key);
            throw;
        }
    }

    /// <inheritdoc />
    public async Task<bool> ExistsAsync(string key, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNullOrEmpty(key);

        try
        {
            var partitionKey = ExtractPartitionKey(key);
            var response = await _container.ReadItemAsync<CacheItem>(key, new PartitionKey(partitionKey), cancellationToken: cancellationToken);
            _logger.LogDebug("Cache key exists in Cosmos DB: {Key}", key);
            return true;
        }
        catch (CosmosException ex) when (ex.StatusCode == System.Net.HttpStatusCode.NotFound)
        {
            _logger.LogDebug("Cache key does not exist in Cosmos DB: {Key}", key);
            return false;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error checking key existence in Cosmos DB cache for key: {Key}", key);
            return false;
        }
    }

    /// <summary>
    /// Extracts the partition key from the cache key.
    /// By default, uses the first segment before the first colon, or the full key if no colon.
    /// </summary>
    /// <param name="key">The cache key.</param>
    /// <returns>The partition key value.</returns>
    private static string ExtractPartitionKey(string key)
    {
        // Format: "addr:v{version}:{sha256}" → partition key "addr"
        var colonIndex = key.IndexOf(':');
        return colonIndex > 0 ? key[..colonIndex] : key;
    }
}
