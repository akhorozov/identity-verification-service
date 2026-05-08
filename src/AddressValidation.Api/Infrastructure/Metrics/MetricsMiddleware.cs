namespace AddressValidation.Api.Infrastructure.Metrics;

using System.Diagnostics;

/// <summary>
/// ASP.NET Core middleware that records <c>address_validation_requests_total</c> and
/// <c>address_validation_duration_seconds</c> for every request to a validation endpoint.
/// Only instruments <c>/api/addresses</c> paths; all other paths pass through untouched.
/// SRS Ref: FR-006
/// </summary>
public sealed class MetricsMiddleware
{
    private readonly RequestDelegate _next;
    private readonly AppMetrics _metrics;

    /// <summary>Initializes a new instance of <see cref="MetricsMiddleware"/>.</summary>
    public MetricsMiddleware(RequestDelegate next, AppMetrics metrics)
    {
        ArgumentNullException.ThrowIfNull(next);
        ArgumentNullException.ThrowIfNull(metrics);
        _next = next;
        _metrics = metrics;
    }

    /// <summary>Invokes the middleware and records metrics for validation endpoints.</summary>
    public async Task InvokeAsync(HttpContext context)
    {
        var path = context.Request.Path.Value ?? string.Empty;

        // Only instrument validation endpoints
        if (!path.StartsWith("/api/addresses", StringComparison.OrdinalIgnoreCase))
        {
            await _next(context);
            return;
        }

        var sw = Stopwatch.StartNew();

        try
        {
            await _next(context);
        }
        finally
        {
            sw.Stop();

            var endpoint = ResolveEndpoint(path);
            var status = context.Response.StatusCode.ToString();
            var apiVersion = context.Request.Headers["Api-Version"].FirstOrDefault() ?? "1.0";
            // Cache source is set by the handler on the response headers; default to "UNKNOWN"
            var cacheSource = context.Response.Headers["X-Cache-Source"].FirstOrDefault() ?? "UNKNOWN";

            _metrics.ValidationRequestsTotal
                .WithLabels(endpoint, status, apiVersion)
                .Inc();

            _metrics.ValidationDurationSeconds
                .WithLabels(endpoint, cacheSource)
                .Observe(sw.Elapsed.TotalSeconds);
        }
    }

    private static string ResolveEndpoint(string path) =>
        path.Contains("/batch", StringComparison.OrdinalIgnoreCase)
            ? "validate_batch"
            : "validate_single";
}
