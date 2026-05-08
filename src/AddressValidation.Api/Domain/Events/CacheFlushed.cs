namespace AddressValidation.Api.Domain.Events;

using System.Text.Json.Serialization;

/// <summary>
/// Raised when the entire cache is flushed via the cache management API.
/// SRS Ref: Section 7.2, FR-003
/// </summary>
public sealed class CacheFlushed : DomainEvent
{
    public override string EventType => "CacheFlushed";

    /// <summary>
    /// Cache layers that were flushed (e.g. ["L1", "L2"]).
    /// </summary>
    [JsonPropertyName("cacheLayers")]
    public required IReadOnlyList<string> CacheLayers { get; init; }

    /// <summary>
    /// Approximate number of entries removed across all flushed layers.
    /// </summary>
    [JsonPropertyName("entriesRemoved")]
    public long EntriesRemoved { get; init; }
}
