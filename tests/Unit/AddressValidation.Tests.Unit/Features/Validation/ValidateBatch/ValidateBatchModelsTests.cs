namespace AddressValidation.Tests.Unit.Features.Validation.ValidateBatch;

using AddressValidation.Api.Domain;
using AddressValidation.Api.Features.Validation.ValidateBatch;
using Xunit;

/// <summary>
/// Unit tests for <see cref="ValidateBatchItem"/>, <see cref="ValidateBatchResultItem"/>,
/// <see cref="ValidateBatchSummary"/>, and <see cref="ValidateBatchResponse"/> mapping.
/// </summary>
public class ValidateBatchModelsTests
{
    // ── ValidateBatchItem.ToAddressInput ─────────────────────────────────────

    [Fact]
    public void ToAddressInput_Should_Map_All_Fields()
    {
        var item = new ValidateBatchItem
        {
            Street  = "1 Infinite Loop",
            Street2 = "Apt 2",
            City    = "Cupertino",
            State   = "CA",
            ZipCode = "95014",
        };

        var input = item.ToAddressInput();

        Assert.Equal("1 Infinite Loop", input.Street);
        Assert.Equal("Apt 2", input.Street2);
        Assert.Equal("Cupertino", input.City);
        Assert.Equal("CA", input.State);
        Assert.Equal("95014", input.ZipCode);
    }

    [Fact]
    public void ToAddressInput_Should_Concatenate_ZipPlus4_When_Both_Present()
    {
        var item = new ValidateBatchItem { Street = "123 Main St", ZipCode = "90210", Plus4 = "1234" };
        var input = item.ToAddressInput();
        Assert.Equal("90210-1234", input.ZipCode);
    }

    [Fact]
    public void ToAddressInput_Should_Use_ZipCode_Only_When_Plus4_Null()
    {
        var item = new ValidateBatchItem { Street = "123 Main St", ZipCode = "90210" };
        var input = item.ToAddressInput();
        Assert.Equal("90210", input.ZipCode);
    }

    [Fact]
    public void ToAddressInput_Should_Return_Null_ZipCode_When_Neither_Present()
    {
        var item = new ValidateBatchItem { Street = "123 Main St", City = "Austin", State = "TX" };
        var input = item.ToAddressInput();
        Assert.Null(input.ZipCode);
    }

    // ── ValidateBatchResultItem.FromDomain ───────────────────────────────────

    [Fact]
    public void FromDomain_Should_Map_All_Fields_Correctly()
    {
        var domain = MakeDomainResponse("Y");
        var item = ValidateBatchResultItem.FromDomain(3, domain);

        Assert.Equal(3, item.InputIndex);
        Assert.Equal("1 Main St", item.Input.Street);
        Assert.Equal("Y", item.Analysis?.DpvMatchCode);
        Assert.Equal("validated", item.Status);
        Assert.Null(item.Error);
    }

    [Fact]
    public void FromDomain_Should_Map_Optional_Geocoding_When_Present()
    {
        var domain = MakeDomainResponse("S");
        domain.Geocoding = new GeocodingResult { Latitude = 30.1, Longitude = -97.8 };
        var item = ValidateBatchResultItem.FromDomain(0, domain);

        Assert.NotNull(item.Geocoding);
        Assert.Equal(30.1, item.Geocoding.Latitude);
    }

    // ── ValidateBatchResultItem.Failed ───────────────────────────────────────

    [Fact]
    public void Failed_Should_Set_Status_And_Error()
    {
        var input = new AddressInput { Street = "Unknown St", ZipCode = "00000" };
        var item = ValidateBatchResultItem.Failed(2, input, "Undeliverable");

        Assert.Equal(2, item.InputIndex);
        Assert.Equal("failed", item.Status);
        Assert.Equal("Undeliverable", item.Error);
        Assert.Null(item.Address);
    }

    [Fact]
    public void Failed_Should_Preserve_InputIndex()
    {
        var input = new AddressInput { Street = "X St", ZipCode = "11111" };
        var item = ValidateBatchResultItem.Failed(99, input, "ProviderNoMatch");
        Assert.Equal(99, item.InputIndex);
    }

    // ── ValidateBatchSummary ─────────────────────────────────────────────────

    [Fact]
    public void BatchSummary_Should_Hold_All_Stats()
    {
        var summary = new ValidateBatchSummary
        {
            Total       = 5,
            Validated   = 3,
            Failed      = 2,
            CacheHits   = 2,
            CacheMisses = 3,
            DurationMs  = 150,
        };

        Assert.Equal(5, summary.Total);
        Assert.Equal(3, summary.Validated);
        Assert.Equal(2, summary.Failed);
        Assert.Equal(2, summary.CacheHits);
        Assert.Equal(3, summary.CacheMisses);
        Assert.Equal(150, summary.DurationMs);
    }

    // ── ValidateBatchResponse ────────────────────────────────────────────────

    [Fact]
    public void BatchResponse_Should_Contain_Results_And_Summary()
    {
        var input = new AddressInput { Street = "1 Main St", ZipCode = "90210" };
        var results = new[] { ValidateBatchResultItem.Failed(0, input, "test") };
        var summary = new ValidateBatchSummary { Total = 1, Validated = 0, Failed = 1, CacheHits = 0, CacheMisses = 1, DurationMs = 10 };
        var response = new ValidateBatchResponse { Results = results, Summary = summary };

        Assert.Single(response.Results);
        Assert.Equal(1, response.Summary.Total);
    }

    // ── Helpers ──────────────────────────────────────────────────────────────

    private static ValidationResponse MakeDomainResponse(string dpv) => new()
    {
        InputAddress     = new AddressInput { Street = "1 Main St", ZipCode = "90210" },
        ValidatedAddress = new ValidatedAddress { DeliveryLine1 = "1 Main St" },
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
}
