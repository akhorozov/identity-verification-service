namespace AddressValidation.Api.Domain.Events;

using System.Text.Json.Serialization;

/// <summary>
/// Raised when the Polly circuit breaker transitions from Open back to Closed
/// (provider is considered healthy again).
/// SRS Ref: Section 7.2
/// </summary>
public sealed class CircuitBreakerClosed : DomainEvent
{
    public override string EventType => "CircuitBreakerClosed";

    /// <summary>
    /// Name of the circuit breaker policy (e.g. "smarty-provider").
    /// </summary>
    [JsonPropertyName("policyName")]
    public required string PolicyName { get; init; }

    /// <summary>
    /// Duration in seconds the circuit was open before closing.
    /// </summary>
    [JsonPropertyName("openDurationSeconds")]
    public double OpenDurationSeconds { get; init; }
}
