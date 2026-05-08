namespace AddressValidation.Api.Domain.Events;

using System.Text.Json.Serialization;

/// <summary>
/// Raised when address validation fails (provider error, timeout, or undeliverable result).
/// No raw address data is stored — only the hash and failure reason.
/// SRS Ref: Section 7.2, FR-004
/// </summary>
public sealed class AddressValidationFailed : DomainEvent
{
    public override string EventType => "AddressValidationFailed";

    /// <summary>
    /// SHA-256 hash of the normalised input address.
    /// </summary>
    [JsonPropertyName("addressHash")]
    public required string AddressHash { get; init; }

    /// <summary>
    /// Human-readable reason for the failure (e.g. "ProviderTimeout", "Undeliverable").
    /// </summary>
    [JsonPropertyName("failureReason")]
    public required string FailureReason { get; init; }

    /// <summary>
    /// HTTP status code returned to the caller, if applicable.
    /// </summary>
    [JsonPropertyName("httpStatusCode")]
    public int? HttpStatusCode { get; init; }

    /// <summary>
    /// Name of the provider that failed, if a provider call was attempted.
    /// </summary>
    [JsonPropertyName("providerName")]
    public string? ProviderName { get; init; }
}
