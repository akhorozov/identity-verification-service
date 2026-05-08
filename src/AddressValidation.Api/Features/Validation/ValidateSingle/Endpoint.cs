namespace AddressValidation.Api.Features.Validation.ValidateSingle;

using FluentValidation;
using Microsoft.AspNetCore.Mvc;

/// <summary>
/// Minimal API endpoint for single address validation (FR-001).
/// POST /api/v1/addresses/validate
///
/// Response headers:
///   X-Cache-Source : L1 | L2 | PROVIDER
///   X-Cache-Stale  : true  (only when circuit-breaker returns stale cached data)
///
/// SRS Ref: FR-001, Section 9.3.1
/// </summary>
public static class ValidateSingleEndpoint
{
    /// <summary>Maps the endpoint onto the provided <see cref="IEndpointRouteBuilder"/>.</summary>
    public static IEndpointRouteBuilder MapValidateSingle(this IEndpointRouteBuilder routes)
    {
        routes.MapPost("/api/v1/addresses/validate", HandleAsync)
            .WithName("ValidateSingleAddress")
            .WithSummary("Validate a single US address")
            .WithDescription(
                "Validates a US postal address via the cache hierarchy (L1 → L2 → Provider). " +
                "Returns a standardised address or 404 for undeliverable addresses.")
            .WithTags("Validation")
            .Produces<ValidateSingleResponse>(StatusCodes.Status200OK)
            .Produces<ProblemDetails>(StatusCodes.Status400BadRequest)
            .Produces<ProblemDetails>(StatusCodes.Status404NotFound)
            .Produces<ProblemDetails>(StatusCodes.Status429TooManyRequests)
            .Produces<ProblemDetails>(StatusCodes.Status500InternalServerError);

        return routes;
    }

    private static async Task<IResult> HandleAsync(
        [FromBody] ValidateSingleRequest request,
        ValidateSingleHandler handler,
        IValidator<ValidateSingleRequest> validator,
        HttpContext httpContext,
        CancellationToken cancellationToken)
    {
        // Validate request
        var validation = await validator.ValidateAsync(request, cancellationToken);
        if (!validation.IsValid)
        {
            return Results.ValidationProblem(
                validation.ToDictionary(),
                statusCode: StatusCodes.Status400BadRequest,
                title: "Validation failed",
                type: "https://tools.ietf.org/html/rfc7807");
        }

        var correlationId = httpContext.Items["CorrelationId"]?.ToString();

        var result = await handler.HandleAsync(request, correlationId, cancellationToken);

        // Undeliverable address (DPV N or provider no-match) → RFC 7807 404
        if (result is null)
        {
            return Results.Problem(
                detail: "The address could not be validated or is undeliverable.",
                statusCode: StatusCodes.Status404NotFound,
                title: "Address Not Found",
                type: "https://tools.ietf.org/html/rfc7807");
        }

        // Set response headers
        httpContext.Response.Headers["X-Cache-Source"] = result.CacheSource;

        if (result.IsStale)
            httpContext.Response.Headers["X-Cache-Stale"] = "true";

        return Results.Ok(result.Response);
    }
}
