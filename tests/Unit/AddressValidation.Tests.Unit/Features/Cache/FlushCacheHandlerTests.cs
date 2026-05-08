namespace AddressValidation.Tests.Unit.Features.Cache;

using AddressValidation.Api.Domain.Events;
using AddressValidation.Api.Features.Cache;
using AddressValidation.Api.Infrastructure.Services.Audit;
using AddressValidation.Api.Infrastructure.Services.Caching;
using Microsoft.Extensions.Logging;
using NSubstitute;
using Xunit;

/// <summary>
/// Unit tests for <see cref="FlushCacheHandler"/>.
/// </summary>
public class FlushCacheHandlerTests
{
    private readonly ICacheManagementService _redisLayer;
    private readonly ICacheManagementService _cosmosLayer;
    private readonly IAuditEventStore _audit;
    private readonly FlushCacheHandler _sut;

    public FlushCacheHandlerTests()
    {
        _redisLayer = Substitute.For<ICacheManagementService>();
        _redisLayer.LayerName.Returns("L1-Redis");

        _cosmosLayer = Substitute.For<ICacheManagementService>();
        _cosmosLayer.LayerName.Returns("L2-CosmosDB");

        _audit = Substitute.For<IAuditEventStore>();

        _sut = new FlushCacheHandler(
            [_redisLayer, _cosmosLayer],
            _audit,
            Substitute.For<ILogger<FlushCacheHandler>>());
    }

    [Fact]
    public async Task HandleAsync_WhenRedisHasEntries_ReturnsRemovedCount()
    {
        _redisLayer.FlushAsync(Arg.Any<CancellationToken>()).Returns(42L);
        _cosmosLayer.FlushAsync(Arg.Any<CancellationToken>()).Returns(0L); // no-op

        var result = await _sut.HandleAsync();

        Assert.Equal(42, result.EntriesRemoved);
    }

    [Fact]
    public async Task HandleAsync_AlwaysCallsFlushOnAllLayers()
    {
        _redisLayer.FlushAsync(Arg.Any<CancellationToken>()).Returns(5L);
        _cosmosLayer.FlushAsync(Arg.Any<CancellationToken>()).Returns(0L);

        await _sut.HandleAsync();

        await _redisLayer.Received(1).FlushAsync(Arg.Any<CancellationToken>());
        await _cosmosLayer.Received(1).FlushAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task HandleAsync_EmitsCacheFlushedAuditEvent()
    {
        _redisLayer.FlushAsync(Arg.Any<CancellationToken>()).Returns(7L);
        _cosmosLayer.FlushAsync(Arg.Any<CancellationToken>()).Returns(0L);

        await _sut.HandleAsync();

        await _audit.Received(1).AppendAsync(
            Arg.Is<CacheFlushed>(e => e.EntriesRemoved == 7),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task HandleAsync_WhenRedisEmpty_ReturnsZeroRemovedCount()
    {
        _redisLayer.FlushAsync(Arg.Any<CancellationToken>()).Returns(0L);
        _cosmosLayer.FlushAsync(Arg.Any<CancellationToken>()).Returns(0L);

        var result = await _sut.HandleAsync();

        Assert.Equal(0, result.EntriesRemoved);
    }
}
