namespace AddressValidation.Tests.Integration.Pipeline;

using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using AddressValidation.Api.Domain;
using AddressValidation.Api.Infrastructure.Providers;
using NSubstitute;
using Xunit;

/// <summary>
/// Full HTTP pipeline integration tests via <see cref="ApiWebApplicationFactory"/> (issue #120).
/// Validates routing, middleware (auth, versioning, exception handling), and response shapes
/// without requiring real external dependencies.
/// </summary>
public sealed class HttpPipelineIntegrationTests : IAsyncLifetime
{
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

    // ── Health checks ─────────────────────────────────────────────────────

    [Fact]
    public async Task HealthLive_Returns200()
    {
        var response = await _client.GetAsync("/health/live");
        response.StatusCode.ShouldBe(HttpStatusCode.OK);
    }

    [Fact]
    public async Task HealthLive_DoesNotRequireApiVersion()
    {
        using var client = _factory.CreateClient();
        var response = await client.GetAsync("/health/live");
        response.StatusCode.ShouldBe(HttpStatusCode.OK);
    }

    // ── Authentication ────────────────────────────────────────────────────

    [Fact]
    public async Task ValidateSingle_WithoutApiKey_IsAnonymousAndAllowed()
    {
        // The validate endpoint does not require authentication (it is publicly accessible).
        // Requests without an API key still reach the handler; provider returns null → 404.
        _factory.ProviderSubstitute
            .ValidateAsync(Arg.Any<AddressInput>(), Arg.Any<CancellationToken>())
            .Returns((ValidationResponse?)null);

        using var unauthClient = _factory.CreateClient();
        unauthClient.DefaultRequestHeaders.Add("Api-Version", "1.0");

        var response = await unauthClient.PostAsJsonAsync("/api/addresses/validate",
            new { street = "123 Main St", city = "Springfield", state = "IL" });

        // 404 = reached the handler (anonymous access is allowed)
        response.StatusCode.ShouldBe(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task ValidateSingle_WithInvalidApiKey_StillReachesHandler()
    {
        // API key authentication failure results in an anonymous request context;
        // the endpoint itself is public so the request still reaches the handler.
        _factory.ProviderSubstitute
            .ValidateAsync(Arg.Any<AddressInput>(), Arg.Any<CancellationToken>())
            .Returns((ValidationResponse?)null);

        using var badClient = _factory.CreateClient();
        badClient.DefaultRequestHeaders.Add("X-Api-Key", "invalid-key");
        badClient.DefaultRequestHeaders.Add("Api-Version", "1.0");

        var response = await badClient.PostAsJsonAsync("/api/addresses/validate",
            new { street = "123 Main St", city = "Springfield", state = "IL" });

        response.StatusCode.ShouldBe(HttpStatusCode.NotFound);
    }

    // ── API versioning ────────────────────────────────────────────────────

    [Fact]
    public async Task ValidateSingle_WithoutApiVersionHeader_Returns400()
    {
        using var client = _factory.CreateClient();
        client.DefaultRequestHeaders.Add("X-Api-Key", "test-readonly-key");
        // Deliberately omit Api-Version header

        var response = await client.PostAsJsonAsync("/api/addresses/validate",
            new { street = "123 Main St", city = "Springfield", state = "IL" });

        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
    }

    // ── Validation ────────────────────────────────────────────────────────

    [Fact]
    public async Task ValidateSingle_WithInvalidRequest_MissingStreet_Returns400()
    {
        var response = await _client.PostAsJsonAsync("/api/addresses/validate",
            new { city = "Springfield", state = "IL" });

        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        response.Content.Headers.ContentType?.MediaType.ShouldBe("application/problem+json");
    }

    [Fact]
    public async Task ValidateSingle_WithInvalidRequest_NoLocation_Returns400()
    {
        var response = await _client.PostAsJsonAsync("/api/addresses/validate",
            new { street = "123 Main St" });

        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
    }

    // ── Happy path ────────────────────────────────────────────────────────

    [Fact]
    public async Task ValidateSingle_WhenProviderReturnsNull_Returns404()
    {
        _factory.ProviderSubstitute
            .ValidateAsync(Arg.Any<AddressInput>(), Arg.Any<CancellationToken>())
            .Returns((ValidationResponse?)null);

        var response = await _client.PostAsJsonAsync("/api/addresses/validate",
            new { street = "123 Main St", city = "Springfield", state = "IL" });

        response.StatusCode.ShouldBe(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task ValidateSingle_WhenProviderReturnsResult_Returns200WithBody()
    {
        var validationResponse = new ValidationResponse
        {
            InputAddress = new AddressInput { Street = "123 Main St", City = "Springfield", State = "IL" },
            Status = "validated",
            ValidatedAddress = new ValidatedAddress { DeliveryLine1 = "123 Main St", LastLine = "Springfield IL 62701" },
            Metadata = new ValidationMetadata
            {
                ProviderName = "Smarty",
                ValidatedAt = DateTimeOffset.UtcNow,
                CacheSource = "PROVIDER",
                ApiVersion = "1.0",
            },
        };

        _factory.ProviderSubstitute
            .ValidateAsync(Arg.Any<AddressInput>(), Arg.Any<CancellationToken>())
            .Returns(validationResponse);

        var response = await _client.PostAsJsonAsync("/api/addresses/validate",
            new { street = "123 Main St", city = "Springfield", state = "IL" });

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        response.Content.Headers.ContentType?.MediaType.ShouldBe("application/json");

        var body = await response.Content.ReadAsStringAsync();
        body.ShouldNotBeNullOrWhiteSpace();
        using var doc = JsonDocument.Parse(body);
        // Response shape: { input, address, analysis, geocoding, metadata }
        doc.RootElement.TryGetProperty("metadata", out var metadata).ShouldBeTrue();
        metadata.GetProperty("cacheSource").GetString().ShouldBe("PROVIDER");
    }

    // ── Response headers ──────────────────────────────────────────────────

    [Fact]
    public async Task ValidateSingle_Response_IncludesCorrelationIdHeader()
    {
        _factory.ProviderSubstitute
            .ValidateAsync(Arg.Any<AddressInput>(), Arg.Any<CancellationToken>())
            .Returns((ValidationResponse?)null);

        var response = await _client.PostAsJsonAsync("/api/addresses/validate",
            new { street = "123 Main St", city = "Springfield", state = "IL" });

        response.Headers.Contains("X-Correlation-ID").ShouldBeTrue();
    }

    [Fact]
    public async Task ValidateSingle_Response_IncludesSecurityHeaders()
    {
        var response = await _client.GetAsync("/health/live");

        response.Headers.Contains("X-Content-Type-Options").ShouldBeTrue();
        response.Headers.GetValues("X-Content-Type-Options").First().ShouldBe("nosniff");
    }

    // ── Batch endpoint ────────────────────────────────────────────────────

    [Fact]
    public async Task ValidateBatch_WithEmptyArray_Returns400()
    {
        var response = await _client.PostAsJsonAsync("/api/addresses/validate/batch",
            new { addresses = Array.Empty<object>() });

        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task ValidateBatch_WithValidAddresses_Returns200()
    {
        var validationResponse = new ValidationResponse
        {
            InputAddress = new AddressInput { Street = "456 Oak Ave", City = "Chicago", State = "IL" },
            Status = "validated",
            ValidatedAddress = new ValidatedAddress { DeliveryLine1 = "456 Oak Ave", LastLine = "Chicago IL 60601" },
            Metadata = new ValidationMetadata
            {
                ProviderName = "Smarty",
                ValidatedAt = DateTimeOffset.UtcNow,
                CacheSource = "PROVIDER",
                ApiVersion = "1.0",
            },
        };

        _factory.ProviderSubstitute
            .ValidateAsync(Arg.Any<AddressInput>(), Arg.Any<CancellationToken>())
            .Returns(validationResponse);

        var payload = new
        {
            addresses = new[]
            {
                new { street = "456 Oak Ave", city = "Chicago", state = "IL" }
            }
        };

        var response = await _client.PostAsJsonAsync("/api/addresses/validate/batch", payload);

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
    }

    // ── Error handling ────────────────────────────────────────────────────

    [Fact]
    public async Task ValidateSingle_WhenProviderThrows_Returns500WithProblemDetails()
    {
        _factory.ProviderSubstitute
            .ValidateAsync(Arg.Any<AddressInput>(), Arg.Any<CancellationToken>())
            .Returns<ValidationResponse?>(_ => throw new InvalidOperationException("Provider exploded"));

        var response = await _client.PostAsJsonAsync("/api/addresses/validate",
            new { street = "123 Main St", city = "Springfield", state = "IL" });

        response.StatusCode.ShouldBe(HttpStatusCode.InternalServerError);
        response.Content.Headers.ContentType?.MediaType.ShouldBe("application/problem+json");

        var body = await response.Content.ReadAsStringAsync();
        using var doc = JsonDocument.Parse(body);
        doc.RootElement.TryGetProperty("traceId", out _).ShouldBeTrue();
    }

    // ── Metrics endpoint ──────────────────────────────────────────────────

    [Fact]
    public async Task Metrics_Endpoint_Returns200()
    {
        using var anonClient = _factory.CreateClient();
        var response = await anonClient.GetAsync("/metrics");
        response.StatusCode.ShouldBe(HttpStatusCode.OK);
    }
}
