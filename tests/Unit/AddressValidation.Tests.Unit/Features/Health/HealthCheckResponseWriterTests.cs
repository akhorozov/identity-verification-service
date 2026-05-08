namespace AddressValidation.Tests.Unit.Features.Health;

using System.Text.Json;
using AddressValidation.Api.Features.Health;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Xunit;

/// <summary>
/// Unit tests for <see cref="HealthCheckResponseWriter"/>.
/// </summary>
public class HealthCheckResponseWriterTests
{
    [Fact]
    public async Task WriteResponse_WhenAllChecksHealthy_WritesHealthyStatusJson()
    {
        var context = new DefaultHttpContext();
        context.Response.Body = new MemoryStream();

        var report = new HealthReport(
            entries: new Dictionary<string, HealthReportEntry>
            {
                ["self"] = new(HealthStatus.Healthy, "Process is alive.", TimeSpan.FromMilliseconds(1), null, null),
            },
            status: HealthStatus.Healthy,
            totalDuration: TimeSpan.FromMilliseconds(5));

        await HealthCheckResponseWriter.WriteResponse(context, report);

        context.Response.Body.Position = 0;
        var json = await new StreamReader(context.Response.Body).ReadToEndAsync();

        using var doc = JsonDocument.Parse(json);
        Assert.Equal("Healthy", doc.RootElement.GetProperty("status").GetString());
        Assert.Equal(1, doc.RootElement.GetProperty("checks").GetArrayLength());
    }

    [Fact]
    public async Task WriteResponse_WhenCheckUnhealthy_WritesUnhealthyStatusJson()
    {
        var context = new DefaultHttpContext();
        context.Response.Body = new MemoryStream();

        var report = new HealthReport(
            entries: new Dictionary<string, HealthReportEntry>
            {
                ["redis"] = new(HealthStatus.Unhealthy, "Redis unreachable.", TimeSpan.FromMilliseconds(100),
                    new InvalidOperationException("timeout"), null),
            },
            status: HealthStatus.Unhealthy,
            totalDuration: TimeSpan.FromMilliseconds(100));

        await HealthCheckResponseWriter.WriteResponse(context, report);

        context.Response.Body.Position = 0;
        var json = await new StreamReader(context.Response.Body).ReadToEndAsync();

        using var doc = JsonDocument.Parse(json);
        Assert.Equal("Unhealthy", doc.RootElement.GetProperty("status").GetString());
        var check = doc.RootElement.GetProperty("checks").EnumerateArray().First();
        Assert.Equal("Unhealthy", check.GetProperty("status").GetString());
        Assert.Equal("Redis unreachable.", check.GetProperty("description").GetString());
        Assert.Equal("timeout", check.GetProperty("exception").GetString());
    }

    [Fact]
    public async Task WriteResponse_SetsContentTypeApplicationJson()
    {
        var context = new DefaultHttpContext();
        context.Response.Body = new MemoryStream();

        var report = new HealthReport(
            entries: new Dictionary<string, HealthReportEntry>(),
            status: HealthStatus.Healthy,
            totalDuration: TimeSpan.Zero);

        await HealthCheckResponseWriter.WriteResponse(context, report);

        Assert.Equal("application/json", context.Response.ContentType);
    }
}
