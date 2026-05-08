namespace AddressValidation.Api.Infrastructure.Services.Audit;

using AddressValidation.Api.Domain.Events;

/// <summary>
/// Filter parameters for querying the audit event store.
/// All fields are optional — omitting a field means "no filter on that dimension".
/// </summary>
public sealed record AuditEventQuery
{
    /// <summary>Start of the date range (inclusive). Filters on <see cref="DomainEvent.RequestDate"/>.</summary>
    public DateTimeOffset? DateFrom { get; init; }

    /// <summary>End of the date range (inclusive). Filters on <see cref="DomainEvent.RequestDate"/>.</summary>
    public DateTimeOffset? DateTo { get; init; }

    /// <summary>Filter by <see cref="DomainEvent.EventType"/> discriminator string.</summary>
    public string? EventType { get; init; }

    /// <summary>Filter by <see cref="DomainEvent.AggregateId"/> (SHA-256 address hash).</summary>
    public string? AggregateId { get; init; }

    /// <summary>Maximum number of events to return. Defaults to 100.</summary>
    public int MaxResults { get; init; } = 100;
}
