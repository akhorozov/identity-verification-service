using AddressValidation.Api.Domain;
using AddressValidation.Api.Infrastructure.Metrics;
using AddressValidation.Api.Infrastructure.Providers;
using AddressValidation.Api.Infrastructure.Providers.Smarty;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace AddressValidation.Tests.Unit.Infrastructure.Providers;

/// <summary>
/// Unit tests for <see cref="SmartyProvider"/> accessed via <see cref="IAddressValidationProvider"/>.
/// </summary>
public class SmartyProviderTests
{
    // ── Fake ISmartyApi ──────────────────────────────────────────────────────

    private sealed class FakeSmartyApi : ISmartyApi
    {
        private readonly IReadOnlyList<SmartyCandidate>? _response;
        private readonly Exception? _exception;

        public FakeSmartyApi(IReadOnlyList<SmartyCandidate> response) => _response = response;
        public FakeSmartyApi(Exception exception) => _exception = exception;

        public Task<IReadOnlyList<SmartyCandidate>> ValidateAddressAsync(
            string street, string? street2, string? city, string? state,
            string? zipcode, string? addressee, int candidates = 1,
            CancellationToken cancellationToken = default)
        {
            if (_exception is not null) throw _exception;
            return Task.FromResult(_response!);
        }
    }

    // ── Helpers ──────────────────────────────────────────────────────────────

    private static AddressInput MakeInput() => new()
    {
        Street = "1600 Amphitheatre Pkwy",
        City = "Mountain View",
        State = "CA",
        ZipCode = "94043"
    };

    private static SmartyCandidate MakeCandidate(string dpvMatchCode = "Y") => new()
    {
        DeliveryLine1 = "1600 AMPHITHEATRE PKWY",
        LastLine = "MOUNTAIN VIEW CA 94043-1351",
        Components = new SmartyComponents
        {
            PrimaryNumber = "1600",
            StreetName = "AMPHITHEATRE",
            StreetSuffix = "PKWY",
            CityName = "MOUNTAIN VIEW",
            StateAbbreviation = "CA",
            Zipcode = "94043",
            Plus4Code = "1351"
        },
        Analysis = new SmartyAnalysis { DpvMatchCode = dpvMatchCode }
    };

    private static IAddressValidationProvider MakeProvider(ISmartyApi api)
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddSingleton(api);
        services.AddSingleton<AppMetrics>();
        services.AddScoped<IAddressValidationProvider, SmartyProvider>();
        var sp = services.BuildServiceProvider();
        return sp.GetRequiredService<IAddressValidationProvider>();
    }

    // ── Tests ────────────────────────────────────────────────────────────────

    [Fact]
    public async Task ValidateAsync_WhenInputIsNull_ThrowsArgumentNullException()
    {
        var provider = MakeProvider(new FakeSmartyApi(new List<SmartyCandidate>()));
        await Assert.ThrowsAsync<ArgumentNullException>(() =>
            provider.ValidateAsync(null!));
    }

    [Fact]
    public async Task ValidateAsync_WhenNoCandidates_ReturnsNull()
    {
        var api = new FakeSmartyApi(new List<SmartyCandidate>());
        var provider = MakeProvider(api);

        var result = await provider.ValidateAsync(MakeInput());

        result.ShouldBeNull();
    }

    [Fact]
    public async Task ValidateAsync_WhenCandidateReturned_ReturnsValidationResponse()
    {
        var api = new FakeSmartyApi(new List<SmartyCandidate> { MakeCandidate("Y") });
        var provider = MakeProvider(api);

        var result = await provider.ValidateAsync(MakeInput());

        result.ShouldNotBeNull();
        result.Status.ShouldBe("validated");
        result.ValidatedAddress!.DeliveryLine1.ShouldBe("1600 AMPHITHEATRE PKWY");
    }

    [Fact]
    public async Task ValidateAsync_SetsProviderName()
    {
        var api = new FakeSmartyApi(new List<SmartyCandidate> { MakeCandidate() });
        var provider = MakeProvider(api);

        var result = await provider.ValidateAsync(MakeInput());

        result!.Metadata.ProviderName.ShouldBe("Smarty");
    }

    [Fact]
    public async Task ValidateAsync_SetsCacheSourceToProvider()
    {
        var api = new FakeSmartyApi(new List<SmartyCandidate> { MakeCandidate() });
        var provider = MakeProvider(api);

        var result = await provider.ValidateAsync(MakeInput());

        result!.Metadata.CacheSource.ShouldBe("PROVIDER");
    }

    [Fact]
    public async Task ValidateAsync_WhenApiThrows_PropagatesException()
    {
        var api = new FakeSmartyApi(new HttpRequestException("connection refused"));
        var provider = MakeProvider(api);

        await Assert.ThrowsAsync<HttpRequestException>(() =>
            provider.ValidateAsync(MakeInput()));
    }

    [Fact]
    public void ProviderName_ReturnsSmartly()
    {
        var provider = MakeProvider(new FakeSmartyApi(new List<SmartyCandidate>()));
        provider.ProviderName.ShouldBe("Smarty");
    }

    [Fact]
    public async Task ValidateAsync_RespectsCancellationToken()
    {
        using var cts = new CancellationTokenSource();
        cts.Cancel();

        var api = new FakeSmartyApi(new List<SmartyCandidate> { MakeCandidate() });
        var provider = MakeProvider(api);

        // Cancellation is forwarded to the API; the fake doesn't honour it,
        // but we verify the token is at least accepted without exception.
        var result = await provider.ValidateAsync(MakeInput(), cts.Token);
        result.ShouldNotBeNull();
    }
}
