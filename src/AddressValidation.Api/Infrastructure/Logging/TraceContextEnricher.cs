namespace AddressValidation.Api.Infrastructure.Logging;

using System.Diagnostics;
using Serilog.Core;
using Serilog.Events;

/// <summary>
/// Serilog enricher that attaches the current OpenTelemetry trace context
/// (<c>TraceId</c>, <c>SpanId</c>, <c>TraceFlags</c>) to every log event so
/// that Serilog sinks (e.g. Seq, Application Insights) can correlate logs
/// with distributed traces (T12 / issue #105).
/// </summary>
public sealed class TraceContextEnricher : ILogEventEnricher
{
    /// <inheritdoc />
    public void Enrich(LogEvent logEvent, ILogEventPropertyFactory propertyFactory)
    {
        var activity = Activity.Current;
        if (activity is null)
            return;

        logEvent.AddPropertyIfAbsent(
            propertyFactory.CreateProperty("TraceId", activity.TraceId.ToString()));

        logEvent.AddPropertyIfAbsent(
            propertyFactory.CreateProperty("SpanId", activity.SpanId.ToString()));

        logEvent.AddPropertyIfAbsent(
            propertyFactory.CreateProperty("TraceFlags", activity.ActivityTraceFlags.ToString()));
    }
}
