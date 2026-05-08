namespace AddressValidation.Api.Domain.Events;

using System.Text.Json.Serialization;

/// <summary>
/// Raised when a new entry is written to the cache (L1 Redis or L2 CosmosDB).
/// SRS Ref: Section 7.2
/// </summary>
public sealed class CacheEntryCreated : DomainEvent
{
    public override string EventType => "CacheEntryCreated";

    /// <summary>
    /// The cache key used to store the entry (addr:v1:{hash}).
    /// </summary>
    [JsonPropertyName("cacheKey")]
    public required string CacheKey { get; init; }

    /// <summary>
    /// Cache layer the entry was written to ("L1" = Redis, "L2" = CosmosDB).
    /// </summary>
    [JsonPropertyName("cacheLayer")]
    public required string CacheLayer { get; init; }

    /// <summary>
    /// TTL in seconds applied to the entry at write time.
    /// </summary>
    [JsonPropertyName("ttlSeconds")]
    public int? TtlSeconds { get; init; }
}
