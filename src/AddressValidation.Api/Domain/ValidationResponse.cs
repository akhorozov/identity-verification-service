namespace AddressValidation.Api.Domain;

/// <summary>
/// Represents the complete validation response for a single address.
/// This is the aggregate root that combines input, validated address, analysis, geocoding, and metadata.
/// Returned by both single and batch validation endpoints.
/// </summary>
public class ValidationResponse
{
    /// <summary>
    /// The original input address as provided by the client.
    /// Preserved for reference and audit purposes.
    /// </summary>
    public required AddressInput InputAddress { get; set; }

    /// <summary>
    /// The USPS-standardized, CASS-certified address components.
    /// Null if validation failed (address not found or undeliverable).
    /// </summary>
    public ValidatedAddress? ValidatedAddress { get; set; }

    /// <summary>
    /// DPV analysis and footnotes for deliverability assessment.
    /// Null if validation failed.
    /// </summary>
    public AddressAnalysis? Analysis { get; set; }

    /// <summary>
    /// Geocoding coordinates and precision metadata.
    /// Null if geocoding was not available or failed.
    /// </summary>
    public GeocodingResult? Geocoding { get; set; }

    /// <summary>
    /// Metadata about the validation response (provider, timing, cache source, API version).
    /// Always populated regardless of validation success/failure.
    /// </summary>
    public required ValidationMetadata Metadata { get; set; }

    /// <summary>
    /// High-level status of the validation.
    /// 
    /// Values:
    /// - "validated" = Address successfully validated and standardized
    /// - "ambiguous" = Multiple candidates matched, primary candidate selected
    /// - "invalid" = Address invalid or not found in USPS database
    /// - "undeliverable" = Address valid but marked as undeliverable
    /// </summary>
    public required string Status { get; set; }

    /// <summary>
    /// Human-readable error or status message.
    /// Populated when Status != "validated" to explain the reason.
    /// Example: "Address not found in USPS database"
    /// </summary>
    public string? Message { get; set; }

    /// <summary>
    /// For batch responses: the zero-based index of this result in the input request.
    /// Used to maintain ordering when some addresses fail validation.
    /// Null for single address responses.
    /// </summary>
    public int? InputIndex { get; set; }
}
