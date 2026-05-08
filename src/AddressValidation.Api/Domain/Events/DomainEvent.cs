namespace AddressValidation.Api.Domain.Events;

using System.Text.Json.Serialization;

/// <summary>
/// Abstract base class for all domain events in the address validation system.
/// Implements the event schema defined in SRS Section 7.3.
/// All properties are immutable (init-only) and UTC-timestamped.
/// </summary>
public abstract class DomainEvent
{
    /// <summary>
    /// Unique identifier for this event instance (UUID v4).
    /// </summary>
    [JsonPropertyName("eventId")]
    public string EventId { get; init; } = Guid.NewGuid().ToString();

    /// <summary>
    /// Discriminator string identifying the event type (e.g. "AddressValidated").
    /// Used as a CosmosDB index and Change Feed filter.
    /// </summary>
    [JsonPropertyName("eventType")]
    public abstract string EventType { get; }

    /// <summary>
    /// Identifier of the aggregate (e.g. SHA-256 address hash) this event belongs to.
    /// Never contains raw PII — always a deterministic hash.
    /// </summary>
    [JsonPropertyName("aggregateId")]
    public required string AggregateId { get; init; }

    /// <summary>
    /// UTC timestamp when the event occurred.
    /// Used as the CosmosDB partition key value (date portion).
    /// </summary>
    [JsonPropertyName("requestDate")]
    public DateTimeOffset RequestDate { get; init; } = DateTimeOffset.UtcNow;

    /// <summary>
    /// Distributed trace / correlation ID propagated from the originating HTTP request.
    /// </summary>
    [JsonPropertyName("requestCorrelationId")]
    public string? RequestCorrelationId { get; init; }

    /// <summary>
    /// Identity of the caller (API key hash or service account name).
    /// Never contains raw credentials.
    /// </summary>
    [JsonPropertyName("userId")]
    public string? UserId { get; init; }

    /// <summary>
    /// Semantic version of the service that produced this event (e.g. "1.0.0").
    /// Enables forward-compatibility analysis in downstream consumers.
    /// </summary>
    [JsonPropertyName("serviceVersion")]
    public string ServiceVersion { get; init; } = "1.0.0";
}
