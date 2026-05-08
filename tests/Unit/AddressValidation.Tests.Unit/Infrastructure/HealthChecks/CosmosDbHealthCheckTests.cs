namespace AddressValidation.Tests.Unit.Infrastructure.HealthChecks;

using System.Net;
using AddressValidation.Api.Infrastructure.HealthChecks;
using Microsoft.Azure.Cosmos;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using NSubstitute;
using NSubstitute.ExceptionExtensions;
using Xunit;

/// <summary>
/// Unit tests for <see cref="CosmosDbHealthCheck"/>.
/// </summary>
public class CosmosDbHealthCheckTests
{
    private readonly CosmosClient _cosmosClient;
    private readonly IConfiguration _configuration;
    private readonly CosmosDbHealthCheck _sut;

    public CosmosDbHealthCheckTests()
    {
        _cosmosClient = Substitute.For<CosmosClient>();
        _configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                { "Cosmos:DatabaseId", "test-db" },
            })
            .Build();

        _sut = new CosmosDbHealthCheck(_cosmosClient, _configuration);
    }

    [Fact]
    public async Task CheckHealthAsync_WhenDatabaseRespondsOk_ReturnsHealthy()
    {
        var database = Substitute.For<Database>();
        var response = Substitute.For<DatabaseResponse>();
        response.StatusCode.Returns(HttpStatusCode.OK);
        response.RequestCharge.Returns(1.0);
        database.ReadAsync(cancellationToken: Arg.Any<CancellationToken>()).Returns(response);
        _cosmosClient.GetDatabase("test-db").Returns(database);

        var context = new HealthCheckContext();
        var result = await _sut.CheckHealthAsync(context);

        Assert.Equal(HealthStatus.Healthy, result.Status);
    }

    [Fact]
    public async Task CheckHealthAsync_WhenDatabaseIdNotConfigured_ReturnsUnhealthy()
    {
        var emptyConfig = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>())
            .Build();

        var sut = new CosmosDbHealthCheck(_cosmosClient, emptyConfig);
        var context = new HealthCheckContext();

        var result = await sut.CheckHealthAsync(context);

        Assert.Equal(HealthStatus.Unhealthy, result.Status);
        Assert.Contains("DatabaseId", result.Description);
    }

    [Fact]
    public async Task CheckHealthAsync_WhenCosmosExceptionThrown_ReturnsUnhealthy()
    {
        var database = Substitute.For<Database>();
        database.ReadAsync(cancellationToken: Arg.Any<CancellationToken>())
            .ThrowsAsync(new CosmosException("Service unavailable", HttpStatusCode.ServiceUnavailable, 0, "", 0));
        _cosmosClient.GetDatabase("test-db").Returns(database);

        var context = new HealthCheckContext();
        var result = await _sut.CheckHealthAsync(context);

        Assert.Equal(HealthStatus.Unhealthy, result.Status);
    }

    [Fact]
    public async Task CheckHealthAsync_WhenUnexpectedExceptionThrown_ReturnsUnhealthy()
    {
        var database = Substitute.For<Database>();
        database.ReadAsync(cancellationToken: Arg.Any<CancellationToken>())
            .ThrowsAsync(new InvalidOperationException("Connection refused"));
        _cosmosClient.GetDatabase("test-db").Returns(database);

        var context = new HealthCheckContext();
        var result = await _sut.CheckHealthAsync(context);

        Assert.Equal(HealthStatus.Unhealthy, result.Status);
    }
}
