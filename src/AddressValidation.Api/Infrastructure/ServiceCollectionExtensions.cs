using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using AddressValidation.Api.Infrastructure.Caching;
using AddressValidation.Api.Infrastructure.CosmosDb;
using AddressValidation.Api.Infrastructure.Redis;
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
}
