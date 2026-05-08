namespace AddressValidation.Tests.Unit.Features.Cache;

using AddressValidation.Api.Domain.Events;
using AddressValidation.Api.Features.Cache;
using AddressValidation.Api.Infrastructure.Services.Audit;
using AddressValidation.Api.Infrastructure.Services.Caching;
using Microsoft.Extensions.Logging;
using NSubstitute;
using Xunit;

/// <summary>
/// Unit tests for <see cref="CacheStatsHandler"/>.
/// </summary>
public class CacheStatsHandlerTests
{
    private readonly ICacheManagementService _redisLayer;
    private readonly ICacheManagementService _cosmosLayer;
    private readonly CacheStatsHandler _sut;

    public CacheStatsHandlerTests()
    {
        _redisLayer = Substitute.For<ICacheManagementService>();
        _redisLayer.LayerName.Returns("L1-Redis");

        _cosmosLayer = Substitute.For<ICacheManagementService>();
        _cosmosLayer.LayerName.Returns("L2-CosmosDB");

        _sut = new CacheStatsHandler(
            [_redisLayer, _cosmosLayer],
            Substitute.For<ILogger<CacheStatsHandler>>());
    }

    [Fact]
    public async Task HandleAsync_WhenBothLayersRespond_ReturnsCombinedStats()
    {
        _redisLayer.GetStatsAsync(Arg.Any<CancellationToken>())
            .Returns(new CacheLayerStats("L1-Redis", 10, 80, 20));
        _cosmosLayer.GetStatsAsync(Arg.Any<CancellationToken>())
            .Returns(new CacheLayerStats("L2-CosmosDB", 50, 0, 0));

        var result = await _sut.HandleAsync();

        Assert.Equal(2, result.Layers.Count);
        var redis = result.Layers.Single(l => l.Layer == "L1-Redis");
        Assert.Equal(10, redis.EntryCount);
        Assert.Equal(80, redis.HitCount);
        Assert.Equal(20, redis.MissCount);
        Assert.Equal(0.8, redis.HitRatio);
    }

    [Fact]
    public async Task HandleAsync_WhenNoHitsOrMisses_ReturnsZeroHitRatio()
    {
        _redisLayer.GetStatsAsync(Arg.Any<CancellationToken>())
            .Returns(new CacheLayerStats("L1-Redis", 0, 0, 0));
        _cosmosLayer.GetStatsAsync(Arg.Any<CancellationToken>())
            .Returns(new CacheLayerStats("L2-CosmosDB", 0, 0, 0));

        var result = await _sut.HandleAsync();

        Assert.All(result.Layers, l => Assert.Equal(0.0, l.HitRatio));
    }

    [Fact]
    public async Task HandleAsync_AlwaysSetsGeneratedAt()
    {
        _redisLayer.GetStatsAsync(Arg.Any<CancellationToken>())
            .Returns(new CacheLayerStats("L1-Redis", 0, 0, 0));
        _cosmosLayer.GetStatsAsync(Arg.Any<CancellationToken>())
            .Returns(new CacheLayerStats("L2-CosmosDB", 0, 0, 0));

        var before = DateTimeOffset.UtcNow;
        var result = await _sut.HandleAsync();
        var after = DateTimeOffset.UtcNow;

        Assert.InRange(result.GeneratedAt, before, after);
    }
}
