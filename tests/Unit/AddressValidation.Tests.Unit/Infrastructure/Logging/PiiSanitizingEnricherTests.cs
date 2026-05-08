namespace AddressValidation.Tests.Unit.Infrastructure.Logging;

using AddressValidation.Api.Infrastructure.Logging;
using NSubstitute;
using Serilog.Core;
using Serilog.Events;
using Serilog.Parsing;
using Xunit;

public class PiiSanitizingEnricherTests
{
    private static ILogEventPropertyValueFactory MakeFactory()
    {
        var factory = Substitute.For<ILogEventPropertyValueFactory>();
        factory.CreatePropertyValue(Arg.Any<object>(), Arg.Any<bool>())
               .Returns(call => new ScalarValue(call.ArgAt<object>(0)));
        return factory;
    }

    private static LogEvent MakeEvent(params (string key, string value)[] properties)
    {
        var props = properties.Select(p =>
            new LogEventProperty(p.key, new ScalarValue(p.value)));

        return new LogEvent(
            DateTimeOffset.UtcNow,
            LogEventLevel.Information,
            null,
            new MessageTemplate("test", []),
            props);
    }

    private static ILogEventPropertyFactory MakePropertyFactory()
    {
        var factory = Substitute.For<ILogEventPropertyFactory>();
        factory.CreateProperty(Arg.Any<string>(), Arg.Any<object?>(), Arg.Any<bool>())
               .Returns(call => new LogEventProperty(call.ArgAt<string>(0), new ScalarValue(call.ArgAt<object?>(1))));
        return factory;
    }

    [Theory]
    [InlineData("street")]
    [InlineData("Street")]
    [InlineData("STREET")]
    [InlineData("address")]
    [InlineData("city")]
    [InlineData("state")]
    [InlineData("zipcode")]
    [InlineData("zip")]
    [InlineData("plus4")]
    [InlineData("deliveryline1")]
    [InlineData("rawaddress")]
    [InlineData("inputaddress")]
    [InlineData("validatedaddress")]
    public void Enrich_KnownPiiProperty_MasksValue(string propertyName)
    {
        var enricher = new PiiSanitizingEnricher();
        var logEvent = MakeEvent((propertyName, "123 Main St"));

        enricher.Enrich(logEvent, MakePropertyFactory());

        var maskedValue = ((ScalarValue)logEvent.Properties[propertyName]).Value as string;
        Assert.Equal("***", maskedValue);
    }

    [Theory]
    [InlineData("correlationId")]
    [InlineData("statusCode")]
    [InlineData("endpoint")]
    [InlineData("traceId")]
    public void Enrich_NonPiiProperty_PreservesValue(string propertyName)
    {
        var enricher = new PiiSanitizingEnricher();
        var logEvent = MakeEvent((propertyName, "original-value"));

        enricher.Enrich(logEvent, MakePropertyFactory());

        var value = ((ScalarValue)logEvent.Properties[propertyName]).Value as string;
        Assert.Equal("original-value", value);
    }

    [Fact]
    public void Enrich_NullLogEvent_Throws()
    {
        var enricher = new PiiSanitizingEnricher();
        Assert.Throws<ArgumentNullException>(() => enricher.Enrich(null!, MakePropertyFactory()));
    }
}
