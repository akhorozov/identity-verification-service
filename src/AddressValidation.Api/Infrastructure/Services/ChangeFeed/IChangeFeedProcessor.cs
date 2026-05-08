namespace AddressValidation.Api.Infrastructure.Services.ChangeFeed;

using AddressValidation.Api.Domain.Events;

/// <summary>
/// Abstraction over the CosmosDB Change Feed processor lifecycle.
/// Allows starting and stopping the processor and exposes the handler
/// that processes batches of domain events as they arrive.
/// SRS Ref: Section 7.4, T15 #139
/// </summary>
public interface IChangeFeedProcessor
{
    /// <summary>Starts the Change Feed processor. Idempotent.</summary>
    Task StartAsync(CancellationToken cancellationToken = default);

    /// <summary>Stops the Change Feed processor gracefully.</summary>
    Task StopAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Processes a batch of domain events delivered by the Change Feed.
    /// Exposed for testing without requiring a live CosmosDB connection.
    /// </summary>
    Task HandleChangesAsync(
        IReadOnlyCollection<DomainEvent> changes,
        CancellationToken cancellationToken);
}
