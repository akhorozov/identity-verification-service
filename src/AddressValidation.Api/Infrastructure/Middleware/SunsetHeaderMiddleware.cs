namespace AddressValidation.Api.Infrastructure.Middleware;

/// <summary>
/// Adds a <c>Sunset</c> header (RFC 8594) to API responses for versions
/// that are scheduled for deprecation. The deprecation schedule is read
/// from <c>Security:ApiSunset:{version}</c> in configuration (NFR / issue #101).
/// </summary>
/// <remarks>
/// Example appsettings entry:
/// <code>
/// "Security": {
///   "ApiSunset": {
///     "1.0": "2026-12-31T00:00:00Z"
///   }
/// }
/// </code>
/// </remarks>
public sealed class SunsetHeaderMiddleware
{
    private readonly RequestDelegate _next;
    private readonly IConfiguration _configuration;

    public SunsetHeaderMiddleware(RequestDelegate next, IConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(next);
        ArgumentNullException.ThrowIfNull(configuration);
        _next = next;
        _configuration = configuration;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        var path = context.Request.Path.Value ?? string.Empty;
        if (path.StartsWith("/api/", StringComparison.OrdinalIgnoreCase))
        {
            var apiVersion = context.Request.Headers["Api-Version"].FirstOrDefault() ?? "1.0";

            var sunsetDate = _configuration[$"Security:ApiSunset:{apiVersion}"];
            if (!string.IsNullOrWhiteSpace(sunsetDate) &&
                DateTimeOffset.TryParse(sunsetDate, out var sunset))
            {
                // Set headers before calling next so they are always present;
                // the Api-Version is known from the request so we don't need OnStarting.
                context.Response.Headers["Sunset"] = sunset.ToString("R");
                context.Response.Headers["Deprecation"] = "true";
                context.Response.Headers["Link"] = "</api/v2>; rel=\"successor-version\"";
            }
        }

        await _next(context);
    }
}
