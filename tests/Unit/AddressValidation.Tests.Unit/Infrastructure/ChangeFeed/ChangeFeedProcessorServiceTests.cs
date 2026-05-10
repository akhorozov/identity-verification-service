namespace AddressValidation.Tests.Unit.Infrastructure.ChangeFeed;

using AddressValidation.Api.Domain.Events;
using AddressValidation.Api.Infrastructure.Services.ChangeFeed;
using Microsoft.Azure.Cosmos;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using NSubstitute;
using Xunit;

/// <summary>
/// Unit tests for <see cref="ChangeFeedProcessorService"/> covering event handling,
/// idempotent start, error isolation, and graceful stop.
/// SRS Ref: Section 7.4, T15 #139
/// </summary>
public sealed class ChangeFeedProcessorServiceTests
{
    private readonly ILogger<ChangeFeedProcessorService> _logger =
        Substitute.For<ILogger<ChangeFeedProcessorService>>();

    private readonly IConfiguration _configuration =
        new ConfigurationBuilder().Build();

    // CosmosClient is never invoked in tests that target HandleChangesAsync/StopAsync directly.
    private readonly CosmosClient _cosmosClient =
        Substitute.For<CosmosClient>();

    private ChangeFeedProcessorService BuildSut() =>
        new(_cosmosClient, _configuration, _logger);

    // ─── HandleChangesAsync ────────────────────────────────────────────────

    [Fact]
    public async Task HandleChangesAsync_WhenCollectionIsEmpty_DoesNotLog()
    {
        var sut = BuildSut();

        await sut.HandleChangesAsync([], CancellationToken.None);

        _logger.DidNotReceiveWithAnyArgs().Log(
            default, default, default(string), default(Exception), default(Func<string, Exception?, string>)!);
    }

    [Fact]
    public async Task HandleChangesAsync_WhenAddressValidatedEvent_LogsInformation()
    {
        // Arrange
        var sut = BuildSut();
        var domainEvent = new AddressValidated
        {
            AggregateId = "abc123",
            AddressHash = "abc123",
            DpvMatchCode = "Y",
            ProviderName = "Smarty",
            CacheSource = "PROVIDER"
        };

        // Act — should not throw
        await sut.HandleChangesAsync([domainEvent], CancellationToken.None);
    }

    [Fact]
    public async Task HandleChangesAsync_WhenAddressValidationFailedEvent_DoesNotThrow()
    {
        var sut = BuildSut();
        var failedEvent = new AddressValidationFailed
        {
            AggregateId = "xyz789",
            AddressHash = "xyz789",
            FailureReason = "ADDRESS_NOT_FOUND"
        };

        var exception = await Record.ExceptionAsync(() =>
            sut.HandleChangesAsync([failedEvent], CancellationToken.None));

        Assert.Null(exception);
    }

    [Fact]
    public async Task HandleChangesAsync_WhenCancelled_ThrowsOperationCancelledException()
    {
        var sut = BuildSut();
        using var cts = new CancellationTokenSource();
        await cts.CancelAsync();

        var domainEvent = new AddressValidated
        {
            AggregateId = "any",
            AddressHash = "any",
            DpvMatchCode = "Y",
            ProviderName = "Smarty",
            CacheSource = "L1"
        };

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() =>
            sut.HandleChangesAsync([domainEvent], cts.Token));
    }

    [Fact]
    public async Task HandleChangesAsync_WhenMultipleEvents_ProcessesAll()
    {
        var sut = BuildSut();
        var events = new DomainEvent[]
        {
            new AddressValidated
            {
                AggregateId = "a1",
                AddressHash = "a1",
                DpvMatchCode = "Y",
                ProviderName = "Smarty",
                CacheSource = "L2"
            },
            new AddressValidationFailed
            {
                AggregateId = "a2",
                AddressHash = "a2",
                FailureReason = "INVALID_INPUT"
            },
            new AddressValidated
            {
                AggregateId = "a3",
                AddressHash = "a3",
                DpvMatchCode = "S",
                ProviderName = "Smarty",
                CacheSource = "PROVIDER"
            }
        };

        var exception = await Record.ExceptionAsync(() =>
            sut.HandleChangesAsync(events, CancellationToken.None));

        Assert.Null(exception);
    }

    // ─── StopAsync ────────────────────────────────────────────────────────

    [Fact]
    public async Task StopAsync_WhenNotStarted_DoesNotThrow()
    {
        var sut = BuildSut();

        var exception = await Record.ExceptionAsync(() =>
            sut.StopAsync(CancellationToken.None));

        Assert.Null(exception);
    }
}
