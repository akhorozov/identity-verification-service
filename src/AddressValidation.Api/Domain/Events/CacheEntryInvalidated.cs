namespace AddressValidation.Api.Domain.Events;

using System.Text.Json.Serialization;

/// <summary>
/// Raised when a single cache entry is invalidated via the cache management API.
/// SRS Ref: Section 7.2, FR-003
/// </summary>
public sealed class CacheEntryInvalidated : DomainEvent
{
    public override string EventType => "CacheEntryInvalidated";

    /// <summary>
    /// The cache key that was invalidated.
    /// </summary>
    [JsonPropertyName("cacheKey")]
    public required string CacheKey { get; init; }

    /// <summary>
    /// Cache layers from which the entry was removed (e.g. ["L1", "L2"]).
    /// </summary>
    [JsonPropertyName("cacheLayers")]
    public required IReadOnlyList<string> CacheLayers { get; init; }
}
