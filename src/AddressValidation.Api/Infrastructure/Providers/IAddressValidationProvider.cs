namespace AddressValidation.Api.Infrastructure.Providers;

using AddressValidation.Api.Domain;

/// <summary>
/// Abstraction for an external address validation provider.
/// Implementations are responsible for calling the upstream API and mapping
/// the result to <see cref="ValidationResponse"/>.
/// </summary>
public interface IAddressValidationProvider
{
    /// <summary>
    /// Gets a human-readable name for this provider (e.g. "Smarty").
    /// Used to populate <see cref="ValidationMetadata.ProviderName"/>.
    /// </summary>
    string ProviderName { get; }

    /// <summary>
    /// Validates a single US address against the upstream provider.
    /// </summary>
    /// <param name="input">The raw address input from the client.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>
    /// A <see cref="ValidationResponse"/> on success, or <c>null</c> if the
    /// provider could not match the address.
    /// </returns>
    Task<ValidationResponse?> ValidateAsync(AddressInput input, CancellationToken cancellationToken = default);
}
