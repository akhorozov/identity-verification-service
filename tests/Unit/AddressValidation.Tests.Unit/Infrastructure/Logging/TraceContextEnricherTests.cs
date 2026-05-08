namespace AddressValidation.Tests.Unit.Infrastructure.Logging;

using System.Diagnostics;
using AddressValidation.Api.Infrastructure.Logging;
using NSubstitute;
using Serilog.Core;
using Serilog.Events;
using Serilog.Parsing;
using Xunit;

public class TraceContextEnricherTests
{
    private static ILogEventPropertyFactory MakeFactory()
    {
        var factory = Substitute.For<ILogEventPropertyFactory>();
        factory.CreateProperty(Arg.Any<string>(), Arg.Any<object>(), Arg.Any<bool>())
               .Returns(call => new LogEventProperty(
                   call.ArgAt<string>(0),
                   new ScalarValue(call.ArgAt<object>(1))));
        return factory;
    }

    private static LogEvent MakeEvent()
        => new(
            DateTimeOffset.UtcNow,
            LogEventLevel.Information,
            exception: null,
            new MessageTemplate("test", []),
            []);

    [Fact]
    public void Enrich_WhenNoCurrentActivity_DoesNotAddProperties()
    {
        // Arrange — ensure no ambient activity
        Assert.Null(Activity.Current);
        var enricher = new TraceContextEnricher();
        var logEvent = MakeEvent();
        var factory = MakeFactory();

        // Act
        enricher.Enrich(logEvent, factory);

        // Assert — no properties added
        Assert.Empty(logEvent.Properties);
    }

    [Fact]
    public void Enrich_WhenActivityIsActive_AddsTraceIdSpanIdAndTraceFlags()
    {
        // Arrange
        using var source = new ActivitySource("test-source");
        using var listener = new ActivityListener
        {
            ShouldListenTo = _ => true,
            Sample = (ref ActivityCreationOptions<ActivityContext> _) => ActivitySamplingResult.AllData,
        };
        ActivitySource.AddActivityListener(listener);

        using var activity = source.StartActivity("test-span");
        Assert.NotNull(activity);

        var enricher = new TraceContextEnricher();
        var logEvent = MakeEvent();
        var factory = MakeFactory();

        // Act
        enricher.Enrich(logEvent, factory);

        // Assert
        Assert.True(logEvent.Properties.ContainsKey("TraceId"));
        Assert.True(logEvent.Properties.ContainsKey("SpanId"));
        Assert.True(logEvent.Properties.ContainsKey("TraceFlags"));

        var traceId = Assert.IsType<ScalarValue>(logEvent.Properties["TraceId"]);
        Assert.Equal(activity.TraceId.ToString(), traceId.Value);

        var spanId = Assert.IsType<ScalarValue>(logEvent.Properties["SpanId"]);
        Assert.Equal(activity.SpanId.ToString(), spanId.Value);
    }

    [Fact]
    public void Enrich_WhenPropertyAlreadyPresent_DoesNotOverwrite()
    {
        // Arrange
        using var source = new ActivitySource("test-source-2");
        using var listener = new ActivityListener
        {
            ShouldListenTo = _ => true,
            Sample = (ref ActivityCreationOptions<ActivityContext> _) => ActivitySamplingResult.AllData,
        };
        ActivitySource.AddActivityListener(listener);

        using var activity = source.StartActivity("test-span-2");
        Assert.NotNull(activity);

        var enricher = new TraceContextEnricher();
        var logEvent = MakeEvent();
        var factory = MakeFactory();

        // Pre-populate TraceId so AddPropertyIfAbsent should not overwrite
        logEvent.AddOrUpdateProperty(new LogEventProperty("TraceId", new ScalarValue("existing-trace")));

        // Act
        enricher.Enrich(logEvent, factory);

        // Assert — pre-existing value preserved
        var traceId = Assert.IsType<ScalarValue>(logEvent.Properties["TraceId"]);
        Assert.Equal("existing-trace", traceId.Value);
    }
}
