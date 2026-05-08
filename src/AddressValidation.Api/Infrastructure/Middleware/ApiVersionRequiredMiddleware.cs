using System.Text.Json;
using Microsoft.AspNetCore.Mvc;

namespace AddressValidation.Api.Infrastructure.Middleware;

/// <summary>
/// Enforces the presence of the <c>Api-Version</c> header on all
/// <c>/api/</c> paths. Returns HTTP 400 with an RFC 7807 ProblemDetails
/// body when the header is absent or empty (NFR-015 / issue #99).
/// Health, metrics, and Swagger paths are excluded.
/// </summary>
public sealed class ApiVersionRequiredMiddleware
{
    private static readonly string[] ExcludedPrefixes =
    [
        "/health",
        "/metrics",
        "/swagger",
        "/favicon",
    ];

    private readonly RequestDelegate _next;

    public ApiVersionRequiredMiddleware(RequestDelegate next)
    {
        ArgumentNullException.ThrowIfNull(next);
        _next = next;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        var path = context.Request.Path.Value ?? string.Empty;

        if (path.StartsWith("/api/", StringComparison.OrdinalIgnoreCase) &&
            !ExcludedPrefixes.Any(p => path.StartsWith(p, StringComparison.OrdinalIgnoreCase)))
        {
            var apiVersion = context.Request.Headers["Api-Version"].FirstOrDefault();
            if (string.IsNullOrWhiteSpace(apiVersion))
            {
                context.Response.StatusCode = StatusCodes.Status400BadRequest;
                context.Response.ContentType = "application/problem+json";

                var correlationId = context.Items.TryGetValue("CorrelationId", out var cid) ? cid?.ToString() : null;
                var problem = new ProblemDetails
                {
                    Type = "https://tools.ietf.org/html/rfc7231#section-6.5.1",
                    Title = "Missing required header.",
                    Status = StatusCodes.Status400BadRequest,
                    Detail = "The 'Api-Version' header is required for all API requests.",
                    Instance = context.Request.Path,
                };
                problem.Extensions["traceId"] = context.TraceIdentifier;
                if (correlationId is not null)
                    problem.Extensions["correlationId"] = correlationId;

                var json = JsonSerializer.Serialize(problem, new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.CamelCase });
                await context.Response.WriteAsync(json);
                return;
            }
        }

        await _next(context);
    }
}
