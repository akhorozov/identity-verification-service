using Microsoft.Azure.Cosmos;
using System.Collections.ObjectModel;

namespace AddressValidation.Api.Infrastructure.Services.Caching;

/// <summary>
/// Service for initializing Azure Cosmos DB containers with optimized settings for caching.
/// Creates container if not exists and applies indexing policies.
/// </summary>
public class CosmosDbInitializationService
{
    private readonly CosmosClient _client;
    private readonly IConfiguration _configuration;
    private readonly ILogger<CosmosDbInitializationService> _logger;

    /// <summary>
    /// Initializes a new instance of the CosmosDbInitializationService class.
    /// </summary>
    /// <param name="client">Azure Cosmos DB client.</param>
    /// <param name="configuration">Application configuration.</param>
    /// <param name="logger">Logger instance.</param>
    public CosmosDbInitializationService(
        CosmosClient client,
        IConfiguration configuration,
        ILogger<CosmosDbInitializationService> logger)
    {
        _client = client ?? throw new ArgumentNullException(nameof(client));
        _configuration = configuration ?? throw new ArgumentNullException(nameof(configuration));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    /// <summary>
    /// Initializes the Cosmos DB database and cache container with optimized settings.
    /// </summary>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>A task representing the async operation.</returns>
    /// <exception cref="InvalidOperationException">Thrown when required configuration is missing.</exception>
    public async Task InitializeAsync(CancellationToken cancellationToken = default)
    {
        var databaseId = _configuration["Cosmos:DatabaseId"]
            ?? throw new InvalidOperationException("Cosmos:DatabaseId configuration is required");
        var containerId = _configuration["Cosmos:CacheContainerId"]
            ?? throw new InvalidOperationException("Cosmos:CacheContainerId configuration is required");
        var partitionKeyPath = _configuration["Cosmos:PartitionKeyPath"] ?? "/pk";

        try
        {
            _logger.LogInformation("Initializing Cosmos DB database: {DatabaseId}", databaseId);

            // Create database if not exists
            var database = await _client.CreateDatabaseIfNotExistsAsync(databaseId, cancellationToken: cancellationToken);
            _logger.LogInformation("Database ensured: {DatabaseId}", databaseId);

            // Create container with TTL and optimized indexing
            await CreateContainerIfNotExistsAsync(database.Database, containerId, partitionKeyPath, cancellationToken);
            _logger.LogInformation("Container initialization completed for: {ContainerId}", containerId);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error initializing Cosmos DB");
            throw;
        }
    }

    /// <summary>
    /// Creates the cache container with optimized indexing policy for cache operations.
    /// </summary>
    private async Task CreateContainerIfNotExistsAsync(
        Database database,
        string containerId,
        string partitionKeyPath,
        CancellationToken cancellationToken)
    {
        try
        {
            // Define container properties
            var containerProperties = new ContainerProperties
            {
                Id = containerId,
                PartitionKeyPath = partitionKeyPath,

                // Enable TTL at container level (default 1 day = 86400 seconds)
                DefaultTimeToLive = 86400,

                // Optimized indexing policy for cache operations
                IndexingPolicy = new IndexingPolicy
                {
                    IndexingMode = IndexingMode.Consistent,

                    // Exclude all paths by default (most cache keys are not queried)
                    IncludedPaths = 
                    {
                        // Index partition key for queries
                        new IncludedPath { Path = "/pk/?" },
                        // Index id for point reads
                        new IncludedPath { Path = "/id/?" },
                    },

                    // Exclude these paths from indexing (reduce storage and cost)
                    ExcludedPaths = 
                    {
                        new ExcludedPath { Path = "/value/*" },   // Cached JSON values
                        new ExcludedPath { Path = "/*" },           // Catch-all
                    },

                    // Composite indexes for common query patterns (if needed later)
                    CompositeIndexes = 
                    {
                        // Example: Query by pk and type
                        new Collection<CompositePath>
                        {
                            new CompositePath { Path = "/pk", Order = CompositePathSortOrder.Ascending },
                            new CompositePath { Path = "/type", Order = CompositePathSortOrder.Ascending },
                        },
                    },
                },
            };

            var response = await database.CreateContainerIfNotExistsAsync(
                containerProperties,
                throughput: 400, // Start with manual throughput (400 RU/s)
                cancellationToken: cancellationToken);

            _logger.LogInformation(
                "Container created/verified: {ContainerId} with partition key: {PartitionKey}",
                containerId,
                partitionKeyPath);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error creating container: {ContainerId}", containerId);
            throw;
        }
    }
}
