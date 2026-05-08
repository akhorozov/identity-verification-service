namespace AddressValidation.Api.Infrastructure.Providers.Smarty;

using AddressValidation.Api.Domain;
using AddressValidation.Api.Infrastructure.Metrics;
using Microsoft.Extensions.Logging;

/// <summary>
/// Address validation provider backed by the Smarty US Street Address API.
/// Uses a Refit-generated <see cref="ISmartyApi"/> client and maps the first
/// candidate to a domain <see cref="ValidationResponse"/>.
/// </summary>
public sealed class SmartyProvider : IAddressValidationProvider
{
    private readonly ISmartyApi _api;
    private readonly ILogger<SmartyProvider> _logger;
    private readonly AppMetrics _metrics;

    public string ProviderName => "Smarty";

    public SmartyProvider(ISmartyApi api, ILogger<SmartyProvider> logger, AppMetrics metrics)
    {
        ArgumentNullException.ThrowIfNull(api);
        ArgumentNullException.ThrowIfNull(logger);
        ArgumentNullException.ThrowIfNull(metrics);

        _api = api;
        _logger = logger;
        _metrics = metrics;
    }

    /// <inheritdoc />
    public async Task<ValidationResponse?> ValidateAsync(
        AddressInput input,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(input);

        _logger.LogDebug("Calling Smarty provider for street={Street}", input.Street);

        IReadOnlyList<SmartyCandidate> candidates;
        try
        {
            candidates = await _api.ValidateAddressAsync(
                street: input.Street,
                street2: input.Street2,
                city: input.City,
                state: input.State,
                zipcode: input.ZipCode,
                addressee: input.Addressee,
                candidates: 1,
                cancellationToken: cancellationToken);

            _metrics.SmartyApiCallsTotal.WithLabels("200").Inc();
        }
        catch (Refit.ApiException ex)
        {
            var statusCode = ((int)ex.StatusCode).ToString();
            _metrics.SmartyApiCallsTotal.WithLabels(statusCode).Inc();
            _metrics.SmartyApiErrorsTotal.WithLabels(ex.GetType().Name).Inc();
            throw;
        }
        catch (Exception ex)
        {
            _metrics.SmartyApiCallsTotal.WithLabels("0").Inc();
            _metrics.SmartyApiErrorsTotal.WithLabels(ex.GetType().Name).Inc();
            throw;
        }

        if (candidates is not { Count: > 0 })
        {
            _logger.LogInformation("Smarty returned no candidates for street={Street}", input.Street);
            return null;
        }

        var response = SmartyResponseMapper.MapToResponse(candidates[0], input, cacheSource: "PROVIDER");

        _logger.LogDebug(
            "Smarty validated street={Street} → dpv={DpvMatchCode}",
            input.Street,
            response.Analysis?.DpvMatchCode);

        return response;
    }
}
