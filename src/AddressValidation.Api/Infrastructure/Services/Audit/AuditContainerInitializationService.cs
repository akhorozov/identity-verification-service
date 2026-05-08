namespace AddressValidation.Api.Infrastructure.Services.Audit;

using System.Collections.ObjectModel;
using Microsoft.Azure.Cosmos;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

/// <summary>
/// Hosted startup service that ensures the <c>audit-events</c> Cosmos DB container exists
/// with the correct partition key (<c>/requestDate</c>), TTL of 365 days, Change Feed enabled,
/// and an indexing policy optimised for the <see cref="IAuditEventStore"/> query patterns.
/// SRS Ref: FR-004, Section 7.1, ADR-005 — Container design
/// </summary>
public sealed class AuditContainerInitializationService : IHostedService
{
    private readonly CosmosClient _client;
    private readonly IConfiguration _configuration;
    private readonly ILogger<AuditContainerInitializationService> _logger;

    /// <summary>365 days expressed in seconds.</summary>
    private const int AuditTtlSeconds = 365 * 24 * 60 * 60;

    public AuditContainerInitializationService(
        CosmosClient client,
        IConfiguration configuration,
        ILogger<AuditContainerInitializationService> logger)
    {
        ArgumentNullException.ThrowIfNull(client);
        ArgumentNullException.ThrowIfNull(configuration);
        ArgumentNullException.ThrowIfNull(logger);

        _client = client;
        _configuration = configuration;
        _logger = logger;
    }

    /// <inheritdoc />
    public async Task StartAsync(CancellationToken cancellationToken)
    {
        var databaseId = _configuration["Cosmos:DatabaseId"]
            ?? throw new InvalidOperationException("Cosmos:DatabaseId configuration is required.");
        var containerId = _configuration["Cosmos:AuditContainerId"] ?? "audit-events";

        try
        {
            _logger.LogInformation(
                "Ensuring Cosmos DB audit container {ContainerId} in database {DatabaseId}.",
                containerId, databaseId);

            var dbResponse = await _client.CreateDatabaseIfNotExistsAsync(
                databaseId, cancellationToken: cancellationToken);

            await CreateAuditContainerIfNotExistsAsync(dbResponse.Database, containerId, cancellationToken);

            _logger.LogInformation("Audit container {ContainerId} is ready.", containerId);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to initialise Cosmos DB audit container.");
            throw;
        }
    }

    /// <inheritdoc />
    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;

    // ── Private helpers ──────────────────────────────────────────────────────

    private async Task CreateAuditContainerIfNotExistsAsync(
        Database database,
        string containerId,
        CancellationToken cancellationToken)
    {
        var properties = new ContainerProperties
        {
            Id = containerId,

            // Partition key is the date portion of RequestDate (yyyy-MM-dd)
            // This spreads writes evenly across days and supports efficient time-range queries.
            PartitionKeyPath = "/requestDate",

            // Retain audit events for 365 days then expire automatically.
            DefaultTimeToLive = AuditTtlSeconds,

            // Change Feed is enabled on all Cosmos containers by default in the SDK — no extra
            // property is needed. The Change Feed processor can be attached to this container
            // downstream by subscribing to it via the Cosmos Change Feed Processor library.

            IndexingPolicy = new IndexingPolicy
            {
                IndexingMode = IndexingMode.Consistent,

                IncludedPaths =
                {
                    // Index the fields used in filter/sort queries.
                    new IncludedPath { Path = "/requestDate/?" },
                    new IncludedPath { Path = "/eventType/?" },
                    new IncludedPath { Path = "/aggregateId/?" },
                    new IncludedPath { Path = "/serviceVersion/?" },
                },

                ExcludedPaths =
                {
                    // Exclude the serialised payload from indexing — it can be large and
                    // is never queried directly via index.
                    new ExcludedPath { Path = "/payload/*" },
                    new ExcludedPath { Path = "/*" },
                },

                // Composite index for the primary query pattern: date range ordered by date.
                CompositeIndexes =
                {
                    new Collection<CompositePath>
                    {
                        new CompositePath { Path = "/requestDate", Order = CompositePathSortOrder.Descending },
                        new CompositePath { Path = "/eventType",   Order = CompositePathSortOrder.Ascending  },
                    },
                    new Collection<CompositePath>
                    {
                        new CompositePath { Path = "/aggregateId", Order = CompositePathSortOrder.Ascending  },
                        new CompositePath { Path = "/requestDate", Order = CompositePathSortOrder.Descending },
                    },
                },
            },
        };

        var response = await database.CreateContainerIfNotExistsAsync(
            properties,
            throughput: null, // inherits database-level auto-scale
            cancellationToken: cancellationToken);

        _logger.LogInformation(
            "Audit container status: {StatusCode} — {ContainerId}.",
            response.StatusCode, containerId);
    }
}
