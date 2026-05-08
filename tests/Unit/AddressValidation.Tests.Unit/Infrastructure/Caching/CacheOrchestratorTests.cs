namespace AddressValidation.Tests.Unit.Infrastructure.Caching;

using AddressValidation.Api.Domain;
using AddressValidation.Api.Infrastructure.Services.Caching;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;
using NSubstitute.ExceptionExtensions;
using Xunit;

/// <summary>
/// Unit tests for <see cref="CacheOrchestrator{T}"/> covering the L1 → L2 → Provider
/// lookup chain, write-through, and fault isolation behavior (issue #123).
/// </summary>
public sealed class CacheOrchestratorTests
{
    private readonly ICacheService<ValidationResponse> _l1 = Substitute.For<ICacheService<ValidationResponse>>();
    private readonly ICacheService<ValidationResponse> _l2 = Substitute.For<ICacheService<ValidationResponse>>();
    private readonly CacheOrchestrator<ValidationResponse> _sut;

    private static readonly string Key = $"addr:v1:{new string('a', 64)}";

    private static ValidationResponse MakeResponse(string source) => new()
    {
        InputAddress = new AddressInput { Street = "1 Main St", City = "Testville", State = "TX" },
        Status = "validated",
        ValidatedAddress = new ValidatedAddress { DeliveryLine1 = "1 Main St", LastLine = "Testville TX 77001" },
        Metadata = new ValidationMetadata
        {
            ProviderName = "Smarty",
            ValidatedAt = DateTimeOffset.UtcNow,
            CacheSource = source,
            ApiVersion = "1.0",
        },
    };

    public CacheOrchestratorTests()
    {
        _sut = new CacheOrchestrator<ValidationResponse>(
            _l1,
            _l2,
            NullLogger<CacheOrchestrator<ValidationResponse>>.Instance);
    }

    // ── L1 hit ───────────────────────────────────────────────────────────

    [Fact]
    public async Task GetAsync_WhenL1Hits_ReturnsL1ValueWithoutCallingL2OrProvider()
    {
        var response = MakeResponse("L1");
        _l1.GetAsync(Key, Arg.Any<CancellationToken>()).Returns(response);

        var result = await _sut.GetAsync(Key, (_, _) => Task.FromResult<ValidationResponse?>(null));

        result.IsHit.ShouldBeTrue();
        result.Source!.Source.ShouldBe("L1:Redis");
        result.Value.ShouldBe(response);
        await _l2.DidNotReceive().GetAsync(Arg.Any<string>(), Arg.Any<CancellationToken>());
    }

    // ── L2 hit ───────────────────────────────────────────────────────────

    [Fact]
    public async Task GetAsync_WhenL1MissesAndL2Hits_ReturnsL2Value()
    {
        var response = MakeResponse("L2");
        _l1.GetAsync(Key, Arg.Any<CancellationToken>()).Returns((ValidationResponse?)null);
        _l2.GetAsync(Key, Arg.Any<CancellationToken>()).Returns(response);

        var result = await _sut.GetAsync(Key, (_, _) => Task.FromResult<ValidationResponse?>(null));

        result.IsHit.ShouldBeTrue();
        result.Source!.Source.ShouldBe("L2:CosmosDB");
        result.Value.ShouldBe(response);
    }

    [Fact]
    public async Task GetAsync_WhenL2Hits_WarmsL1Asynchronously()
    {
        var response = MakeResponse("L2");
        _l1.GetAsync(Key, Arg.Any<CancellationToken>()).Returns((ValidationResponse?)null);
        _l2.GetAsync(Key, Arg.Any<CancellationToken>()).Returns(response);

        await _sut.GetAsync(Key, (_, _) => Task.FromResult<ValidationResponse?>(null));

        // Give the fire-and-forget write a moment to run
        await Task.Delay(50);

        await _l1.Received(1).SetAsync(Key, response, Arg.Any<TimeSpan?>(), Arg.Any<CancellationToken>());
    }

    // ── Provider hit ─────────────────────────────────────────────────────

    [Fact]
    public async Task GetAsync_WhenBothCachesMiss_CallsProvider()
    {
        var response = MakeResponse("PROVIDER");
        _l1.GetAsync(Key, Arg.Any<CancellationToken>()).Returns((ValidationResponse?)null);
        _l2.GetAsync(Key, Arg.Any<CancellationToken>()).Returns((ValidationResponse?)null);

        var result = await _sut.GetAsync(Key, (_, _) => Task.FromResult<ValidationResponse?>(response));

        result.IsHit.ShouldBeTrue();
        result.Source!.Source.ShouldBe("Provider:External");
        result.Value.ShouldBe(response);
    }

