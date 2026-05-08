using Microsoft.Azure.Cosmos;
using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace AddressValidation.Api.Infrastructure.HealthChecks;

/// <summary>
/// Verifies Azure Cosmos DB (L2 cache) connectivity by reading the account properties.
/// Used by the readiness and startup probes.
/// SRS Ref: FR-005
/// </summary>
public sealed class CosmosDbHealthCheck : IHealthCheck
{
    private readonly CosmosClient _cosmosClient;
    private readonly IConfiguration _configuration;

    /// <summary>
    /// Initializes a new instance of <see cref="CosmosDbHealthCheck"/>.
    /// </summary>
    public CosmosDbHealthCheck(CosmosClient cosmosClient, IConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(cosmosClient);
        ArgumentNullException.ThrowIfNull(configuration);
        _cosmosClient = cosmosClient;
        _configuration = configuration;
    }

    /// <inheritdoc />
    public async Task<HealthCheckResult> CheckHealthAsync(
        HealthCheckContext context,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var databaseId = _configuration["Cosmos:DatabaseId"];
            if (string.IsNullOrWhiteSpace(databaseId))
            {
                return HealthCheckResult.Unhealthy("Cosmos:DatabaseId is not configured.");
            }

            // ReadAsync on the database is a lightweight check that verifies connectivity.
            var database = _cosmosClient.GetDatabase(databaseId);
            var response = await database.ReadAsync(cancellationToken: cancellationToken);

            return response.StatusCode == System.Net.HttpStatusCode.OK
                ? HealthCheckResult.Healthy($"Cosmos DB responding. RU charge: {response.RequestCharge:F1}")
                : HealthCheckResult.Degraded($"Cosmos DB returned unexpected status: {response.StatusCode}");
        }
        catch (CosmosException ex)
        {
            return HealthCheckResult.Unhealthy($"Cosmos DB error: {ex.StatusCode}", ex);
        }
        catch (Exception ex)
        {
            return HealthCheckResult.Unhealthy("Cosmos DB unreachable.", ex);
        }
    }
}
