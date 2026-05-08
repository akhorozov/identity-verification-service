namespace AddressValidation.Tests.Unit.Infrastructure.Telemetry;

using System.Diagnostics;
using AddressValidation.Api.Infrastructure.Telemetry;
using Xunit;

public class AddressValidationActivitySourceTests
{
    [Fact]
    public void ActivitySource_HasCorrectName()
    {
        Assert.Equal(
            AddressValidationActivitySource.ServiceName,
            AddressValidationActivitySource.ActivitySource.Name);
    }

    [Fact]
    public void ServiceName_IsCorrectValue()
    {
        Assert.Equal("AddressValidation.Api", AddressValidationActivitySource.ServiceName);
    }

    [Fact]
    public void ActivitySource_CanStartSpan_WhenListenerRegistered()
    {
        // Arrange
        using var listener = new ActivityListener
        {
            ShouldListenTo = src => src.Name == AddressValidationActivitySource.ServiceName,
            Sample = (ref ActivityCreationOptions<ActivityContext> _) => ActivitySamplingResult.AllData,
        };
        ActivitySource.AddActivityListener(listener);

        // Act
        using var activity = AddressValidationActivitySource.ActivitySource
            .StartActivity(AddressValidationActivitySource.SmartyValidate, ActivityKind.Client);

        // Assert
        Assert.NotNull(activity);
        Assert.Equal(AddressValidationActivitySource.SmartyValidate, activity.DisplayName);
        Assert.Equal(ActivityKind.Client, activity.Kind);
    }

    [Fact]
    public void SpanNames_AreNotNullOrEmpty()
    {
        Assert.NotEmpty(AddressValidationActivitySource.SmartyValidate);
        Assert.NotEmpty(AddressValidationActivitySource.CacheGet);
        Assert.NotEmpty(AddressValidationActivitySource.CacheSet);
        Assert.NotEmpty(AddressValidationActivitySource.CacheInvalidate);
    }
}