    [Fact]
    public async Task GetAsync_WhenProviderHits_WriteThroughBothLevels()
    {
        var response = MakeResponse("PROVIDER");
        _l1.GetAsync(Key, Arg.Any<CancellationToken>()).Returns((ValidationResponse?)null);
        _l2.GetAsync(Key, Arg.Any<CancellationToken>()).Returns((ValidationResponse?)null);

        await _sut.GetAsync(Key, (_, _) => Task.FromResult<ValidationResponse?>(response));

        await _l1.Received(1).SetAsync(Key, response, Arg.Any<TimeSpan?>(), Arg.Any<CancellationToken>());
        await _l2.Received(1).SetAsync(Key, response, Arg.Any<TimeSpan?>(), Arg.Any<CancellationToken>());
    }

    // ── Full miss ────────────────────────────────────────────────────────

    [Fact]
    public async Task GetAsync_WhenAllMiss_ReturnsIsHitFalse()
    {
        _l1.GetAsync(Key, Arg.Any<CancellationToken>()).Returns((ValidationResponse?)null);
        _l2.GetAsync(Key, Arg.Any<CancellationToken>()).Returns((ValidationResponse?)null);

        var result = await _sut.GetAsync(Key, (_, _) => Task.FromResult<ValidationResponse?>(null));

        result.IsHit.ShouldBeFalse();
        result.Value.ShouldBeNull();
        result.Source!.Source.ShouldBe("Miss:None");
    }

    // ── SetAsync ─────────────────────────────────────────────────────────

    [Fact]
    public async Task SetAsync_WritesBothLevels()
    {
        var response = MakeResponse("PROVIDER");

        await _sut.SetAsync(Key, response);

        await _l1.Received(1).SetAsync(Key, response, Arg.Any<TimeSpan?>(), Arg.Any<CancellationToken>());
        await _l2.Received(1).SetAsync(Key, response, Arg.Any<TimeSpan?>(), Arg.Any<CancellationToken>());
    }

    // ── Fault isolation ──────────────────────────────────────────────────

    [Fact]
    public async Task GetAsync_WhenL1Throws_PropagatesException()
    {
        _l1.GetAsync(Key, Arg.Any<CancellationToken>())
            .ThrowsAsync(new InvalidOperationException("Redis down"));

        var act = () => _sut.GetAsync(Key, (_, _) => Task.FromResult<ValidationResponse?>(null));

        await act.ShouldThrowAsync<InvalidOperationException>();
    }

    [Fact]
    public async Task GetAsync_WhenCancelled_ThrowsOperationCancelledException()
    {
        var cts = new CancellationTokenSource();
        await cts.CancelAsync();

        _l1.GetAsync(Key, Arg.Any<CancellationToken>())
            .ThrowsAsync(new OperationCanceledException());

        var act = () => _sut.GetAsync(Key, (_, _) => Task.FromResult<ValidationResponse?>(null), cts.Token);

        await act.ShouldThrowAsync<OperationCanceledException>();
    }

    // ── Guard clauses ────────────────────────────────────────────────────

    [Fact]
    public async Task GetAsync_WhenKeyIsEmpty_ThrowsArgumentException()
    {
        var act = () => _sut.GetAsync(string.Empty, (_, _) => Task.FromResult<ValidationResponse?>(null));

        await act.ShouldThrowAsync<ArgumentException>();
    }

    [Fact]
    public async Task SetAsync_WhenKeyIsEmpty_ThrowsArgumentException()
    {
        var act = () => _sut.SetAsync(string.Empty, MakeResponse("X"));

        await act.ShouldThrowAsync<ArgumentException>();
    }

    [Fact]
    public void Constructor_WhenL1IsNull_ThrowsArgumentNullException()
    {
        var act = () => new CacheOrchestrator<ValidationResponse>(
            null!,
            _l2,
            NullLogger<CacheOrchestrator<ValidationResponse>>.Instance);

        act.ShouldThrow<ArgumentNullException>();
    }

    [Fact]
    public void Constructor_WhenL2IsNull_ThrowsArgumentNullException()
    {
        var act = () => new CacheOrchestrator<ValidationResponse>(
            _l1,
            null!,
            NullLogger<CacheOrchestrator<ValidationResponse>>.Instance);

        act.ShouldThrow<ArgumentNullException>();
    }
}
