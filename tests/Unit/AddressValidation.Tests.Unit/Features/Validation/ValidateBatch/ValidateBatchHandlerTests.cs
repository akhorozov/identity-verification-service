namespace AddressValidation.Tests.Unit.Features.Validation.ValidateBatch;

using AddressValidation.Api.Domain;
using AddressValidation.Api.Domain.Events;
using AddressValidation.Api.Features.Validation.ValidateBatch;
using AddressValidation.Api.Infrastructure.Metrics;
using AddressValidation.Api.Infrastructure.Providers;
using AddressValidation.Api.Infrastructure.Services.Audit;
using AddressValidation.Api.Infrastructure.Services.Caching;
using Microsoft.Extensions.Logging;
using NSubstitute;
using NSubstitute.ExceptionExtensions;
using Xunit;

/// <summary>
/// Unit tests for <see cref="ValidateBatchHandler"/>.
/// </summary>
public class ValidateBatchHandlerTests
{
    private readonly CacheOrchestrator<ValidationResponse> _cache;
    private readonly ICacheService<ValidationResponse> _l1;
    private readonly ICacheService<ValidationResponse> _l2;
    private readonly IAddressValidationProvider _provider;
    private readonly IAuditEventStore _audit;
    private readonly ValidateBatchHandler _sut;

    private static ValidateBatchRequest MakeRequest(int count = 2) => new()
    {
        Addresses = Enumerable.Range(0, count).Select(i => new ValidateBatchItem
        {
            Street  = $"{i + 1} Main St",
            ZipCode = "90210",
        }).ToArray()
    };

    private static ValidationResponse MakeResponse(string street, string dpv = "Y") => new()
    {
        InputAddress     = new AddressInput { Street = street, ZipCode = "90210" },
        ValidatedAddress = new ValidatedAddress { DeliveryLine1 = street },
        Analysis         = new AddressAnalysis { DpvMatchCode = dpv },
        Status           = dpv == "N" ? "undeliverable" : "validated",
        Metadata         = new ValidationMetadata
        {
            ProviderName = "Smarty",
            ValidatedAt  = DateTimeOffset.UtcNow,
            CacheSource  = "PROVIDER",
            ApiVersion   = "1.0",
        },
    };

    public ValidateBatchHandlerTests()
    {
        _l1       = Substitute.For<ICacheService<ValidationResponse>>();
        _l2       = Substitute.For<ICacheService<ValidationResponse>>();
        _provider = Substitute.For<IAddressValidationProvider>();
        _audit    = Substitute.For<IAuditEventStore>();

        _l1.GetAsync(Arg.Any<string>()).Returns((ValidationResponse?)null);
        _l2.GetAsync(Arg.Any<string>()).Returns((ValidationResponse?)null);
        _provider.ProviderName.Returns("Smarty");

        var logger = Substitute.For<ILogger<CacheOrchestrator<ValidationResponse>>>();
        _cache = new CacheOrchestrator<ValidationResponse>(_l1, _l2, logger);

        _sut = new ValidateBatchHandler(
            _cache,
            _provider,
            _audit,
            Substitute.For<ILogger<ValidateBatchHandler>>(),
            new AppMetrics());
    }

    // ── Constructor guards ───────────────────────────────────────────────────

    [Fact]
    public void Constructor_Should_Throw_When_Cache_Null()
    {
        Assert.Throws<ArgumentNullException>(() => new ValidateBatchHandler(
            null!,
            _provider,
            _audit,
            Substitute.For<ILogger<ValidateBatchHandler>>(),
            new AppMetrics()));
    }

    [Fact]
    public void Constructor_Should_Throw_When_Provider_Null()
    {
        Assert.Throws<ArgumentNullException>(() => new ValidateBatchHandler(
            _cache,
            null!,
            _audit,
            Substitute.For<ILogger<ValidateBatchHandler>>(),
            new AppMetrics()));
    }

    [Fact]
    public void Constructor_Should_Throw_When_Audit_Null()
    {
        Assert.Throws<ArgumentNullException>(() => new ValidateBatchHandler(
            _cache,
            _provider,
            null!,
            Substitute.For<ILogger<ValidateBatchHandler>>(),
            new AppMetrics()));
    }

    // ── All addresses validated via provider ─────────────────────────────────

    [Fact]
    public async Task HandleAsync_Should_Return_All_Validated_When_Provider_Succeeds()
    {
        _provider.ValidateAsync(Arg.Any<AddressInput>(), Arg.Any<CancellationToken>())
            .Returns(ci => MakeResponse(ci.Arg<AddressInput>().Street!));

        var response = await _sut.HandleAsync(MakeRequest(2), "corr-1");

        Assert.Equal(2, response.Summary.Total);
        Assert.Equal(2, response.Summary.Validated);
        Assert.Equal(0, response.Summary.Failed);
        Assert.Equal(2, response.Results.Length);
        Assert.All(response.Results, r => Assert.Equal("validated", r.Status));
    }

    // ── inputIndex order preserved ───────────────────────────────────────────

