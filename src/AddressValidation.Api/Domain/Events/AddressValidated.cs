namespace AddressValidation.Api.Domain.Events;

using System.Text.Json.Serialization;

/// <summary>
/// Raised when an address has been successfully validated by the upstream provider.
/// Address data is stored only as a SHA-256 hash — no raw PII.
/// SRS Ref: Section 7.2, FR-004
/// </summary>
public sealed class AddressValidated : DomainEvent
{
    public override string EventType => "AddressValidated";

    /// <summary>
    /// SHA-256 hash of the normalised input address (same as AggregateId / cache key).
    /// </summary>
    [JsonPropertyName("addressHash")]
    public required string AddressHash { get; init; }

    /// <summary>
    /// DPV match code returned by the provider (Y / S / D).
    /// </summary>
    [JsonPropertyName("dpvMatchCode")]
    public required string DpvMatchCode { get; init; }

    /// <summary>
    /// Name of the validation provider used (e.g. "Smarty").
    /// </summary>
    [JsonPropertyName("providerName")]
    public required string ProviderName { get; init; }

    /// <summary>
    /// Cache layer that served or stored the result ("PROVIDER", "L1", "L2").
    /// </summary>
    [JsonPropertyName("cacheSource")]
    public required string CacheSource { get; init; }

    /// <summary>
    /// Duration in milliseconds from request receipt to validated response.
    /// </summary>
    [JsonPropertyName("durationMs")]
    public long DurationMs { get; init; }
}
