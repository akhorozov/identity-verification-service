using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace AddressValidation.Tests.Unit;

/// <summary>
/// Base fixture for unit tests with dependency injection setup
/// </summary>
public class UnitTestFixture : IDisposable
{
    public IServiceProvider ServiceProvider { get; }

    public UnitTestFixture()
    {
        var services = new ServiceCollection();

        // Register test services here

        ServiceProvider = services.BuildServiceProvider();
    }

    public void Dispose()
    {
        (ServiceProvider as IDisposable)?.Dispose();
    }
}

/// <summary>
/// Collection definition for sharing test fixtures
/// </summary>
[CollectionDefinition("Unit Tests Collection")]
public class UnitTestCollection : ICollectionFixture<UnitTestFixture>
{
    // This class has no code, and is never created. Its purpose is simply
    // to define the collection that other test classes can join.
}
