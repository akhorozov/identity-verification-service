using AddressValidation.Api.Infrastructure.Authentication;

namespace AddressValidation.Api.Features.Cache;

/// <summary>
/// Minimal API endpoint for GET /api/cache/stats.
/// SRS Ref: FR-003, Section 9.3.3 — any valid API key may read stats.
/// Issue #74.
/// </summary>
public static class CacheStatsEndpoint
{
    /// <summary>
    /// Maps the GET /api/cache/stats route.
    /// </summary>
    public static IEndpointRouteBuilder MapCacheStats(this IEndpointRouteBuilder app)
    {
        app.MapGet("/api/cache/stats", HandleAsync)
            .WithName("GetCacheStats")
            .WithSummary("Returns hit/miss ratios and entry counts for all cache layers.")
            .WithTags("Cache Management")
            .Produces<CacheStatsResponse>(StatusCodes.Status200OK)
            .Produces(StatusCodes.Status401Unauthorized)
            .RequireAuthorization(ApiKeyAuthorizationPolicy.ReadOnly);

        return app;
    }

    private static async Task<IResult> HandleAsync(
        CacheStatsHandler handler,
        CancellationToken cancellationToken)
    {
        var result = await handler.HandleAsync(cancellationToken);
        return Results.Ok(result);
    }
}
