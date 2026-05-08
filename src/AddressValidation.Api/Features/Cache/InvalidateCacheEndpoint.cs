using AddressValidation.Api.Infrastructure.Authentication;

namespace AddressValidation.Api.Features.Cache;

/// <summary>
/// Minimal API endpoint for DELETE /api/cache/{key}.
/// SRS Ref: FR-003, Section 9.3.4 — admin role required. Issue #75.
/// </summary>
public static class InvalidateCacheEndpoint
{
    /// <summary>
    /// Maps the DELETE /api/cache/{key} route.
    /// </summary>
    public static IEndpointRouteBuilder MapInvalidateCache(this IEndpointRouteBuilder app)
    {
        app.MapDelete("/api/cache/{key}", HandleAsync)
            .WithName("InvalidateCacheKey")
            .WithSummary("Removes a specific cache entry from Redis and marks it stale in CosmosDB.")
            .WithTags("Cache Management")
            .Produces(StatusCodes.Status204NoContent)
            .Produces(StatusCodes.Status401Unauthorized)
            .Produces(StatusCodes.Status403Forbidden)
            .Produces(StatusCodes.Status404NotFound)
            .RequireAuthorization(ApiKeyAuthorizationPolicy.Admin);

        return app;
    }

    private static async Task<IResult> HandleAsync(
        string key,
        InvalidateCacheHandler handler,
        CancellationToken cancellationToken)
    {
        var result = await handler.HandleAsync(key, cancellationToken);

        if (!result.Found)
        {
            return Results.Problem(
                title: "Cache Key Not Found",
                detail: $"No cache entry exists for key '{key}'.",
                statusCode: StatusCodes.Status404NotFound,
                type: "https://tools.ietf.org/html/rfc7807");
        }

        return Results.NoContent();
    }
}
