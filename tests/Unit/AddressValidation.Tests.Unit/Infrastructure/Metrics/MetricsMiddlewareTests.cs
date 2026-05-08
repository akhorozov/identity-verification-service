namespace AddressValidation.Tests.Unit.Infrastructure.Metrics;

using AddressValidation.Api.Infrastructure.Metrics;
using Microsoft.AspNetCore.Http;
using Xunit;

/// <summary>
/// Unit tests for <see cref="MetricsMiddleware"/>.
/// Verifies that validation requests increment the counter and record the histogram.
/// </summary>
public class MetricsMiddlewareTests
{
    private static DefaultHttpContext MakeContext(string path, int statusCode = 200, string apiVersion = "1.0", string cacheSource = "L1")
    {
        var context = new DefaultHttpContext();
        context.Request.Path = path;
        context.Request.Headers["Api-Version"] = apiVersion;
        // Simulate the handler setting the cache source header
        context.Response.OnStarting(() =>
        {
            context.Response.StatusCode = statusCode;
            return Task.CompletedTask;
        });
        context.Response.Headers["X-Cache-Source"] = cacheSource;
        context.Response.StatusCode = statusCode;
        return context;
    }

    [Fact]
    public async Task InvokeAsync_ValidationPath_IncrementsCounter()
    {
        var metrics = new AppMetrics();
        var middleware = new MetricsMiddleware(_ => Task.CompletedTask, metrics);
        var context = MakeContext("/api/addresses/validate", 200, "1.0", "L1");

        await middleware.InvokeAsync(context);

        Assert.True(metrics.ValidationRequestsTotal
            .WithLabels("validate_single", "200", "1.0").Value >= 1);
    }

    [Fact]
    public async Task InvokeAsync_BatchPath_UsesValidateBatchLabel()
    {
        var metrics = new AppMetrics();
        var middleware = new MetricsMiddleware(_ => Task.CompletedTask, metrics);
        var context = MakeContext("/api/addresses/validate/batch", 200, "1.0", "PROVIDER");

        await middleware.InvokeAsync(context);

        Assert.True(metrics.ValidationRequestsTotal
            .WithLabels("validate_batch", "200", "1.0").Value >= 1);
    }

    [Fact]
    public async Task InvokeAsync_NonValidationPath_DoesNotIncrementCounter()
    {
        var metrics = new AppMetrics();
        var middleware = new MetricsMiddleware(_ => Task.CompletedTask, metrics);
        var context = MakeContext("/health/live");

        // Capture baseline before the call (Prometheus counters are process-wide)
        var before = metrics.ValidationRequestsTotal
            .WithLabels("validate_single", "200", "1.0").Value;

        await middleware.InvokeAsync(context);

        // Counter must not have increased — non-api path should not be instrumented
        var after = metrics.ValidationRequestsTotal
            .WithLabels("validate_single", "200", "1.0").Value;
        Assert.Equal(before, after);
    }

    [Fact]
    public async Task InvokeAsync_ValidationPath_ObservesHistogramDuration()
    {
        var metrics = new AppMetrics();
        var middleware = new MetricsMiddleware(_ => Task.CompletedTask, metrics);
        var context = MakeContext("/api/addresses/validate", 200, "1.0", "PROVIDER");

        // Should not throw — validates the histogram observe path
        await middleware.InvokeAsync(context);
    }

    [Fact]
    public async Task InvokeAsync_WhenNextThrows_StillRecordsMetrics()
    {
        var metrics = new AppMetrics();
        RequestDelegate throwingNext = _ => throw new InvalidOperationException("downstream failure");
        var middleware = new MetricsMiddleware(throwingNext, metrics);
        var context = MakeContext("/api/addresses/validate", 500, "1.0", "UNKNOWN");

        await Assert.ThrowsAsync<InvalidOperationException>(() => middleware.InvokeAsync(context));

        // Counter should still have been incremented in the finally block
        Assert.True(metrics.ValidationRequestsTotal
            .WithLabels("validate_single", "500", "1.0").Value >= 1);
    }
}
