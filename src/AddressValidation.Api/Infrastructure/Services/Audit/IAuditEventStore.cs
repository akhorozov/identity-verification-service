namespace AddressValidation.Api.Infrastructure.Services.Audit;

using AddressValidation.Api.Domain.Events;

/// <summary>
/// Abstraction for the append-only audit event store.
/// Implementations must never allow updates or deletes to stored events.
/// SRS Ref: Section 11.2, FR-004
/// </summary>
public interface IAuditEventStore
{
    /// <summary>
    /// Appends a single domain event to the audit store.
    /// </summary>
    /// <param name="domainEvent">The event to persist. Must not be null.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    Task AppendAsync(DomainEvent domainEvent, CancellationToken cancellationToken = default);

    /// <summary>
    /// Appends multiple domain events to the audit store in a single batch operation.
    /// Events are written atomically where the underlying store supports it.
    /// </summary>
    /// <param name="domainEvents">The events to persist. Must not be null or empty.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    Task AppendBatchAsync(IReadOnlyList<DomainEvent> domainEvents, CancellationToken cancellationToken = default);

    /// <summary>
    /// Queries the audit store for events matching the supplied filters.
    /// </summary>
    /// <param name="query">Filter criteria. Pass an empty <see cref="AuditEventQuery"/> to retrieve recent events.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Matching events ordered by <see cref="DomainEvent.RequestDate"/> descending.</returns>
    Task<IReadOnlyList<DomainEvent>> QueryAsync(AuditEventQuery query, CancellationToken cancellationToken = default);
}
