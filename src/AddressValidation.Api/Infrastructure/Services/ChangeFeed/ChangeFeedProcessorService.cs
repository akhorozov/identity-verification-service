namespace AddressValidation.Api.Infrastructure.Services.ChangeFeed;

using System.Text.Json;
using System.Text.Json.Serialization;
using AddressValidation.Api.Domain.Events;
using Microsoft.Azure.Cosmos;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

/// <summary>
/// CosmosDB Change Feed processor that watches the <c>audit-events</c> container
/// and emits structured log entries for observability and downstream alerting.
///
/// Design notes:
/// <list type="bullet">
///   <item>One lease container per deployment; lease documents are created automatically.</item>
///   <item>The processor is started/stopped as a hosted background service (see
///         <see cref="ChangeFeedHostedService"/>).</item>
///   <item>Errors in individual event handlers are logged but do not abort the batch —
///         the processor advances its lease so the feed does not stall.</item>
/// </list>
/// SRS Ref: Section 7.4, T15 #139
/// </summary>
public sealed class ChangeFeedProcessorService : IChangeFeedProcessor
{
    private readonly CosmosClient _cosmosClient;
    private readonly IConfiguration _configuration;
    private readonly ILogger<ChangeFeedProcessorService> _logger;

    private ChangeFeedProcessor? _processor;

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        Converters = { new JsonStringEnumConverter() }
    };

    /// <summary>
    /// Initializes a new instance of <see cref="ChangeFeedProcessorService"/>.
    /// </summary>
    public ChangeFeedProcessorService(
        CosmosClient cosmosClient,
        IConfiguration configuration,
        ILogger<ChangeFeedProcessorService> logger)
    {
        ArgumentNullException.ThrowIfNull(cosmosClient);
        ArgumentNullException.ThrowIfNull(configuration);
        ArgumentNullException.ThrowIfNull(logger);

        _cosmosClient = cosmosClient;
        _configuration = configuration;
        _logger = logger;
    }

    /// <inheritdoc/>
    public async Task StartAsync(CancellationToken cancellationToken = default)
    {
        if (_processor is not null)
        {
            _logger.LogDebug("ChangeFeedProcessor is already started; skipping.");
            return;
        }

        var databaseName = _configuration["CosmosDb:DatabaseName"] ?? "address-validation";
        var containerName = _configuration["CosmosDb:AuditContainerName"] ?? "audit-events";
        var leaseContainerName = _configuration["CosmosDb:LeaseContainerName"] ?? "audit-leases";
        var processorName = _configuration["CosmosDb:ChangeFeedProcessorName"] ?? "audit-change-feed";

        var database = _cosmosClient.GetDatabase(databaseName);

        // Ensure the lease container exists (created with minimal throughput).
        await database.CreateContainerIfNotExistsAsync(
            new ContainerProperties(leaseContainerName, "/id"),
            ThroughputProperties.CreateAutoscaleThroughput(1000),
            cancellationToken: cancellationToken);

        var monitoredContainer = database.GetContainer(containerName);
        var leaseContainer = database.GetContainer(leaseContainerName);

        _processor = monitoredContainer
            .GetChangeFeedProcessorBuilder<JsonElement>(processorName, HandleChangesInternalAsync)
            .WithInstanceName(Environment.MachineName)
            .WithLeaseContainer(leaseContainer)
            .WithPollInterval(TimeSpan.FromSeconds(5))
            .Build();

        await _processor.StartAsync();

        _logger.LogInformation(
            "CosmosDB Change Feed processor '{ProcessorName}' started on container '{Container}'.",
            processorName, containerName);
    }

    /// <inheritdoc/>
    public async Task StopAsync(CancellationToken cancellationToken = default)
    {
        if (_processor is null) return;

        await _processor.StopAsync();
        _processor = null;

        _logger.LogInformation("CosmosDB Change Feed processor stopped.");
    }

    /// <inheritdoc/>
    public async Task HandleChangesAsync(
        IReadOnlyCollection<DomainEvent> changes,
        CancellationToken cancellationToken)
    {
        if (changes.Count == 0) return;

        _logger.LogDebug("Processing {Count} domain event(s) from Change Feed.", changes.Count);

        foreach (var domainEvent in changes)
        {
            cancellationToken.ThrowIfCancellationRequested();
            ProcessDomainEvent(domainEvent);
        }

        await Task.CompletedTask;
    }

    // ─── Private helpers ────────────────────────────────────────────────────

    /// <summary>
    /// Callback invoked by the CosmosDB SDK for each batch of raw JSON documents.
    /// Deserialises each document to a <see cref="DomainEvent"/> and delegates to
    /// <see cref="HandleChangesAsync"/>.
    /// </summary>
    private async Task HandleChangesInternalAsync(
        ChangeFeedProcessorContext context,
        IReadOnlyCollection<JsonElement> changes,
        CancellationToken cancellationToken)
    {
        var events = new List<DomainEvent>(changes.Count);

        foreach (var element in changes)
        {
            try
            {
                var domainEvent = DeserializeDomainEvent(element);
                if (domainEvent is not null)
                    events.Add(domainEvent);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex,
                    "Failed to deserialise Change Feed document. LeaseToken={LeaseToken}",
                    context.LeaseToken);
            }
        }

        await HandleChangesAsync(events, cancellationToken);
    }

    /// <summary>
    /// Deserialises a raw <see cref="JsonElement"/> to a typed <see cref="DomainEvent"/>
    /// using the <c>eventType</c> discriminator field.
    /// </summary>
    private DomainEvent? DeserializeDomainEvent(JsonElement element)
    {
        if (!element.TryGetProperty("eventType", out var eventTypeProp))
        {
            _logger.LogDebug("Change Feed document missing 'eventType'; skipping.");
            return null;
        }

        var eventType = eventTypeProp.GetString();
        var json = element.GetRawText();

        return eventType switch
        {
            "AddressValidated" => JsonSerializer.Deserialize<AddressValidated>(json, JsonOptions),
            "AddressValidationFailed" => JsonSerializer.Deserialize<AddressValidationFailed>(json, JsonOptions),
            _ => DeserializeAsGenericEvent(json, eventType)
        };
    }

    private DomainEvent? DeserializeAsGenericEvent(string json, string? eventType)
    {
        _logger.LogDebug("Change Feed: unrecognised eventType '{EventType}'; skipping.", eventType);
        return null;
    }

    /// <summary>Extracts business metrics and structured log entries from a domain event.</summary>
    private void ProcessDomainEvent(DomainEvent domainEvent)
    {
        try
        {
            switch (domainEvent)
            {
                case AddressValidated validated:
                    _logger.LogInformation(
                        "ChangeFeed | AddressValidated | AggregateId={AggregateId} Provider={Provider} " +
                        "DpvMatchCode={DpvMatchCode} CacheSource={CacheSource} CorrelationId={CorrelationId}",
                        validated.AggregateId,
                        validated.ProviderName,
                        validated.DpvMatchCode,
                        validated.CacheSource,
                        validated.RequestCorrelationId);
                    break;

                case AddressValidationFailed failed:
                    _logger.LogWarning(
                        "ChangeFeed | AddressValidationFailed | AggregateId={AggregateId} " +
                        "Reason={Reason} CorrelationId={CorrelationId}",
                        failed.AggregateId,
                        failed.FailureReason,
                        failed.RequestCorrelationId);
                    break;

                default:
                    _logger.LogDebug(
                        "ChangeFeed | UnhandledEvent | EventType={EventType} EventId={EventId}",
                        domainEvent.EventType,
                        domainEvent.EventId);
                    break;
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex,
                "Error processing domain event EventId={EventId} EventType={EventType}",
                domainEvent.EventId,
                domainEvent.EventType);
        }
    }
}
