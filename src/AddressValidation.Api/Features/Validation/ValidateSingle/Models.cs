namespace AddressValidation.Api.Features.Validation.ValidateSingle;

using System.Text.Json.Serialization;
using AddressValidation.Api.Domain;

/// <summary>
/// Request model for single address validation (FR-001).
/// SRS Ref: Section 9.3.1
/// </summary>
public sealed record ValidateSingleRequest
{
    /// <summary>Primary street address line. Required, 1–100 characters.</summary>
    [JsonPropertyName("street")]
    public required string Street { get; init; }

    /// <summary>Secondary address line (apt, suite, etc.). Optional.</summary>
    [JsonPropertyName("street2")]
    public string? Street2 { get; init; }

    /// <summary>City name. Either City+State or ZipCode must be provided.</summary>
    [JsonPropertyName("city")]
    public string? City { get; init; }

    /// <summary>Two-letter US state abbreviation. Required when City is provided.</summary>
    [JsonPropertyName("state")]
    public string? State { get; init; }

    /// <summary>5-digit or 5+4 ZIP code. Either this or City+State must be provided.</summary>
    [JsonPropertyName("zipCode")]
    public string? ZipCode { get; init; }

    /// <summary>ZIP+4 extension. Optional.</summary>
    [JsonPropertyName("plus4")]
    public string? Plus4 { get; init; }

    /// <summary>Converts the request to the shared domain <see cref="AddressInput"/> model.</summary>
    public AddressInput ToAddressInput() => new()
    {
        Street  = Street,
        Street2 = Street2,
        City    = City,
        State   = State,
        ZipCode = ZipCode is not null && Plus4 is not null
            ? $"{ZipCode}-{Plus4}"
            : ZipCode
    };
}

/// <summary>
/// Response model for single address validation (FR-001).
/// Wraps the domain <see cref="ValidationResponse"/> with endpoint-level metadata.
/// SRS Ref: Section 9.3.1
/// </summary>
public sealed record ValidateSingleResponse
{
    /// <summary>Original input address as received.</summary>
    [JsonPropertyName("input")]
    public required AddressInput Input { get; init; }

    /// <summary>USPS-standardised address. Null when address is undeliverable.</summary>
    [JsonPropertyName("address")]
    public ValidatedAddress? Address { get; init; }

    /// <summary>DPV analysis and deliverability footnotes.</summary>
    [JsonPropertyName("analysis")]
    public AddressAnalysis? Analysis { get; init; }

    /// <summary>Geocoding coordinates and precision.</summary>
    [JsonPropertyName("geocoding")]
    public GeocodingResult? Geocoding { get; init; }

    /// <summary>Provider, timing, cache source, and API version metadata.</summary>
    [JsonPropertyName("metadata")]
    public required ValidationMetadata Metadata { get; init; }

    /// <summary>Creates a <see cref="ValidateSingleResponse"/> from a domain <see cref="ValidationResponse"/>.</summary>
    public static ValidateSingleResponse FromDomain(ValidationResponse domain) => new()
    {
        Input    = domain.InputAddress,
        Address  = domain.ValidatedAddress,
        Analysis = domain.Analysis,
        Geocoding = domain.Geocoding,
        Metadata = domain.Metadata
    };
}
