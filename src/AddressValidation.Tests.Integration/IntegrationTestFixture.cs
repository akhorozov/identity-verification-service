using Xunit;

namespace AddressValidation.Tests.Integration;

/// <summary>
/// Base fixture for integration tests with API and container setup
/// </summary>
public class IntegrationTestFixture : IAsyncLifetime
{
    private HttpClient? _httpClient;

    public HttpClient HttpClient => _httpClient ?? throw new InvalidOperationException("Fixture not initialized");

    public async Task InitializeAsync()
    {
        // Initialize test containers (Redis, Cosmos DB) here
        // Initialize HttpClient to test API
        _httpClient = new HttpClient();
        await Task.CompletedTask;
    }

    public async Task DisposeAsync()
    {
        _httpClient?.Dispose();
        // Clean up containers here
        await Task.CompletedTask;
    }
}

/// <summary>
/// Collection definition for sharing integration test fixtures
/// </summary>
[CollectionDefinition("Integration Tests Collection")]
public class IntegrationTestCollection : ICollectionFixture<IntegrationTestFixture>
{
    // This class has no code, and is never created. Its purpose is simply
    // to define the collection that other test classes can join.
}

