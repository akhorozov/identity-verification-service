using Microsoft.Azure.Cosmos;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using AddressValidation.Api.Domain;
using AddressValidation.Api.Infrastructure.Caching;
using AddressValidation.Api.Infrastructure.CosmosDb;
using AddressValidation.Api.Infrastructure.Providers;
using AddressValidation.Api.Infrastructure.Providers.Smarty;
using AddressValidation.Api.Infrastructure.Redis;
using AddressValidation.Api.Infrastructure.Services.Audit;
using AddressValidation.Api.Infrastructure.Services.Caching;
using Refit;
using StackExchange.Redis;

namespace AddressValidation.Api.Infrastructure;

/// <summary>
/// Extension methods for registering infrastructure services
/// </summary>
public static class ServiceCollectionExtensions
{
    /// <summary>
    /// Add infrastructure services to the dependency injection container
    /// </summary>
    public static IServiceCollection AddInfrastructure(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        // Add Redis cache
        if (configuration.GetValue<bool>("Redis:Enabled"))
        {
            services.AddSingleton<IConnectionMultiplexer>(sp =>
            {
                var options = ConfigurationOptions.Parse(
                    configuration.GetValue<string>("Redis:ConnectionString") ?? "localhost:6379"
                );

                options.Ssl = configuration.GetValue<bool>("Redis:Ssl");
                options.AbortOnConnectFail = configuration.GetValue<bool>("Redis:AbortOnConnectFail", true);
                options.KeepAlive = configuration.GetValue<int>("Redis:KeepAlive", 180);
                options.ConnectTimeout = configuration.GetValue<int>("Redis:ConnectTimeout", 5000);
                options.SyncTimeout = configuration.GetValue<int>("Redis:SyncTimeout", 5000);

                return ConnectionMultiplexer.Connect(options);
            });

            services.AddSingleton<IRedisCache, RedisCache>();
        }

        // Add Cosmos DB cache
        if (configuration.GetValue<bool>("CosmosDb:Enabled"))
        {
            services.AddSingleton<ICosmosDbCache, CosmosDbCache>();
        }

        // Add cache abstraction (priority: Redis > Cosmos)
        services.AddSingleton<IDistributedCache>(sp =>
        {
            if (configuration.GetValue<bool>("Redis:Enabled"))
            {
                return sp.GetRequiredService<IRedisCache>();
            }

            if (configuration.GetValue<bool>("CosmosDb:Enabled"))
            {
                return sp.GetRequiredService<ICosmosDbCache>();
            }

            throw new InvalidOperationException(
                "At least one cache provider (Redis or Cosmos DB) must be enabled");
        });

        return services;
    }

    /// <summary>
    /// Registers the append-only audit event store and the startup service that ensures
    /// the <c>audit-events</c> Cosmos DB container exists with the correct configuration.
    /// </summary>
    public static IServiceCollection AddAuditEventStore(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(configuration);

        // CosmosClient is expected to already be registered (by AddCachingServices or the AppHost wire-up).
        // Register a singleton CosmosClient if one is not already present.
        if (!services.Any(sd => sd.ServiceType == typeof(CosmosClient)))
        {
            var endpoint = configuration["Cosmos:Endpoint"]
                ?? throw new InvalidOperationException("Cosmos:Endpoint configuration is required.");
            var key = configuration["Cosmos:Key"]
                ?? throw new InvalidOperationException("Cosmos:Key configuration is required.");

            services.AddSingleton(_ => new CosmosClient(endpoint, key,
                new CosmosClientOptions { ApplicationName = "AddressValidation.Api" }));
        }

        services.AddSingleton<IAuditEventStore, CosmosAuditEventStore>();
        services.AddHostedService<AuditContainerInitializationService>();

        return services;
    }

    /// <summary>
    /// Registers the Smarty address validation provider with resilience policies.
    /// Requires <c>Smarty:BaseUrl</c>, <c>Smarty:AuthId</c>, and <c>Smarty:AuthToken</c>
    /// in configuration (use Azure Key Vault or user-secrets for sensitive values).
    /// </summary>
    public static IServiceCollection AddProviders(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(configuration);

        var baseUrl = configuration["Smarty:BaseUrl"] ?? "https://us-street.api.smarty.com";

        services
            .AddRefitClient<ISmartyApi>(settings => new RefitSettings
            {
                ContentSerializer = new SystemTextJsonContentSerializer()
            })
            .ConfigureHttpClient(client =>
            {
                client.BaseAddress = new Uri(baseUrl);
                client.DefaultRequestHeaders.Add("x-standardize-only", "false");
            })
            .AddHttpMessageHandler(sp =>
                new SmartyAuthHandler(
                    configuration["Smarty:AuthId"] ?? string.Empty,
                    configuration["Smarty:AuthToken"] ?? string.Empty))
            .AddProviderResilience();

        services.AddScoped<IAddressValidationProvider, SmartyProvider>();

        return services;
    }

    /// <summary>
    /// Registers the T3 multi-level cache orchestrator for <see cref="ValidationResponse"/>.
    /// Wires <see cref="RedisCacheService{T}"/> (L1) and <see cref="CosmosCacheService{T}"/> (L2)
    /// into a <see cref="CacheOrchestrator{T}"/> available for injection.
    /// </summary>
    public static IServiceCollection AddValidationCaching(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(configuration);

        services.AddSingleton<ICacheService<ValidationResponse>, RedisCacheService<ValidationResponse>>();
        services.AddSingleton<ICacheService<ValidationResponse>>(sp =>
            new CosmosCacheService<ValidationResponse>(
                sp.GetRequiredService<CosmosClient>(),
                configuration,
                sp.GetRequiredService<ILogger<CosmosCacheService<ValidationResponse>>>()));

        services.AddSingleton<CacheOrchestrator<ValidationResponse>>(sp =>
        {
            var all = sp.GetServices<ICacheService<ValidationResponse>>().ToArray();
            // First registered = L1 (Redis), second = L2 (CosmosDB)
            return new CacheOrchestrator<ValidationResponse>(
                all[0],
                all[1],
                sp.GetRequiredService<ILogger<CacheOrchestrator<ValidationResponse>>>());
        });

        return services;
    }
}
