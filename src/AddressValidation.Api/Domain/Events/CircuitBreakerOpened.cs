namespace AddressValidation.Api.Domain.Events;

using System.Text.Json.Serialization;

/// <summary>
/// Raised when the Polly circuit breaker transitions to the Open state
/// (provider is being isolated due to repeated failures).
/// SRS Ref: Section 7.2
/// </summary>
public sealed class CircuitBreakerOpened : DomainEvent
{
    public override string EventType => "CircuitBreakerOpened";

    /// <summary>
    /// Name of the circuit breaker policy (e.g. "smarty-provider").
    /// </summary>
    [JsonPropertyName("policyName")]
    public required string PolicyName { get; init; }

    /// <summary>
    /// Duration in seconds for which the circuit will remain open.
    /// </summary>
    [JsonPropertyName("breakDurationSeconds")]
    public double BreakDurationSeconds { get; init; }

    /// <summary>
    /// Exception message that triggered the final failure, if available.
    /// Stack trace is never included.
    /// </summary>
    [JsonPropertyName("triggerReason")]
    public string? TriggerReason { get; init; }
}
