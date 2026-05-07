namespace AddressValidation.Api.Domain;

/// <summary>
/// Represents metadata about the validation response.
/// Includes information about the provider, timing, cache source, and API version.
/// </summary>
public class ValidationMetadata
{
    /// <summary>
    /// Name of the address validation provider used.
    /// Example: "Smarty" (or could be "Google", "Melissa Data" in future implementations)
    /// </summary>
    public required string ProviderName { get; set; }

    /// <summary>
    /// Timestamp (UTC) when the address was validated by the provider.
    /// Used to track freshness of cached results.
    /// </summary>
    public required DateTimeOffset ValidatedAt { get; set; }

    /// <summary>
    /// Indicates which cache layer returned this result.
    /// 
    /// Values:
    /// - "L1" = Redis hot cache (< 1ms typical response time)
    /// - "L2" = CosmosDB persistent cache (10-15ms typical response time)
    /// - "PROVIDER" = Freshly validated via Smarty API
    /// </summary>
    public required string CacheSource { get; set; }

    /// <summary>
    /// API version that processed this request.
    /// Example: "1.0"
    /// Used for tracking compatibility and deprecation warnings.
    /// </summary>
    public required string ApiVersion { get; set; }

    /// <summary>
    /// Total time (in milliseconds) to process the request.
    /// Measured from request receipt to response generation.
    /// Useful for performance monitoring and SLA tracking.
    /// </summary>
    public long RequestDurationMs { get; set; }

    /// <summary>
    /// Optional correlation ID for distributed tracing.
    /// Enables tracing the request through multiple services.
    /// Example: "00-4bf92f3577b34da6a3ce929d0e0e4736-00f067aa0ba902b7-01"
    /// </summary>
    public string? CorrelationId { get; set; }
}