    [Fact]
    public async Task HandleAsync_Should_Preserve_InputIndex_Order()
    {
        _provider.ValidateAsync(Arg.Any<AddressInput>(), Arg.Any<CancellationToken>())
            .Returns(ci => MakeResponse(ci.Arg<AddressInput>().Street!));

        var response = await _sut.HandleAsync(MakeRequest(3), null);

        for (var i = 0; i < 3; i++)
            Assert.Equal(i, response.Results[i].InputIndex);
    }

    // ── Provider returns null → failed result ────────────────────────────────

    [Fact]
    public async Task HandleAsync_Should_Mark_Failed_When_Provider_Returns_Null()
    {
        _provider.ValidateAsync(Arg.Any<AddressInput>(), Arg.Any<CancellationToken>())
            .Returns((ValidationResponse?)null);

        var response = await _sut.HandleAsync(MakeRequest(1), null);

        Assert.Equal(1, response.Summary.Failed);
        Assert.Equal(0, response.Summary.Validated);
        Assert.Equal("failed", response.Results[0].Status);
    }

    // ── DPV N → failed result ────────────────────────────────────────────────

    [Fact]
    public async Task HandleAsync_Should_Mark_Failed_When_DPV_Is_N()
    {
        _provider.ValidateAsync(Arg.Any<AddressInput>(), Arg.Any<CancellationToken>())
            .Returns(MakeResponse("1 Main St", dpv: "N"));

        var response = await _sut.HandleAsync(MakeRequest(1), null);

        Assert.Equal(1, response.Summary.Failed);
        Assert.Equal("failed", response.Results[0].Status);
    }

    // ── 207 partial success ──────────────────────────────────────────────────

    [Fact]
    public async Task HandleAsync_Should_Return_Mixed_Results_When_Partial_Failure()
    {
        var request = new ValidateBatchRequest
        {
            Addresses =
            [
                new ValidateBatchItem { Street = "Good St", ZipCode = "90210" },
                new ValidateBatchItem { Street = "Bad St",  ZipCode = "00000" },
            ]
        };

        _provider.ValidateAsync(Arg.Is<AddressInput>(a => a.Street == "Good St"), Arg.Any<CancellationToken>())
            .Returns(MakeResponse("Good St", "Y"));
        _provider.ValidateAsync(Arg.Is<AddressInput>(a => a.Street == "Bad St"), Arg.Any<CancellationToken>())
            .Returns((ValidationResponse?)null);

        var response = await _sut.HandleAsync(request, null);

        Assert.Equal(1, response.Summary.Validated);
        Assert.Equal(1, response.Summary.Failed);
        Assert.Equal("validated", response.Results[0].Status);
        Assert.Equal("failed",    response.Results[1].Status);
    }

    // ── Summary stats ────────────────────────────────────────────────────────

    [Fact]
    public async Task HandleAsync_Should_Return_Correct_Summary_Stats()
    {
        _provider.ValidateAsync(Arg.Any<AddressInput>(), Arg.Any<CancellationToken>())
            .Returns(ci => MakeResponse(ci.Arg<AddressInput>().Street!));

        var response = await _sut.HandleAsync(MakeRequest(3), null);

        Assert.Equal(3, response.Summary.Total);
        Assert.Equal(3, response.Summary.Validated);
        Assert.Equal(0, response.Summary.Failed);
        Assert.Equal(3, response.Summary.CacheMisses);
        Assert.Equal(0, response.Summary.CacheHits);
        Assert.True(response.Summary.DurationMs >= 0);
    }

    // ── Provider exception is caught per-address ─────────────────────────────

    [Fact]
    public async Task HandleAsync_Should_Mark_Failed_When_Provider_Throws()
    {
        _provider.ValidateAsync(Arg.Any<AddressInput>(), Arg.Any<CancellationToken>())
            .ThrowsAsync(new HttpRequestException("Provider unavailable"));

        var response = await _sut.HandleAsync(MakeRequest(1), null);

        Assert.Equal(1, response.Summary.Failed);
        Assert.Equal("failed", response.Results[0].Status);
    }

    // ── Audit events ─────────────────────────────────────────────────────────

    [Fact]
    public async Task HandleAsync_Should_Emit_AddressValidated_Audit_Event()
    {
        _provider.ValidateAsync(Arg.Any<AddressInput>(), Arg.Any<CancellationToken>())
            .Returns(ci => MakeResponse(ci.Arg<AddressInput>().Street!));

        await _sut.HandleAsync(MakeRequest(1), "corr-audit");

        // Allow fire-and-forget audit tasks to complete
        await Task.Delay(50);

        await _audit.Received().AppendAsync(
            Arg.Is<DomainEvent>(e => e is AddressValidated),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task HandleAsync_Should_Emit_AddressValidationFailed_Audit_Event()
    {
        _provider.ValidateAsync(Arg.Any<AddressInput>(), Arg.Any<CancellationToken>())
            .Returns((ValidationResponse?)null);

        await _sut.HandleAsync(MakeRequest(1), "corr-fail");

        await Task.Delay(50);

        await _audit.Received().AppendAsync(
            Arg.Is<DomainEvent>(e => e is AddressValidationFailed),
            Arg.Any<CancellationToken>());
    }
}
