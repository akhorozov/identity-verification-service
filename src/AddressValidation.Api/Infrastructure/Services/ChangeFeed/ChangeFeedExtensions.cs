namespace AddressValidation.Api.Infrastructure.Services.ChangeFeed;

using Microsoft.Extensions.DependencyInjection;

/// <summary>
/// DI registration extension for the CosmosDB Change Feed processor.
/// </summary>
public static class ChangeFeedExtensions
{
    /// <summary>
    /// Registers the <see cref="ChangeFeedProcessorService"/> and its hosted service wrapper.
    /// Requires <see cref="Microsoft.Azure.Cosmos.CosmosClient"/> to be registered separately
    /// (done by <c>AddCosmosServices</c> in <c>ServiceCollectionExtensions</c>).
    /// </summary>
    public static IServiceCollection AddChangeFeedProcessor(this IServiceCollection services)
    {
        services.AddSingleton<IChangeFeedProcessor, ChangeFeedProcessorService>();
        services.AddHostedService<ChangeFeedHostedService>();
        return services;
    }
}
