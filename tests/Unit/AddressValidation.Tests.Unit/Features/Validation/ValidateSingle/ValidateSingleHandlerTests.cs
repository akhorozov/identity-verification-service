namespace AddressValidation.Tests.Unit.Features.Validation.ValidateSingle;

using AddressValidation.Api.Domain;
using AddressValidation.Api.Domain.Events;
using AddressValidation.Api.Features.Validation.ValidateSingle;
using AddressValidation.Api.Infrastructure.Metrics;
using AddressValidation.Api.Infrastructure.Providers;
using AddressValidation.Api.Infrastructure.Services.Audit;
using AddressValidation.Api.Infrastructure.Services.Caching;
using Microsoft.Extensions.Logging;
using NSubstitute;
using NSubstitute.ExceptionExtensions;
using Xunit;

/// <summary>
/// Unit tests for <see cref="ValidateSingleHandler"/>.
/// </summary>
public class ValidateSingleHandlerTests
{
    private readonly CacheOrchestrator<ValidationResponse> _cache;
    private readonly IAddressValidationProvider _provider;
    private readonly IAuditEventStore _audit;
    private readonly ValidateSingleHandler _sut;

    private static readonly ValidateSingleRequest ValidRequest = new()
    {
        Street  = "1 Infinite Loop",
        City    = "Cupertino",
        State   = "CA",
        ZipCode = "95014"
    };

    private static ValidationResponse MakeResponse(string dpv = "Y") => new()
    {
        InputAddress = new AddressInput { Street = "1 Infinite Loop", ZipCode = "95014" },
        ValidatedAddress = new ValidatedAddress { DeliveryLine1 = "1 Infinite Loop" },
        Analysis  = new AddressAnalysis { DpvMatchCode = dpv },
        Status    = dpv == "N" ? "undeliverable" : "validated",
        Metadata  = new ValidationMetadata
        {
            ProviderName = "Smarty",
            ValidatedAt  = DateTimeOffset.UtcNow,
            CacheSource  = "PROVIDER",
            ApiVersion   = "1.0"
        }
    };

    public ValidateSingleHandlerTests()
    {
        // CacheOrchestrator needs two ICacheService<ValidationResponse> instances
        var l1 = Substitute.For<ICacheService<ValidationResponse>>();
        var l2 = Substitute.For<ICacheService<ValidationResponse>>();
        var orchLogger = Substitute.For<ILogger<CacheOrchestrator<ValidationResponse>>>();
        _cache = new CacheOrchestrator<ValidationResponse>(l1, l2, orchLogger);

        _provider = Substitute.For<IAddressValidationProvider>();
        _provider.ProviderName.Returns("Smarty");

        _audit = Substitute.For<IAuditEventStore>();

        var handlerLogger = Substitute.For<ILogger<ValidateSingleHandler>>();
        _sut = new ValidateSingleHandler(_cache, _provider, _audit, handlerLogger, new AppMetrics());
    }

    // ── Success paths ─────────────────────────────────────────────────────────

    [Fact]
    public async Task HandleAsync_ProviderReturnsValidatedY_ReturnsResult()
    {
        _provider.ValidateAsync(Arg.Any<AddressInput>(), Arg.Any<CancellationToken>())
            .Returns(MakeResponse("Y"));

        var result = await _sut.HandleAsync(ValidRequest, "corr-1");

        Assert.NotNull(result);
        Assert.Equal("PROVIDER", result.CacheSource);
        Assert.False(result.IsStale);
    }

    [Theory]
    [InlineData("S")]
    [InlineData("D")]
    public async Task HandleAsync_DpvSOrD_ReturnsResultWithWarning(string dpv)
    {
        _provider.ValidateAsync(Arg.Any<AddressInput>(), Arg.Any<CancellationToken>())
            .Returns(MakeResponse(dpv));

        var result = await _sut.HandleAsync(ValidRequest, null);

        Assert.NotNull(result);
        Assert.Equal(dpv, result.Response.Analysis?.DpvMatchCode);
    }

