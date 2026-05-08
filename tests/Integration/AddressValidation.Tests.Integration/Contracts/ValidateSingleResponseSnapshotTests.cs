namespace AddressValidation.Tests.Integration.Contracts;

using System.Net;
using System.Net.Http.Json;
using System.Text.RegularExpressions;
using AddressValidation.Api.Domain;
using NSubstitute;
using Xunit;

/// <summary>
/// Snapshot / contract tests for the single-address validation API response shape (issues #114, #124).
/// Uses Verify to detect unintended breaking changes to the JSON contract.
/// Snapshots are auto-created on first run and committed to source control.
/// </summary>
public sealed class ValidateSingleResponseSnapshotTests : IAsyncLifetime
{
    private static readonly Regex TraceIdPattern = new(@"""traceId""\s*:\s*""[^""]+""", RegexOptions.Compiled);
    private static readonly Regex DurationPattern = new(@"""requestDurationMs""\s*:\s*\d+", RegexOptions.Compiled);

    private ApiWebApplicationFactory _factory = null!;
    private HttpClient _client = null!;

    public Task InitializeAsync()
    {
        _factory = new ApiWebApplicationFactory();
        _client = _factory.CreateReadonlyClient();
        return Task.CompletedTask;
    }

    public async Task DisposeAsync()
    {
        _client.Dispose();
        await ((IAsyncDisposable)_factory).DisposeAsync();
    }

    // Scrub non-deterministic fields before snapshot comparison
    private string Scrub(string json) =>
        DurationPattern.Replace(
            TraceIdPattern.Replace(json, @"""traceId"": ""scrubbed"""),
            @"""requestDurationMs"": 0");

    private static ValidationResponse MakeValidatedResponse(string street, string city, string state, string zip) => new()
    {
        InputAddress = new AddressInput { Street = street, City = city, State = state, ZipCode = zip },
        Status = "validated",
        ValidatedAddress = new ValidatedAddress
        {
            DeliveryLine1 = street,
            LastLine = $"{city} {state} {zip}",
            CityName = city.ToUpperInvariant(),
            StateAbbreviation = state,
            ZipCode = zip,
        },
        Metadata = new ValidationMetadata
        {
            ProviderName = "Smarty",
            ValidatedAt = DateTimeOffset.Parse("2024-01-15T10:00:00Z"),
            CacheSource = "PROVIDER",
            ApiVersion = "1.0",
        },
    };

    [Fact]
    public async Task ValidateSingle_SuccessResponse_MatchesSnapshot()
    {
        _factory.ProviderSubstitute
            .ValidateAsync(Arg.Any<AddressInput>(), Arg.Any<CancellationToken>())
            .Returns(MakeValidatedResponse("1 Infinite Loop", "Cupertino", "CA", "95014"));

        var response = await _client.PostAsJsonAsync("/api/addresses/validate",
            new { street = "1 Infinite Loop", city = "Cupertino", state = "CA", zipCode = "95014" });

        response.StatusCode.ShouldBe(HttpStatusCode.OK);

        var body = Scrub(await response.Content.ReadAsStringAsync());
        await Verifier.VerifyJson(body);
    }

    [Fact]
    public async Task ValidateSingle_NotFoundResponse_MatchesSnapshot()
    {
        _factory.ProviderSubstitute
            .ValidateAsync(Arg.Any<AddressInput>(), Arg.Any<CancellationToken>())
            .Returns((ValidationResponse?)null);

        var response = await _client.PostAsJsonAsync("/api/addresses/validate",
            new { street = "999 Nowhere Dr", city = "Faketown", state = "TX" });

        response.StatusCode.ShouldBe(HttpStatusCode.NotFound);

        var body = Scrub(await response.Content.ReadAsStringAsync());
        await Verifier.VerifyJson(body);
    }

    [Fact]
    public async Task ValidateSingle_ValidationError_MatchesSnapshot()
    {
        // Street provided but no City+State or ZipCode — triggers FluentValidation 400 with problem+json body
        var response = await _client.PostAsJsonAsync("/api/addresses/validate",
            new { street = "123 Main St" });

        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);

        var body = Scrub(await response.Content.ReadAsStringAsync());
        await Verifier.VerifyJson(body);
    }
}
