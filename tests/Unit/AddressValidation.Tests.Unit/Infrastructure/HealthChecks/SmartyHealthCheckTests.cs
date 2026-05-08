namespace AddressValidation.Tests.Unit.Infrastructure.HealthChecks;

using System.Net;
using AddressValidation.Api.Infrastructure.HealthChecks;
using AddressValidation.Api.Infrastructure.Providers.Smarty;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Logging;
using NSubstitute;
using NSubstitute.ExceptionExtensions;
using Refit;
using Xunit;

/// <summary>
/// Unit tests for <see cref="SmartyHealthCheck"/>.
/// </summary>
public class SmartyHealthCheckTests
{
    private readonly ISmartyApi _smartyApi;
    private readonly ILogger<SmartyHealthCheck> _logger;
    private readonly SmartyHealthCheck _sut;

    public SmartyHealthCheckTests()
    {
        _smartyApi = Substitute.For<ISmartyApi>();
        _logger = Substitute.For<ILogger<SmartyHealthCheck>>();
        _sut = new SmartyHealthCheck(_smartyApi, _logger);
    }

    [Fact]
    public async Task CheckHealthAsync_WhenApiRespondsSuccessfully_ReturnsHealthy()
    {
        _smartyApi.ValidateAddressAsync(
                Arg.Any<string>(), Arg.Any<string?>(), Arg.Any<string?>(),
                Arg.Any<string?>(), Arg.Any<string?>(), Arg.Any<string?>(),
                Arg.Any<int>(), Arg.Any<CancellationToken>())
            .Returns([]);

        var result = await _sut.CheckHealthAsync(new HealthCheckContext());

        Assert.Equal(HealthStatus.Healthy, result.Status);
    }

    [Fact]
    public async Task CheckHealthAsync_WhenApiReturns401_ReturnsDegraded()
    {
        var apiEx = await ApiException.Create(
            new HttpRequestMessage(),
            HttpMethod.Get,
            new HttpResponseMessage(HttpStatusCode.Unauthorized),
            new RefitSettings());

        _smartyApi.ValidateAddressAsync(
                Arg.Any<string>(), Arg.Any<string?>(), Arg.Any<string?>(),
                Arg.Any<string?>(), Arg.Any<string?>(), Arg.Any<string?>(),
                Arg.Any<int>(), Arg.Any<CancellationToken>())
            .ThrowsAsync(apiEx);

        var result = await _sut.CheckHealthAsync(new HealthCheckContext());

        Assert.Equal(HealthStatus.Degraded, result.Status);
    }

    [Fact]
    public async Task CheckHealthAsync_WhenNetworkExceptionThrown_ReturnsUnhealthy()
    {
        _smartyApi.ValidateAddressAsync(
                Arg.Any<string>(), Arg.Any<string?>(), Arg.Any<string?>(),
                Arg.Any<string?>(), Arg.Any<string?>(), Arg.Any<string?>(),
                Arg.Any<int>(), Arg.Any<CancellationToken>())
            .ThrowsAsync(new HttpRequestException("Network unreachable"));

        var result = await _sut.CheckHealthAsync(new HealthCheckContext());

        Assert.Equal(HealthStatus.Unhealthy, result.Status);
    }
}
