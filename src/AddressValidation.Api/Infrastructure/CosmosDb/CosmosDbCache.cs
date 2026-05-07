using System.Text.Json;
using Microsoft.Azure.Cosmos;

namespace AddressValidation.Api.Infrastructure.CosmosDb;

/// <summary>
/// Cosmos DB cache implementation
/// </summary>
public interface ICosmosDbCache : Infrastructure.Caching.IDistributedCache
{
}

/// <summary>
/// Cosmos DB cache implementation
/// </summary>
public class CosmosDbCache : ICosmosDbCache
{
    private readonly CosmosClient _client;
    private readonly Container _container;
    private readonly ILogger<CosmosDbCache> _logger;

    public CosmosDbCache(
        IConfiguration configuration,
        ILogger<CosmosDbCache> logger)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));

        var endpoint = configuration.GetValue<string>("CosmosDb:Endpoint") ??
            throw new InvalidOperationException("CosmosDb:Endpoint must be configured");
        var key = configuration.GetValue<string>("CosmosDb:Key") ??
            throw new InvalidOperationException("CosmosDb:Key must be configured");
        var databaseId = configuration.GetValue<string>("CosmosDb:DatabaseId") ?? "address-validation-cache";
        var containerId = configuration.GetValue<string>("CosmosDb:ContainerId") ?? "cache";

        _client = new CosmosClient(endpoint, key);
        _container = _client.GetContainer(databaseId, containerId);
    }

    public async Task<T?> GetAsync<T>(string key, CancellationToken cancellationToken = default)
    {
        try
        {
            var response = await _container.ReadItemAsync<CacheItem>(
                key,
                new PartitionKey(GetPartitionKey(key)),
                cancellationToken: cancellationToken);

            if (response.Resource?.ExpiresAt < DateTime.UtcNow)
            {
                await RemoveAsync(key, cancellationToken);
                return default;
            }

            return JsonSerializer.Deserialize<T>(response.Resource?.Value ?? "null");
        }
        catch (CosmosException ex) when (ex.StatusCode == System.Net.HttpStatusCode.NotFound)
        {
            return default;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving value from Cosmos DB cache for key: {Key}", key);
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
            var cacheItem = new CacheItem
            {
                id = key,
                partitionKey = GetPartitionKey(key),
                Value = JsonSerializer.Serialize(value),
                CreatedAt = DateTime.UtcNow,
                ExpiresAt = DateTime.UtcNow.Add(expiration ?? TimeSpan.FromHours(1))
            };

            await _container.UpsertItemAsync(cacheItem, cancellationToken: cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error setting value in Cosmos DB cache for key: {Key}", key);
        }
    }

    public async Task RemoveAsync(string key, CancellationToken cancellationToken = default)
    {
        try
        {
            await _container.DeleteItemAsync<CacheItem>(
                key,
                new PartitionKey(GetPartitionKey(key)),
                cancellationToken: cancellationToken);
        }
        catch (CosmosException ex) when (ex.StatusCode == System.Net.HttpStatusCode.NotFound)
        {
            // Key doesn't exist, which is fine
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error removing value from Cosmos DB cache for key: {Key}", key);
        }
    }

    public async Task<bool> ExistsAsync(string key, CancellationToken cancellationToken = default)
    {
        try
        {
            var response = await _container.ReadItemAsync<CacheItem>(
                key,
                new PartitionKey(GetPartitionKey(key)),
                cancellationToken: cancellationToken);

            if (response.Resource?.ExpiresAt < DateTime.UtcNow)
            {
                await RemoveAsync(key, cancellationToken);
                return false;
            }

            return true;
        }
        catch (CosmosException ex) when (ex.StatusCode == System.Net.HttpStatusCode.NotFound)
        {
            return false;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error checking key existence in Cosmos DB cache for key: {Key}", key);
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
            var tasks = keys.Select(key => GetAsync<T>(key, cancellationToken));
            var values = await Task.WhenAll(tasks);

            int i = 0;
            foreach (var key in keys)
            {
                result[key] = values[i];
                i++;
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving multiple values from Cosmos DB cache");
        }

        return result;
    }

    public async Task RemoveManyAsync(IEnumerable<string> keys, CancellationToken cancellationToken = default)
    {
        try
        {
            var tasks = keys.Select(key => RemoveAsync(key, cancellationToken));
            await Task.WhenAll(tasks);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error removing multiple values from Cosmos DB cache");
        }
    }

    private string GetPartitionKey(string key)
    {
        // Use first character as partition key for distribution
        return key.Length > 0 ? key[0].ToString() : "default";
    }

    private class CacheItem
    {
        public string id { get; set; } = string.Empty;
        public string partitionKey { get; set; } = string.Empty;
        public string Value { get; set; } = string.Empty;
        public DateTime CreatedAt { get; set; }
        public DateTime ExpiresAt { get; set; }
    }
}
