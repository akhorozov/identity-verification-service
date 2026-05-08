namespace AddressValidation.Tests.Integration.Caching;

using System.Text.Json;
using AddressValidation.Api.Domain;
using AddressValidation.Api.Infrastructure.Services.Caching;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;
using StackExchange.Redis;
using Testcontainers.Redis;
using Xunit;

/// <summary>
/// Testcontainers-backed Redis integration tests (issue #121).
/// Verifies that <see cref="RedisCacheService{T}"/> correctly stores, retrieves,
/// and expires values against a real Redis instance.
/// </summary>
public sealed class RedisIntegrationTests : IAsyncLifetime
{
    private readonly RedisContainer _container = new RedisBuilder()
        .WithImage("redis:7-alpine")
        .Build();

    private IConnectionMultiplexer _redis = null!;
    private RedisCacheService<ValidationResponse> _sut = null!;

    public async Task InitializeAsync()
    {
        await _container.StartAsync();

        _redis = await ConnectionMultiplexer.ConnectAsync(_container.GetConnectionString());

        var config = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Redis:DefaultDatabase"] = "0",
                ["Redis:DefaultTtlSeconds"] = "60",
            })
            .Build();

        _sut = new RedisCacheService<ValidationResponse>(
            _redis,
            config,
            NullLogger<RedisCacheService<ValidationResponse>>.Instance);
    }

    public async Task DisposeAsync()
    {
        _redis.Dispose();
        await _container.DisposeAsync();
    }

    private static ValidationResponse MakeResponse(string street) => new()
    {
        InputAddress = new AddressInput { Street = street, City = "Springfield", State = "IL" },
        Status = "validated",
        ValidatedAddress = new ValidatedAddress { DeliveryLine1 = street, LastLine = "Springfield IL 62701" },
        Metadata = new ValidationMetadata
        {
            ProviderName = "Smarty",
            ValidatedAt = DateTimeOffset.UtcNow,
            CacheSource = "PROVIDER",
            ApiVersion = "1.0",
        },
    };

    [Fact]
    public async Task SetAsync_GetAsync_RoundTrip_ReturnsStoredValue()
    {
        var key = $"addr:v1:{Guid.NewGuid():N}";
        var value = MakeResponse("123 Main St");

        await _sut.SetAsync(key, value);
        var result = await _sut.GetAsync(key);

        result.ShouldNotBeNull();
        result!.Status.ShouldBe("validated");
        result.ValidatedAddress!.DeliveryLine1.ShouldBe("123 Main St");
    }

    [Fact]
    public async Task GetAsync_ForMissingKey_ReturnsNull()
    {
        var result = await _sut.GetAsync($"addr:v1:{Guid.NewGuid():N}");
        result.ShouldBeNull();
    }

    [Fact]
    public async Task ExistsAsync_AfterSet_ReturnsTrue()
    {
        var key = $"addr:v1:{Guid.NewGuid():N}";
        await _sut.SetAsync(key, MakeResponse("456 Oak Ave"));

        var exists = await _sut.ExistsAsync(key);
        exists.ShouldBeTrue();
    }

    [Fact]
    public async Task ExistsAsync_ForMissingKey_ReturnsFalse()
    {
        var exists = await _sut.ExistsAsync($"addr:v1:{Guid.NewGuid():N}");
        exists.ShouldBeFalse();
    }

    [Fact]
    public async Task RemoveAsync_DeletesKey()
    {
        var key = $"addr:v1:{Guid.NewGuid():N}";
        await _sut.SetAsync(key, MakeResponse("789 Elm St"));

        await _sut.RemoveAsync(key);

        var exists = await _sut.ExistsAsync(key);
        exists.ShouldBeFalse();
    }

    [Fact]
    public async Task SetAsync_WithCustomTtl_ExpiresAfterTtl()
    {
        var key = $"addr:v1:{Guid.NewGuid():N}";
        await _sut.SetAsync(key, MakeResponse("Expiring St"), TimeSpan.FromSeconds(1));

        // Verify it's there immediately
        var before = await _sut.ExistsAsync(key);
        before.ShouldBeTrue();

        // Wait for expiry
        await Task.Delay(TimeSpan.FromSeconds(2));

        var after = await _sut.ExistsAsync(key);
        after.ShouldBeFalse();
    }

    [Fact]
    public async Task SetAsync_OverwritesExistingKey()
    {
        var key = $"addr:v1:{Guid.NewGuid():N}";
        await _sut.SetAsync(key, MakeResponse("First Value"));
        await _sut.SetAsync(key, MakeResponse("Second Value"));

        var result = await _sut.GetAsync(key);
        result!.ValidatedAddress!.DeliveryLine1.ShouldBe("Second Value");
    }

    [Fact]
    public async Task MultipleKeys_AreIndependent()
    {
        var key1 = $"addr:v1:{Guid.NewGuid():N}";
        var key2 = $"addr:v1:{Guid.NewGuid():N}";
        await _sut.SetAsync(key1, MakeResponse("Street One"));
        await _sut.SetAsync(key2, MakeResponse("Street Two"));

        var r1 = await _sut.GetAsync(key1);
        var r2 = await _sut.GetAsync(key2);

        r1!.ValidatedAddress!.DeliveryLine1.ShouldBe("Street One");
        r2!.ValidatedAddress!.DeliveryLine1.ShouldBe("Street Two");
    }
}
