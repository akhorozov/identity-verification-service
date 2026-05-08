namespace AddressValidation.Api.Domain.Events;

using System.Text.Json.Serialization;

/// <summary>
/// Raised when a cache hit occurs (entry read from L1 Redis or L2 CosmosDB).
/// SRS Ref: Section 7.2
/// </summary>
public sealed class CacheEntryRetrieved : DomainEvent
{
    public override string EventType => "CacheEntryRetrieved";

    /// <summary>
    /// The cache key that was read.
    /// </summary>
    [JsonPropertyName("cacheKey")]
    public required string CacheKey { get; init; }

    /// <summary>
    /// Cache layer the hit originated from ("L1" = Redis, "L2" = CosmosDB).
    /// </summary>
    [JsonPropertyName("cacheLayer")]
    public required string CacheLayer { get; init; }

    /// <summary>
    /// Age of the cached entry in seconds at retrieval time, if known.
    /// </summary>
    [JsonPropertyName("ageSeconds")]
    public long? AgeSeconds { get; init; }
}
