namespace AddressValidation.Api.Infrastructure.Providers.Smarty;

using Refit;

/// <summary>
/// Refit-generated HTTP client for the Smarty US Street Address API.
/// Base URL is configured via <c>Smarty:BaseUrl</c> (default: https://us-street.api.smarty.com).
/// Authentication is supplied via query parameters <c>auth-id</c> and <c>auth-token</c>
/// injected through <see cref="SmartyAuthHandler"/>.
/// </summary>
public interface ISmartyApi
{
    /// <summary>
    /// Validates a single US street address.
    /// Returns up to <paramref name="candidates"/> USPS-matched address candidates.
    /// </summary>
    [Get("/street-address")]
    Task<IReadOnlyList<SmartyCandidate>> ValidateAddressAsync(
        [Query] string street,
        [Query] string? street2,
        [Query] string? city,
        [Query] string? state,
        [Query] string? zipcode,
        [Query] string? addressee,
        [Query] int candidates = 1,
        CancellationToken cancellationToken = default);
}
