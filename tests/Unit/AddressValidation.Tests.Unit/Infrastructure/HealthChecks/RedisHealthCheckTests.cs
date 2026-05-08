namespace AddressValidation.Tests.Unit.Infrastructure.HealthChecks;

using AddressValidation.Api.Infrastructure.HealthChecks;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using NSubstitute;
using NSubstitute.ExceptionExtensions;
using StackExchange.Redis;
using Xunit;

/// <summary>
/// Unit tests for <see cref="RedisHealthCheck"/>.
/// </summary>
public class RedisHealthCheckTests
{
    private readonly IConnectionMultiplexer _multiplexer;
    private readonly IDatabase _database;
    private readonly RedisHealthCheck _sut;

    public RedisHealthCheckTests()
    {
        _multiplexer = Substitute.For<IConnectionMultiplexer>();
        _database = Substitute.For<IDatabase>();
        _multiplexer.IsConnected.Returns(true);
        _multiplexer.GetDatabase(Arg.Any<int>(), Arg.Any<object?>()).Returns(_database);

        _sut = new RedisHealthCheck(_multiplexer);
    }

    [Fact]
    public async Task CheckHealthAsync_WhenPingIsfast_ReturnsHealthy()
    {
        _database.PingAsync(Arg.Any<CommandFlags>()).Returns(TimeSpan.FromMilliseconds(5));

        var result = await _sut.CheckHealthAsync(new HealthCheckContext());

        Assert.Equal(HealthStatus.Healthy, result.Status);
    }

    [Fact]
    public async Task CheckHealthAsync_WhenPingIsSlowButBelowThreshold_ReturnsDegraded()
    {
        _database.PingAsync(Arg.Any<CommandFlags>()).Returns(TimeSpan.FromMilliseconds(1500));

        var result = await _sut.CheckHealthAsync(new HealthCheckContext());

        Assert.Equal(HealthStatus.Degraded, result.Status);
    }

    [Fact]
    public async Task CheckHealthAsync_WhenNotConnected_ReturnsUnhealthy()
    {
        _multiplexer.IsConnected.Returns(false);

        var result = await _sut.CheckHealthAsync(new HealthCheckContext());

        Assert.Equal(HealthStatus.Unhealthy, result.Status);
    }

    [Fact]
    public async Task CheckHealthAsync_WhenExceptionThrown_ReturnsUnhealthy()
    {
        _database.PingAsync(Arg.Any<CommandFlags>())
            .ThrowsAsync(new RedisConnectionException(ConnectionFailureType.UnableToConnect, "Redis unavailable"));

        var result = await _sut.CheckHealthAsync(new HealthCheckContext());

        Assert.Equal(HealthStatus.Unhealthy, result.Status);
    }
}
