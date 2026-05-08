namespace AddressValidation.Tests.Unit.Features.Cache;

using AddressValidation.Api.Domain.Events;
using AddressValidation.Api.Features.Cache;
using AddressValidation.Api.Infrastructure.Services.Audit;
using AddressValidation.Api.Infrastructure.Services.Caching;
using Microsoft.Extensions.Logging;
using NSubstitute;
using Xunit;

/// <summary>
/// Unit tests for <see cref="InvalidateCacheHandler"/>.
/// </summary>
public class InvalidateCacheHandlerTests
{
    private readonly ICacheManagementService _redisLayer;
    private readonly ICacheManagementService _cosmosLayer;
    private readonly IAuditEventStore _audit;
    private readonly InvalidateCacheHandler _sut;

    public InvalidateCacheHandlerTests()
    {
        _redisLayer = Substitute.For<ICacheManagementService>();
        _redisLayer.LayerName.Returns("L1-Redis");

        _cosmosLayer = Substitute.For<ICacheManagementService>();
        _cosmosLayer.LayerName.Returns("L2-CosmosDB");

        _audit = Substitute.For<IAuditEventStore>();

        _sut = new InvalidateCacheHandler(
            [_redisLayer, _cosmosLayer],
            _audit,
            Substitute.For<ILogger<InvalidateCacheHandler>>());
    }

    [Fact]
    public async Task HandleAsync_WhenKeyExistsInBothLayers_ReturnsTrueWithBothLayers()
    {
        _redisLayer.InvalidateAsync("addr:v1:abc", Arg.Any<CancellationToken>()).Returns(true);
        _cosmosLayer.InvalidateAsync("addr:v1:abc", Arg.Any<CancellationToken>()).Returns(true);

        var result = await _sut.HandleAsync("addr:v1:abc");

        Assert.True(result.Found);
        Assert.Equal("addr:v1:abc", result.CacheKey);
        Assert.Contains("L1-Redis", result.InvalidatedLayers);
        Assert.Contains("L2-CosmosDB", result.InvalidatedLayers);
    }

    [Fact]
    public async Task HandleAsync_WhenKeyExistsOnlyInRedis_ReturnsTrueWithRedisLayer()
    {
        _redisLayer.InvalidateAsync(Arg.Any<string>(), Arg.Any<CancellationToken>()).Returns(true);
        _cosmosLayer.InvalidateAsync(Arg.Any<string>(), Arg.Any<CancellationToken>()).Returns(false);

        var result = await _sut.HandleAsync("addr:v1:abc");

        Assert.True(result.Found);
        Assert.Equal(["L1-Redis"], result.InvalidatedLayers);
    }

    [Fact]
    public async Task HandleAsync_WhenKeyNotFoundInAnyLayer_ReturnsFalse()
    {
        _redisLayer.InvalidateAsync(Arg.Any<string>(), Arg.Any<CancellationToken>()).Returns(false);
        _cosmosLayer.InvalidateAsync(Arg.Any<string>(), Arg.Any<CancellationToken>()).Returns(false);

        var result = await _sut.HandleAsync("addr:v1:notfound");

        Assert.False(result.Found);
        Assert.Empty(result.InvalidatedLayers);
    }

    [Fact]
    public async Task HandleAsync_WhenKeyFound_EmitsCacheEntryInvalidatedEvent()
    {
        _redisLayer.InvalidateAsync(Arg.Any<string>(), Arg.Any<CancellationToken>()).Returns(true);
        _cosmosLayer.InvalidateAsync(Arg.Any<string>(), Arg.Any<CancellationToken>()).Returns(false);

        await _sut.HandleAsync("addr:v1:abc");

        await _audit.Received(1).AppendAsync(
            Arg.Is<CacheEntryInvalidated>(e => e.CacheKey == "addr:v1:abc"),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task HandleAsync_WhenKeyNotFound_DoesNotEmitAuditEvent()
    {
        _redisLayer.InvalidateAsync(Arg.Any<string>(), Arg.Any<CancellationToken>()).Returns(false);
        _cosmosLayer.InvalidateAsync(Arg.Any<string>(), Arg.Any<CancellationToken>()).Returns(false);

        await _sut.HandleAsync("addr:v1:notfound");

        await _audit.DidNotReceive().AppendAsync(Arg.Any<DomainEvent>(), Arg.Any<CancellationToken>());
    }
}
