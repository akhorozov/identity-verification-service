namespace AddressValidation.Api.Features.Validation.ValidateBatch;

using System.Text.Json;
using FluentValidation;
using Microsoft.AspNetCore.Mvc;

/// <summary>
/// Minimal API endpoint for batch address validation (FR-002).
/// POST /api/addresses/validate/batch
///
/// Response codes:
///   200 OK          — all addresses validated successfully
///   207 Multi-Status — at least one address failed or is undeliverable
///   400 Bad Request  — request-level validation error (RFC 7807)
///
/// Response headers:
///   X-Batch-Summary : JSON-serialised summary object (total, validated, failed, cacheHits, cacheMisses, durationMs)
///
/// SRS Ref: FR-002, Section 9.3.2
/// </summary>
public static class ValidateBatchEndpoint
{
    private static readonly JsonSerializerOptions SummaryJsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented = false,
    };

    /// <summary>Maps the endpoint onto the provided <see cref="IEndpointRouteBuilder"/>.</summary>
    public static IEndpointRouteBuilder MapValidateBatch(this IEndpointRouteBuilder routes)
    {
        routes.MapPost("/api/addresses/validate/batch", HandleAsync)
            .WithName("ValidateBatchAddresses")
            .WithSummary("Validate a batch of US addresses (max 100)")
            .WithDescription(
                "Validates up to 100 US postal addresses in a single request via the cache hierarchy " +
                "(parallel L1 → L2 → Provider). Requires Api-Version: 1.0 request header. " +
                "Returns 200 when all succeed, 207 when at least one fails.")
            .WithTags("Validation")
            .Produces<ValidateBatchResponse>(StatusCodes.Status200OK)
            .Produces<ValidateBatchResponse>(StatusCodes.Status207MultiStatus)
            .Produces<ProblemDetails>(StatusCodes.Status400BadRequest)
            .Produces<ProblemDetails>(StatusCodes.Status429TooManyRequests)
            .Produces<ProblemDetails>(StatusCodes.Status500InternalServerError);

        return routes;
    }

    private static async Task<IResult> HandleAsync(
        [FromBody] ValidateBatchRequest request,
        ValidateBatchHandler handler,
        IValidator<ValidateBatchRequest> validator,
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
                title: "Batch validation request is invalid.",
                type: "https://tools.ietf.org/html/rfc7807");
        }

        var correlationId = httpContext.Items["CorrelationId"]?.ToString();

        var response = await handler.HandleAsync(request, correlationId, cancellationToken);

        // Set X-Batch-Summary header
        var summaryJson = JsonSerializer.Serialize(response.Summary, SummaryJsonOptions);
        httpContext.Response.Headers["X-Batch-Summary"] = summaryJson;

        // 207 Multi-Status when at least one address failed
        if (response.Summary.Failed > 0)
        {
            httpContext.Response.StatusCode = StatusCodes.Status207MultiStatus;
            return Results.Json(response, statusCode: StatusCodes.Status207MultiStatus);
        }

        return Results.Ok(response);
    }
}
