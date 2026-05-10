namespace AddressValidation.Api.Infrastructure.Services.ChangeFeed;

using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

/// <summary>
/// Background hosted service that manages the lifecycle of the
/// <see cref="IChangeFeedProcessor"/> alongside the application.
/// </summary>
internal sealed class ChangeFeedHostedService : BackgroundService
{
    private readonly IChangeFeedProcessor _processor;
    private readonly ILogger<ChangeFeedHostedService> _logger;

    public ChangeFeedHostedService(
        IChangeFeedProcessor processor,
        ILogger<ChangeFeedHostedService> logger)
    {
        ArgumentNullException.ThrowIfNull(processor);
        ArgumentNullException.ThrowIfNull(logger);

        _processor = processor;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        try
        {
            await _processor.StartAsync(stoppingToken);
            // Keep running until the host signals cancellation.
            await Task.Delay(Timeout.Infinite, stoppingToken);
        }
        catch (OperationCanceledException)
        {
            // Normal shutdown — not an error.
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "CosmosDB Change Feed processor encountered an unexpected error.");
        }
        finally
        {
            await _processor.StopAsync(CancellationToken.None);
        }
    }
}
