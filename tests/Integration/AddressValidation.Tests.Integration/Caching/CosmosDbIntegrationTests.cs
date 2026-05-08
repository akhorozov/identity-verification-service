namespace AddressValidation.Tests.Integration.Caching;

using AddressValidation.Api.Domain;
using AddressValidation.Api.Infrastructure.Services.Caching;
using Microsoft.Azure.Cosmos;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;
using Testcontainers.CosmosDb;
using Xunit;

/// <summary>
/// Testcontainers-backed CosmosDB integration tests (issue #122).
/// Verifies that <see cref="CosmosCacheService{T}"/> correctly stores, retrieves,
/// and manages values against a real CosmosDB Emulator container.
/// </summary>
/// <remarks>
/// These tests require Linux Docker (CosmosDB Emulator image is Linux-only).
/// They are tagged Category=CosmosDb and can be excluded on Windows CI:
///   dotnet test --filter "Category!=CosmosDb"
/// </remarks>
[Trait("Category", "CosmosDb")]
public sealed class CosmosDbIntegrationTests : IAsyncLifetime
{
    private const string DatabaseId = "test-cache-db";
    private const string ContainerId = "test-cache-container";

    private readonly CosmosDbContainer _container = new CosmosDbBuilder()
        .Build();

    private CosmosClient _cosmosClient = null!;
    private CosmosCacheService<ValidationResponse> _sut = null!;

    public async Task InitializeAsync()
    {
        await _container.StartAsync();

        // CosmosDB Emulator uses a well-known self-signed cert — disable validation in tests
        _cosmosClient = new CosmosClient(
            _container.GetConnectionString(),
            new CosmosClientOptions
            {
                HttpClientFactory = () => new System.Net.Http.HttpClient(
                    new System.Net.Http.HttpClientHandler
                    {
                        ServerCertificateCustomValidationCallback =
                            System.Net.Http.HttpClientHandler.DangerousAcceptAnyServerCertificateValidator
                    }),
                ConnectionMode = ConnectionMode.Gateway,
            });

        // Provision database + container
        var db = await _cosmosClient.CreateDatabaseIfNotExistsAsync(DatabaseId);
        await db.Database.CreateContainerIfNotExistsAsync(
            new ContainerProperties(ContainerId, "/pk") { DefaultTimeToLive = -1 });

        var config = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Cosmos:DatabaseId"] = DatabaseId,
                ["Cosmos:CacheContainerId"] = ContainerId,
                ["Cosmos:DefaultTtlSeconds"] = "86400",
                ["Cosmos:PartitionKeyPath"] = "/pk",
            })
            .Build();

        _sut = new CosmosCacheService<ValidationResponse>(
            _cosmosClient,
            config,
            NullLogger<CosmosCacheService<ValidationResponse>>.Instance);
    }

    public async Task DisposeAsync()
    {
        _cosmosClient.Dispose();
        await _container.DisposeAsync();
    }

    private static ValidationResponse MakeResponse(string street) => new()
    {
        InputAddress = new AddressInput { Street = street, City = "Chicago", State = "IL" },
        Status = "validated",
        ValidatedAddress = new ValidatedAddress { DeliveryLine1 = street, LastLine = "Chicago IL 60601" },
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
        var key = $"addr:v1:{Guid.NewGuid():N}aabbccddeeff00112233445566778899";
        var value = MakeResponse("100 Congress Ave");

        await _sut.SetAsync(key, value);
        var result = await _sut.GetAsync(key);

        result.ShouldNotBeNull();
        result!.Status.ShouldBe("validated");
        result.ValidatedAddress!.DeliveryLine1.ShouldBe("100 Congress Ave");
    }

    [Fact]
    public async Task GetAsync_ForMissingKey_ReturnsNull()
    {
        var key = $"addr:v1:{Guid.NewGuid():N}aabbccddeeff00112233445566778899";
        var result = await _sut.GetAsync(key);
        result.ShouldBeNull();
    }

    [Fact]
    public async Task ExistsAsync_AfterSet_ReturnsTrue()
    {
        var key = $"addr:v1:{Guid.NewGuid():N}aabbccddeeff00112233445566778899";
        await _sut.SetAsync(key, MakeResponse("200 State St"));

        var exists = await _sut.ExistsAsync(key);
        exists.ShouldBeTrue();
    }

    [Fact]
    public async Task RemoveAsync_DeletesKey()
    {
        var key = $"addr:v1:{Guid.NewGuid():N}aabbccddeeff00112233445566778899";
        await _sut.SetAsync(key, MakeResponse("300 Oak Blvd"));

        await _sut.RemoveAsync(key);

        var exists = await _sut.ExistsAsync(key);
        exists.ShouldBeFalse();
    }

    [Fact]
    public async Task SetAsync_OverwritesExistingKey()
    {
        var key = $"addr:v1:{Guid.NewGuid():N}aabbccddeeff00112233445566778899";
        await _sut.SetAsync(key, MakeResponse("Old Street"));
        await _sut.SetAsync(key, MakeResponse("New Street"));

        var result = await _sut.GetAsync(key);
        result!.ValidatedAddress!.DeliveryLine1.ShouldBe("New Street");
    }
}
