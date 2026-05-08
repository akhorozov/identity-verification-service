namespace AddressValidation.Api.Infrastructure.Services.Caching;

/// <summary>
/// Service for warming the cache on application startup.
/// Pre-loads commonly used or critical data into L1 (Redis) and L2 (CosmosDB).
/// </summary>
public interface ICacheWarmingService
{
    /// <summary>
    /// Performs cache warming on application startup.
    /// </summary>
    /// <param name="cancellationToken">Cancellation token for the async operation.</param>
    /// <returns>A task representing the async operation.</returns>
    Task WarmAsync(CancellationToken cancellationToken = default);
}

/// <summary>
/// Default implementation of cache warming service.
/// Currently a no-op placeholder for future pre-population strategies.
/// </summary>
public class CacheWarmingService : ICacheWarmingService
{
    private readonly ILogger<CacheWarmingService> _logger;

    /// <summary>
    /// Initializes a new instance of the CacheWarmingService class.
    /// </summary>
    /// <param name="logger">Logger instance.</param>
    public CacheWarmingService(ILogger<CacheWarmingService> logger)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    /// <inheritdoc />
    public async Task WarmAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            _logger.LogInformation("Starting cache warming...");

            // Placeholder for future cache warming strategies:
            // 1. Load frequently accessed address validation results
            // 2. Pre-populate known good addresses
            // 3. Load reference data (state codes, postal formats, etc.)
            // 4. Warm L1 from L2 on startup

            await Task.CompletedTask;

            _logger.LogInformation("Cache warming completed successfully");
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Cache warming failed (non-critical, proceeding anyway)");
            // Don't throw - warming is a performance optimization, not a requirement
        }
    }
}

/// <summary>
/// Hosted service that performs cache warming on application startup.
/// Registered in DI and runs automatically when the application starts.
/// </summary>
public class CacheWarmingHostedService : IHostedService
{
    private readonly ICacheWarmingService _cachewarmingService;
    private readonly ILogger<CacheWarmingHostedService> _logger;

    /// <summary>
    /// Initializes a new instance of the CacheWarmingHostedService class.
    /// </summary>
    /// <param name="cacheWarmingService">Cache warming service.</param>
    /// <param name="logger">Logger instance.</param>
    public CacheWarmingHostedService(
        ICacheWarmingService cacheWarmingService,
        ILogger<CacheWarmingHostedService> logger)
    {
        _cachewarmingService = cacheWarmingService ?? throw new ArgumentNullException(nameof(cacheWarmingService));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    /// <inheritdoc />
    public async Task StartAsync(CancellationToken cancellationToken)
    {
        _logger.LogInformation("CacheWarmingHostedService starting...");

        try
        {
            using var cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            cts.CancelAfter(TimeSpan.FromSeconds(30)); // Max 30 seconds for warming

            await _cachewarmingService.WarmAsync(cts.Token);
            _logger.LogInformation("CacheWarmingHostedService completed successfully");
        }
        catch (OperationCanceledException)
        {
            _logger.LogWarning("Cache warming timed out (30 seconds) - proceeding with partially warmed cache");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "CacheWarmingHostedService encountered an error during startup");
            // Don't rethrow - cache warming failures should not prevent app startup
        }
    }

    /// <inheritdoc />
    public Task StopAsync(CancellationToken cancellationToken)
    {
        _logger.LogInformation("CacheWarmingHostedService stopping...");
        return Task.CompletedTask;
    }
}
