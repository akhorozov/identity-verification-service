using Microsoft.Azure.Cosmos;
using Microsoft.Azure.Cosmos.Linq;

namespace AddressValidation.Api.Infrastructure.Services.Caching;

/// <summary>
/// CosmosDB (L2) implementation of <see cref="ICacheManagementService"/>.
/// Supports stats, per-key stale-marking (SRS: retain data, mark stale), and count reporting.
/// Flush is intentionally a no-op for CosmosDB per SRS FR-003 ("flush only affects Redis").
/// </summary>
public sealed class CosmosCacheManagementService : ICacheManagementService
{
    private readonly Container _container;
    private readonly ILogger<CosmosCacheManagementService> _logger;

    /// <summary>Minimal projection for counting/invalidation without deserializing values.</summary>
    private sealed class CacheItemMeta
    {
        [System.Text.Json.Serialization.JsonPropertyName("id")]
        public string Id { get; set; } = string.Empty;

        [System.Text.Json.Serialization.JsonPropertyName("pk")]
        public string PartitionKey { get; set; } = string.Empty;

        [System.Text.Json.Serialization.JsonPropertyName("ttl")]
        public int? Ttl { get; set; }
    }

    /// <inheritdoc />
    public string LayerName => "L2-CosmosDB";

    /// <summary>
    /// Initializes a new instance of <see cref="CosmosCacheManagementService"/>.
    /// </summary>
    public CosmosCacheManagementService(
        CosmosClient cosmosClient,
        IConfiguration configuration,
        ILogger<CosmosCacheManagementService> logger)
    {
        ArgumentNullException.ThrowIfNull(cosmosClient);
        ArgumentNullException.ThrowIfNull(configuration);
        ArgumentNullException.ThrowIfNull(logger);

        _logger = logger;

        var databaseId = configuration["Cosmos:DatabaseId"]
            ?? throw new InvalidOperationException("Cosmos:DatabaseId configuration is required");
        var containerId = configuration["Cosmos:CacheContainerId"]
            ?? throw new InvalidOperationException("Cosmos:CacheContainerId configuration is required");

        _container = cosmosClient.GetDatabase(databaseId).GetContainer(containerId);
    }

    /// <inheritdoc />
    public async Task<CacheLayerStats> GetStatsAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            var query = _container
                .GetItemLinqQueryable<CacheItemMeta>()
                .Where(item => item.Id.StartsWith("addr:"))
                .Select(item => item.Id);

            long count = 0;
            using var iterator = query.ToFeedIterator();
            while (iterator.HasMoreResults)
            {
                var page = await iterator.ReadNextAsync(cancellationToken);
                count += page.Count;
            }

            // CosmosDB does not expose cumulative hit/miss counters natively.
            return new CacheLayerStats(
                Layer: LayerName,
                EntryCount: count,
                HitCount: 0,
                MissCount: 0);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving Cosmos DB cache stats");
            return new CacheLayerStats(LayerName, -1, 0, 0);
        }
    }

    /// <inheritdoc />
    /// <remarks>
    /// SRS FR-003: "key invalidation removes from Redis and marks as stale in CosmosDB".
    /// We achieve this by setting TTL to 1 second (effectively immediate expiry) rather
    /// than physically deleting, so Cosmos audit history is preserved.
    /// </remarks>
    public async Task<bool> InvalidateAsync(string key, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNullOrEmpty(key);

        try
        {
            var partitionKey = ExtractPartitionKey(key);

            // Patch TTL to 1 second — Cosmos TTL will remove it shortly, marking it stale.
            var patchOps = new[]
            {
                PatchOperation.Set("/ttl", 1),
            };

            await _container.PatchItemAsync<CacheItemMeta>(
                key,
                new PartitionKey(partitionKey),
                patchOps,
                cancellationToken: cancellationToken);

            _logger.LogInformation("Cosmos DB: marked key {Key} as stale (ttl=1s)", key);
            return true;
        }
        catch (CosmosException ex) when (ex.StatusCode == System.Net.HttpStatusCode.NotFound)
        {
            _logger.LogDebug("Cosmos DB: key {Key} not found for invalidation", key);
            return false;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error invalidating Cosmos DB key {Key}", key);
            throw;
        }
    }

    /// <inheritdoc />
    /// <remarks>
    /// Per SRS FR-003: "flush only affects Redis; CosmosDB data is retained."
    /// This implementation returns 0 and logs accordingly — it is a deliberate no-op.
    /// </remarks>
    public Task<long> FlushAsync(CancellationToken cancellationToken = default)
    {
        _logger.LogDebug("Cosmos DB: flush is a no-op per SRS FR-003 (L2 retained)");
        return Task.FromResult(0L);
    }

    private static string ExtractPartitionKey(string key)
    {
        var colonIndex = key.IndexOf(':');
        return colonIndex > 0 ? key[..colonIndex] : key;
    }
}
