namespace AddressValidation.Api.Infrastructure.Services.Audit;

using System.Text.Json;
using System.Text.Json.Serialization;
using AddressValidation.Api.Domain.Events;
using Microsoft.Azure.Cosmos;
using Microsoft.Azure.Cosmos.Linq;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

/// <summary>
/// Append-only audit event store backed by Azure Cosmos DB.
/// Writes use strong consistency. Updates and deletes are never performed.
/// Container: <c>audit-events</c>, partition key: <c>/requestDate</c>.
/// SRS Ref: FR-004, Section 7.1, ADR-005
/// </summary>
public sealed class CosmosAuditEventStore : IAuditEventStore
{
    private readonly Container _container;
    private readonly ILogger<CosmosAuditEventStore> _logger;

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    /// <summary>
    /// Internal document model stored in Cosmos DB.
    /// The <c>id</c> field is the EventId; the partition key is the date portion of RequestDate.
    /// </summary>
    private sealed class AuditDocument
    {
        [JsonPropertyName("id")]
        public string Id { get; init; } = string.Empty;

        [JsonPropertyName("eventType")]
        public string EventType { get; init; } = string.Empty;

        [JsonPropertyName("aggregateId")]
        public string AggregateId { get; init; } = string.Empty;

        /// <summary>Partition key value — ISO 8601 date string (yyyy-MM-dd).</summary>
        [JsonPropertyName("requestDate")]
        public string RequestDate { get; init; } = string.Empty;

        [JsonPropertyName("requestCorrelationId")]
        public string? RequestCorrelationId { get; init; }

        [JsonPropertyName("userId")]
        public string? UserId { get; init; }

        [JsonPropertyName("serviceVersion")]
        public string ServiceVersion { get; init; } = string.Empty;

        /// <summary>Full event payload serialised as a JSON string for schema flexibility.</summary>
        [JsonPropertyName("payload")]
        public string Payload { get; init; } = string.Empty;

        /// <summary>CosmosDB TTL in seconds (365 days = 31 536 000 s).</summary>
        [JsonPropertyName("ttl")]
        public int Ttl { get; init; } = 31_536_000;
    }

    public CosmosAuditEventStore(
        CosmosClient cosmosClient,
        IConfiguration configuration,
        ILogger<CosmosAuditEventStore> logger)
    {
        ArgumentNullException.ThrowIfNull(cosmosClient);
        ArgumentNullException.ThrowIfNull(configuration);
        ArgumentNullException.ThrowIfNull(logger);

        var databaseId = configuration["Cosmos:DatabaseId"]
            ?? throw new InvalidOperationException("Cosmos:DatabaseId configuration is required.");
        var containerId = configuration["Cosmos:AuditContainerId"] ?? "audit-events";

        _container = cosmosClient.GetContainer(databaseId, containerId);
        _logger = logger;
    }

    /// <inheritdoc />
    public async Task AppendAsync(DomainEvent domainEvent, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(domainEvent);

        var doc = ToDocument(domainEvent);

        await _container.CreateItemAsync(
            doc,
            new PartitionKey(doc.RequestDate),
            new ItemRequestOptions { ConsistencyLevel = ConsistencyLevel.Strong },
            cancellationToken);

        _logger.LogDebug(
            "Audit event appended: {EventType} id={EventId}",
            domainEvent.EventType, domainEvent.EventId);
    }

    /// <inheritdoc />
    public async Task AppendBatchAsync(
        IReadOnlyList<DomainEvent> domainEvents,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(domainEvents);
        if (domainEvents.Count == 0) return;

        // Group by partition key (date) and use transactional batches within each partition
        var groups = domainEvents
            .Select(ToDocument)
            .GroupBy(d => d.RequestDate);

        foreach (var group in groups)
        {
            var batch = _container.CreateTransactionalBatch(new PartitionKey(group.Key));
            foreach (var doc in group)
            {
                batch.CreateItem(doc, new TransactionalBatchItemRequestOptions
                {
                    EnableContentResponseOnWrite = false
                });
            }

            using var response = await batch.ExecuteAsync(cancellationToken);
            if (!response.IsSuccessStatusCode)
            {
                _logger.LogError(
                    "Audit batch write failed for partition {Date}: {StatusCode}",
                    group.Key, response.StatusCode);
                throw new InvalidOperationException(
                    $"Audit batch write failed with status {response.StatusCode}.");
            }
        }

        _logger.LogDebug("Audit batch of {Count} events appended.", domainEvents.Count);
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<DomainEvent>> QueryAsync(
        AuditEventQuery query,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(query);

        var queryable = _container
            .GetItemLinqQueryable<AuditDocument>(
                requestOptions: new QueryRequestOptions { MaxItemCount = query.MaxResults })
            .AsQueryable();

        if (query.DateFrom.HasValue)
        {
            var from = query.DateFrom.Value.UtcDateTime.ToString("yyyy-MM-dd");
            queryable = queryable.Where(d => string.Compare(d.RequestDate, from) >= 0);
        }

        if (query.DateTo.HasValue)
        {
            var to = query.DateTo.Value.UtcDateTime.ToString("yyyy-MM-dd");
            queryable = queryable.Where(d => string.Compare(d.RequestDate, to) <= 0);
        }

        if (!string.IsNullOrWhiteSpace(query.EventType))
            queryable = queryable.Where(d => d.EventType == query.EventType);

        if (!string.IsNullOrWhiteSpace(query.AggregateId))
            queryable = queryable.Where(d => d.AggregateId == query.AggregateId);

        var results = new List<DomainEvent>();
        using var feedIterator = queryable
            .OrderByDescending(d => d.RequestDate)
            .Take(query.MaxResults)
            .ToFeedIterator();

        while (feedIterator.HasMoreResults)
        {
            var page = await feedIterator.ReadNextAsync(cancellationToken);
            foreach (var doc in page)
            {
                var evt = FromDocument(doc);
                if (evt is not null) results.Add(evt);
            }
        }

        return results;
    }

    // ── Private helpers ──────────────────────────────────────────────────────

    private static AuditDocument ToDocument(DomainEvent evt) => new()
    {
        Id = evt.EventId,
        EventType = evt.EventType,
        AggregateId = evt.AggregateId,
        RequestDate = evt.RequestDate.UtcDateTime.ToString("yyyy-MM-dd"),
        RequestCorrelationId = evt.RequestCorrelationId,
        UserId = evt.UserId,
        ServiceVersion = evt.ServiceVersion,
        Payload = JsonSerializer.Serialize(evt, evt.GetType(), JsonOptions)
    };

    private static DomainEvent? FromDocument(AuditDocument doc)
    {
        var type = doc.EventType switch
        {
            "AddressValidated"        => typeof(Domain.Events.AddressValidated),
            "AddressValidationFailed" => typeof(Domain.Events.AddressValidationFailed),
            "CacheEntryCreated"       => typeof(Domain.Events.CacheEntryCreated),
            "CacheEntryRetrieved"     => typeof(Domain.Events.CacheEntryRetrieved),
            "CacheEntryInvalidated"   => typeof(Domain.Events.CacheEntryInvalidated),
            "CacheFlushed"            => typeof(Domain.Events.CacheFlushed),
            "CircuitBreakerOpened"    => typeof(Domain.Events.CircuitBreakerOpened),
            "CircuitBreakerClosed"    => typeof(Domain.Events.CircuitBreakerClosed),
            _                         => null
        };

        if (type is null) return null;
        return (DomainEvent?)JsonSerializer.Deserialize(doc.Payload, type, JsonOptions);
    }
}
