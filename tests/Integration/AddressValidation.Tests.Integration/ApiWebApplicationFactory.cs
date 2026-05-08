namespace AddressValidation.Tests.Integration;

using System.Net.Http.Headers;
using AddressValidation.Api.Domain;
using AddressValidation.Api.Infrastructure.Providers;
using AddressValidation.Api.Infrastructure.Services.Audit;
using AddressValidation.Api.Infrastructure.Services.Caching;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Azure.Cosmos;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;
using StackExchange.Redis;
using WireMock.Server;

/// <summary>
/// Custom <see cref="WebApplicationFactory{TProgram}"/> for HTTP pipeline integration tests (issue #120).
/// Replaces external dependencies (Smarty, Redis, CosmosDB) with test doubles so no real
/// infrastructure is required for pipeline tests. Testcontainer-based tests live in
/// separate fixtures.
/// </summary>
public sealed class ApiWebApplicationFactory : WebApplicationFactory<Program>, IAsyncDisposable
{
    private WireMockServer? _smartyMock;

    /// <summary>WireMock server standing in for the Smarty US-Street API.</summary>
    public WireMockServer SmartyMock => _smartyMock
        ?? throw new InvalidOperationException("Factory not yet started.");

    /// <summary>Pre-configured NSubstitute provider stub (alternative to WireMock).</summary>
    public IAddressValidationProvider ProviderSubstitute { get; } =
        Substitute.For<IAddressValidationProvider>();

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        _smartyMock = WireMockServer.Start();

        builder.UseEnvironment("Testing");

        builder.ConfigureAppConfiguration((_, config) =>
        {
            config.AddInMemoryCollection(new Dictionary<string, string?>
            {
                // Disable real external services
                ["Redis:Enabled"] = "false",
                ["CosmosDb:Enabled"] = "false",
                ["Cosmos:Enabled"] = "false",
                ["AzureKeyVault:Enabled"] = "false",
                ["OpenTelemetry:Enabled"] = "false",
                ["Audit:Enabled"] = "false",
                ["Audit:LogToStorage"] = "false",
                // Point Smarty at WireMock
                ["Smarty:BaseUrl"] = _smartyMock.Url!,
                ["Smarty:AuthId"] = "test-auth-id",
                ["Smarty:AuthToken"] = "test-auth-token",
                // Test API keys
                ["Security:ApiKeys:0:Name"] = "test-readonly",
                ["Security:ApiKeys:0:Key"] = "test-readonly-key",
                ["Security:ApiKeys:0:Role"] = "readonly",
                ["Security:ApiKeys:1:Name"] = "test-admin",
                ["Security:ApiKeys:1:Key"] = "test-admin-key",
                ["Security:ApiKeys:1:Role"] = "admin",
                // Disable rate limiting in tests
                ["Security:RateLimiting:Enabled"] = "false",
            });
        });

        builder.ConfigureServices(services =>
        {
            // ── Remove real external infrastructure registrations ──────────────────

            // CosmosClient (used by CosmosCacheService, CosmosAuditEventStore, etc.)
            services.RemoveAll<CosmosClient>();
            services.AddSingleton(_ => Substitute.For<CosmosClient>());

            // IConnectionMultiplexer (used by RedisCacheService)
            services.RemoveAll<IConnectionMultiplexer>();
            services.AddSingleton(_ => Substitute.For<IConnectionMultiplexer>());

            // Remove all registered ICacheService<ValidationResponse> (L1+L2)
            var cacheDescriptors = services
                .Where(d => d.ServiceType == typeof(ICacheService<ValidationResponse>))
                .ToList();
            foreach (var d in cacheDescriptors) services.Remove(d);

            // Register no-op NSubstitute stubs for both cache levels
            var l1 = Substitute.For<ICacheService<ValidationResponse>>();
            var l2 = Substitute.For<ICacheService<ValidationResponse>>();
            services.AddSingleton(l1);
            services.AddSingleton(l2);

            // Remove real CacheOrchestrator and register one backed by the stubs
            services.RemoveAll<CacheOrchestrator<ValidationResponse>>();
            services.AddSingleton(_ =>
                new CacheOrchestrator<ValidationResponse>(
                    l1,
                    l2,
                    NullLogger<CacheOrchestrator<ValidationResponse>>.Instance));

            // Replace the real validation provider with a NSubstitute stub
            services.RemoveAll<IAddressValidationProvider>();
            services.AddScoped(_ => ProviderSubstitute);

            // Replace audit store with no-op stub
            services.RemoveAll<IAuditEventStore>();
            services.AddSingleton(_ => Substitute.For<IAuditEventStore>());
        });
    }

    /// <summary>Creates an <see cref="HttpClient"/> pre-configured with the readonly API key.</summary>
    public HttpClient CreateReadonlyClient()
    {
        var client = CreateClient(new WebApplicationFactoryClientOptions
        {
            AllowAutoRedirect = false,
            HandleCookies = false,
        });
        client.DefaultRequestHeaders.Add("X-Api-Key", "test-readonly-key");
        client.DefaultRequestHeaders.Add("Api-Version", "1.0");
        client.DefaultRequestHeaders.Accept.Add(
            new MediaTypeWithQualityHeaderValue("application/json"));
        return client;
    }

    /// <summary>Creates an <see cref="HttpClient"/> pre-configured with the admin API key.</summary>
    public HttpClient CreateAdminClient()
    {
        var client = CreateClient(new WebApplicationFactoryClientOptions
        {
            AllowAutoRedirect = false,
            HandleCookies = false,
        });
        client.DefaultRequestHeaders.Add("X-Api-Key", "test-admin-key");
        client.DefaultRequestHeaders.Add("Api-Version", "1.0");
        client.DefaultRequestHeaders.Accept.Add(
            new MediaTypeWithQualityHeaderValue("application/json"));
        return client;
    }

    async ValueTask IAsyncDisposable.DisposeAsync()
    {
        _smartyMock?.Stop();
        _smartyMock?.Dispose();
        await Task.CompletedTask;
        Dispose();
    }
}
