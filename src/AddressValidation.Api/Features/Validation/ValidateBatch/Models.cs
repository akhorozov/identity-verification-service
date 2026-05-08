namespace AddressValidation.Api.Features.Validation.ValidateBatch;

using System.Text.Json.Serialization;
using AddressValidation.Api.Domain;
using AddressValidation.Api.Features.Validation.ValidateSingle;

/// <summary>
/// A single address item within a batch validation request.
/// SRS Ref: FR-002, Section 9.3.2
/// </summary>
public sealed record ValidateBatchItem
{
    /// <summary>Primary street address line. Required.</summary>
    [JsonPropertyName("street")]
    public required string Street { get; init; }

    /// <summary>Secondary address line (suite, apt, etc.). Optional.</summary>
    [JsonPropertyName("street2")]
    public string? Street2 { get; init; }

    /// <summary>City name. Required when ZipCode is not provided.</summary>
    [JsonPropertyName("city")]
    public string? City { get; init; }

    /// <summary>Two-letter US state abbreviation. Required when ZipCode is not provided.</summary>
    [JsonPropertyName("state")]
    public string? State { get; init; }

    /// <summary>5-digit ZIP code, optionally with +4 suffix. Required when City/State are not provided.</summary>
    [JsonPropertyName("zipCode")]
    public string? ZipCode { get; init; }

    /// <summary>4-digit ZIP+4 extension. Optional.</summary>
    [JsonPropertyName("plus4")]
    public string? Plus4 { get; init; }

    /// <summary>Converts this batch item to the domain <see cref="AddressInput"/> model.</summary>
    public AddressInput ToAddressInput() => new()
    {
        Street   = Street,
        Street2  = Street2,
        City     = City,
        State    = State,
        ZipCode  = ZipCode is not null && Plus4 is not null
            ? $"{ZipCode}-{Plus4}"
            : ZipCode,
    };

    /// <summary>Converts this batch item to a <see cref="ValidateSingleRequest"/> for reuse of validation rules.</summary>
    public ValidateSingleRequest ToSingleRequest() => new()
    {
        Street   = Street,
        Street2  = Street2,
        City     = City,
        State    = State,
        ZipCode  = ZipCode,
        Plus4    = Plus4,
    };
}

/// <summary>
/// The result for a single address within a batch validation response.
/// SRS Ref: FR-002, Section 9.3.2
/// </summary>
public sealed record ValidateBatchResultItem
{
    /// <summary>Zero-based index matching the original request array position.</summary>
    [JsonPropertyName("inputIndex")]
    public required int InputIndex { get; init; }

    /// <summary>Original input address as received.</summary>
    [JsonPropertyName("input")]
    public required AddressInput Input { get; init; }

    /// <summary>USPS-standardised address. Null when address is undeliverable or errored.</summary>
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
    public ValidationMetadata? Metadata { get; init; }

    /// <summary>Validation/processing status for this individual address.</summary>
    [JsonPropertyName("status")]
    public required string Status { get; init; }

    /// <summary>Human-readable error message when status is "failed".</summary>
    [JsonPropertyName("error")]
    public string? Error { get; init; }

    /// <summary>Creates a successful result item from a domain <see cref="ValidationResponse"/>.</summary>
    public static ValidateBatchResultItem FromDomain(int index, ValidationResponse domain) => new()
    {
        InputIndex = index,
        Input      = domain.InputAddress,
        Address    = domain.ValidatedAddress,
        Analysis   = domain.Analysis,
        Geocoding  = domain.Geocoding,
        Metadata   = domain.Metadata,
        Status     = domain.Status,
    };

    /// <summary>Creates a failed result item for an address that could not be validated.</summary>
    public static ValidateBatchResultItem Failed(int index, AddressInput input, string reason) => new()
    {
        InputIndex = index,
        Input      = input,
        Status     = "failed",
        Error      = reason,
    };
}

/// <summary>
/// Aggregated statistics for a batch validation request.
/// SRS Ref: FR-002, Section 9.3.2 — batch summary
/// </summary>
public sealed record ValidateBatchSummary
{
    /// <summary>Total number of addresses submitted.</summary>
    [JsonPropertyName("total")]
    public required int Total { get; init; }

    /// <summary>Number of successfully validated addresses.</summary>
    [JsonPropertyName("validated")]
    public required int Validated { get; init; }

    /// <summary>Number of failed/undeliverable addresses.</summary>
    [JsonPropertyName("failed")]
    public required int Failed { get; init; }

    /// <summary>Number of results served from any cache level (L1 or L2).</summary>
    [JsonPropertyName("cacheHits")]
    public required int CacheHits { get; init; }

    /// <summary>Number of results that required a provider call.</summary>
    [JsonPropertyName("cacheMisses")]
    public required int CacheMisses { get; init; }

    /// <summary>Total processing duration in milliseconds.</summary>
    [JsonPropertyName("durationMs")]
    public required long DurationMs { get; init; }
}

/// <summary>
/// Request model for batch address validation (FR-002).
/// SRS Ref: FR-002, Section 9.3.2
/// </summary>
public sealed record ValidateBatchRequest
{
    /// <summary>Array of address items to validate. Maximum 100 per request.</summary>
    [JsonPropertyName("addresses")]
    public required ValidateBatchItem[] Addresses { get; init; }
}

/// <summary>
/// Response model for batch address validation (FR-002).
/// HTTP 200 when all succeed; HTTP 207 when at least one fails.
/// SRS Ref: FR-002, Section 9.3.2
/// </summary>
public sealed record ValidateBatchResponse
{
    /// <summary>Individual results in the same order as the input array.</summary>
    [JsonPropertyName("results")]
    public required ValidateBatchResultItem[] Results { get; init; }

    /// <summary>Aggregated batch statistics.</summary>
    [JsonPropertyName("summary")]
    public required ValidateBatchSummary Summary { get; init; }
}