    [Fact]
    public async Task HandleAsync_Success_EmitsAddressValidatedAuditEvent()
    {
        _provider.ValidateAsync(Arg.Any<AddressInput>(), Arg.Any<CancellationToken>())
            .Returns(MakeResponse("Y"));

        await _sut.HandleAsync(ValidRequest, "corr-1");

        await _audit.Received(1).AppendAsync(
            Arg.Is<DomainEvent>(e => e.EventType == "AddressValidated"),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task HandleAsync_ProviderHit_EmitsCacheEntryCreatedAuditEvent()
    {
        _provider.ValidateAsync(Arg.Any<AddressInput>(), Arg.Any<CancellationToken>())
            .Returns(MakeResponse("Y"));

        await _sut.HandleAsync(ValidRequest, "corr-1");

        await _audit.Received(1).AppendAsync(
            Arg.Is<DomainEvent>(e => e.EventType == "CacheEntryCreated"),
            Arg.Any<CancellationToken>());
    }

    // ── Failure paths ─────────────────────────────────────────────────────────

    [Fact]
    public async Task HandleAsync_DpvN_ReturnsNull()
    {
        _provider.ValidateAsync(Arg.Any<AddressInput>(), Arg.Any<CancellationToken>())
            .Returns(MakeResponse("N"));

        var result = await _sut.HandleAsync(ValidRequest, null);

        Assert.Null(result);
    }

    [Fact]
    public async Task HandleAsync_DpvN_EmitsAddressValidationFailedAuditEvent()
    {
        _provider.ValidateAsync(Arg.Any<AddressInput>(), Arg.Any<CancellationToken>())
            .Returns(MakeResponse("N"));

        await _sut.HandleAsync(ValidRequest, "corr-1");

        await _audit.Received().AppendAsync(
            Arg.Is<DomainEvent>(e => e.EventType == "AddressValidationFailed"),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task HandleAsync_ProviderReturnsNull_ReturnsNull()
    {
        _provider.ValidateAsync(Arg.Any<AddressInput>(), Arg.Any<CancellationToken>())
            .Returns((ValidationResponse?)null);

        var result = await _sut.HandleAsync(ValidRequest, null);

        Assert.Null(result);
    }

    [Fact]
    public async Task HandleAsync_ProviderThrows_PropagatesException()
    {
        _provider.ValidateAsync(Arg.Any<AddressInput>(), Arg.Any<CancellationToken>())
            .ThrowsAsync(new HttpRequestException("Provider unavailable"));

        await Assert.ThrowsAsync<HttpRequestException>(
            () => _sut.HandleAsync(ValidRequest, null));
    }

    [Fact]
    public async Task HandleAsync_ProviderThrows_EmitsFailedAuditEvent()
    {
        _provider.ValidateAsync(Arg.Any<AddressInput>(), Arg.Any<CancellationToken>())
            .ThrowsAsync(new HttpRequestException("timeout"));

        try { await _sut.HandleAsync(ValidRequest, null); } catch { }

        await _audit.Received().AppendAsync(
            Arg.Is<DomainEvent>(e => e.EventType == "AddressValidationFailed"),
            Arg.Any<CancellationToken>());
    }

    // ── Null guard ────────────────────────────────────────────────────────────

    [Fact]
    public void Constructor_NullCache_Throws()
        => Assert.Throws<ArgumentNullException>(() =>
            new ValidateSingleHandler(null!, _provider, _audit,
                Substitute.For<ILogger<ValidateSingleHandler>>(), new AppMetrics()));

    [Fact]
    public void Constructor_NullProvider_Throws()
        => Assert.Throws<ArgumentNullException>(() =>
            new ValidateSingleHandler(_cache, null!, _audit,
                Substitute.For<ILogger<ValidateSingleHandler>>(), new AppMetrics()));

    [Fact]
    public void Constructor_NullAudit_Throws()
        => Assert.Throws<ArgumentNullException>(() =>
            new ValidateSingleHandler(_cache, _provider, null!,
                Substitute.For<ILogger<ValidateSingleHandler>>(), new AppMetrics()));
}
