namespace AddressValidation.Tests.Unit.Infrastructure.Metrics;

using AddressValidation.Api.Infrastructure.Metrics;
using Xunit;

/// <summary>
/// Unit tests for <see cref="AppMetrics"/> — verifies all FR-006 metrics are registered
/// and accessible with the correct names and labels.
/// </summary>
public class AppMetricsTests
{
    private readonly AppMetrics _sut = new();

    [Fact]
    public void ValidationRequestsTotal_IsNotNull()
        => Assert.NotNull(_sut.ValidationRequestsTotal);

    [Fact]
    public void ValidationDurationSeconds_IsNotNull()
        => Assert.NotNull(_sut.ValidationDurationSeconds);

    [Fact]
    public void CacheHitRatio_IsNotNull()
        => Assert.NotNull(_sut.CacheHitRatio);

    [Fact]
    public void SmartyApiCallsTotal_IsNotNull()
        => Assert.NotNull(_sut.SmartyApiCallsTotal);

    [Fact]
    public void SmartyApiErrorsTotal_IsNotNull()
        => Assert.NotNull(_sut.SmartyApiErrorsTotal);

    [Fact]
    public void ActiveCircuitBreakers_IsNotNull()
        => Assert.NotNull(_sut.ActiveCircuitBreakers);

    [Fact]
    public void ValidationRequestsTotal_CanIncrementWithLabels()
    {
        _sut.ValidationRequestsTotal.WithLabels("validate_single", "200", "1.0").Inc();
        Assert.True(_sut.ValidationRequestsTotal.WithLabels("validate_single", "200", "1.0").Value >= 1);
    }

    [Fact]
    public void ValidationDurationSeconds_CanObserveWithLabels()
    {
        // Should not throw
        _sut.ValidationDurationSeconds.WithLabels("validate_single", "L1").Observe(0.05);
    }

    [Fact]
    public void CacheHitRatio_CanSetWithLabels()
    {
        _sut.CacheHitRatio.WithLabels("L1-Redis").Set(0.85);
        Assert.Equal(0.85, _sut.CacheHitRatio.WithLabels("L1-Redis").Value, precision: 5);
    }

    [Fact]
    public void SmartyApiCallsTotal_CanIncrementWithLabels()
    {
        _sut.SmartyApiCallsTotal.WithLabels("200").Inc();
        Assert.True(_sut.SmartyApiCallsTotal.WithLabels("200").Value >= 1);
    }

    [Fact]
    public void SmartyApiErrorsTotal_CanIncrementWithLabels()
    {
        _sut.SmartyApiErrorsTotal.WithLabels("ApiException").Inc();
        Assert.True(_sut.SmartyApiErrorsTotal.WithLabels("ApiException").Value >= 1);
    }

    [Fact]
    public void ActiveCircuitBreakers_CanSetWithLabels()
    {
        _sut.ActiveCircuitBreakers.WithLabels("Smarty").Set(1);
        Assert.Equal(1, _sut.ActiveCircuitBreakers.WithLabels("Smarty").Value);
    }
}
