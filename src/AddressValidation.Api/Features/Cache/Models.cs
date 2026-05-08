using System.Text.Json.Serialization;

namespace AddressValidation.Api.Features.Cache;

/// <summary>
/// Statistics snapshot for a single cache layer.
/// </summary>
public sealed record LayerStatsResponse(
    [property: JsonPropertyName("layer")] string Layer,
    [property: JsonPropertyName("entryCount")] long EntryCount,
    [property: JsonPropertyName("hitCount")] long HitCount,
    [property: JsonPropertyName("missCount")] long MissCount,
    [property: JsonPropertyName("hitRatio")] double HitRatio);

/// <summary>
/// Response body for GET /api/cache/stats.
/// </summary>
public sealed record CacheStatsResponse(
    [property: JsonPropertyName("generatedAt")] DateTimeOffset GeneratedAt,
    [property: JsonPropertyName("layers")] IReadOnlyList<LayerStatsResponse> Layers);

/// <summary>
/// Response body for DELETE /api/cache/{key} (204 has no body; used internally).
/// </summary>
public sealed record InvalidateCacheResult(bool Found, string CacheKey, IReadOnlyList<string> InvalidatedLayers);

/// <summary>
/// Response body for DELETE /api/cache/flush.
/// </summary>
public sealed record FlushCacheResult(long EntriesRemoved, IReadOnlyList<string> FlushedLayers);
