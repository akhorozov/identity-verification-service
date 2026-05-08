using Microsoft.Extensions.Diagnostics.HealthChecks;
using AddressValidation.Api.Infrastructure.Providers.Smarty;

namespace AddressValidation.Api.Infrastructure.HealthChecks;

/// <summary>
/// Verifies Smarty API connectivity by issuing a minimal probe request.
/// A response of any kind (including 401 / 402) confirms the endpoint is reachable.
/// Used by the readiness and startup probes.
/// SRS Ref: FR-005
/// </summary>
public sealed class SmartyHealthCheck : IHealthCheck
{
    private readonly ISmartyApi _smartyApi;
    private readonly ILogger<SmartyHealthCheck> _logger;

    /// <summary>
    /// Initializes a new instance of <see cref="SmartyHealthCheck"/>.
    /// </summary>
    public SmartyHealthCheck(ISmartyApi smartyApi, ILogger<SmartyHealthCheck> logger)
    {
        ArgumentNullException.ThrowIfNull(smartyApi);
        ArgumentNullException.ThrowIfNull(logger);
        _smartyApi = smartyApi;
        _logger = logger;
    }

    /// <inheritdoc />
    public async Task<HealthCheckResult> CheckHealthAsync(
        HealthCheckContext context,
        CancellationToken cancellationToken = default)
    {
        try
        {
            // Issue a minimal request — any HTTP response means the endpoint is reachable.
            // Refit throws ApiException on 4xx/5xx; we treat those as "reachable but degraded"
            // rather than "unreachable", since they indicate auth/quota issues, not connectivity.
            await _smartyApi.ValidateAddressAsync(
                street: "1 Infinite Loop",
                street2: null,
                city: "Cupertino",
                state: "CA",
                zipcode: "95014",
                addressee: null,
                candidates: 1,
                cancellationToken: cancellationToken);

            return HealthCheckResult.Healthy("Smarty API is reachable and responding.");
        }
        catch (Refit.ApiException ex) when ((int)ex.StatusCode >= 400 && (int)ex.StatusCode < 500)
        {
            // 401/402 means the API key is invalid or quota exceeded, but the endpoint IS reachable.
            _logger.LogWarning("Smarty API responded with {StatusCode} during health check", ex.StatusCode);
            return HealthCheckResult.Degraded($"Smarty API reachable but returned {(int)ex.StatusCode}: {ex.ReasonPhrase}");
        }
        catch (Exception ex)
        {
            return HealthCheckResult.Unhealthy("Smarty API unreachable.", ex);
        }
    }
}
