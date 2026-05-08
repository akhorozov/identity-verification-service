using Microsoft.Extensions.Diagnostics.HealthChecks;
using StackExchange.Redis;

namespace AddressValidation.Api.Infrastructure.HealthChecks;

/// <summary>
/// Verifies Redis (L1 cache) connectivity by issuing a PING command.
/// Used by the readiness and startup probes.
/// SRS Ref: FR-005
/// </summary>
public sealed class RedisHealthCheck : IHealthCheck
{
    private readonly IConnectionMultiplexer _connectionMultiplexer;

    /// <summary>
    /// Initializes a new instance of <see cref="RedisHealthCheck"/>.
    /// </summary>
    public RedisHealthCheck(IConnectionMultiplexer connectionMultiplexer)
    {
        ArgumentNullException.ThrowIfNull(connectionMultiplexer);
        _connectionMultiplexer = connectionMultiplexer;
    }

    /// <inheritdoc />
    public async Task<HealthCheckResult> CheckHealthAsync(
        HealthCheckContext context,
        CancellationToken cancellationToken = default)
    {
        try
        {
            if (!_connectionMultiplexer.IsConnected)
            {
                return HealthCheckResult.Unhealthy("Redis connection is not established.");
            }

            var db = _connectionMultiplexer.GetDatabase();
            var latency = await db.PingAsync();

            return latency.TotalMilliseconds < 1000
                ? HealthCheckResult.Healthy($"Redis responding. Latency: {latency.TotalMilliseconds:F1}ms")
                : HealthCheckResult.Degraded($"Redis responding but slow. Latency: {latency.TotalMilliseconds:F1}ms");
        }
        catch (Exception ex)
        {
            return HealthCheckResult.Unhealthy("Redis unreachable.", ex);
        }
    }
}
