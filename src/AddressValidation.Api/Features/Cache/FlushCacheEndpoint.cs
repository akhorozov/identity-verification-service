using AddressValidation.Api.Infrastructure.Authentication;

namespace AddressValidation.Api.Features.Cache;

/// <summary>
/// Minimal API endpoint for DELETE /api/cache/flush.
/// SRS Ref: FR-003, Section 9.3.5 — admin role required; flushes L1/Redis only. Issue #77.
/// </summary>
public static class FlushCacheEndpoint
{
    /// <summary>
    /// Maps the DELETE /api/cache/flush route.
    /// </summary>
    public static IEndpointRouteBuilder MapFlushCache(this IEndpointRouteBuilder app)
    {
        // IMPORTANT: register /flush BEFORE /{key} so the literal segment wins routing.
        app.MapDelete("/api/cache/flush", HandleAsync)
            .WithName("FlushCache")
            .WithSummary("Flushes the L1 Redis cache. CosmosDB (L2) data is retained.")
            .WithTags("Cache Management")
            .Produces(StatusCodes.Status204NoContent)
            .Produces(StatusCodes.Status401Unauthorized)
            .Produces(StatusCodes.Status403Forbidden)
            .RequireAuthorization(ApiKeyAuthorizationPolicy.Admin);

        return app;
    }

    private static async Task<IResult> HandleAsync(
        FlushCacheHandler handler,
        CancellationToken cancellationToken)
    {
        await handler.HandleAsync(cancellationToken);
        return Results.NoContent();
    }
}
