namespace AddressValidation.Tests.Unit.Features.Validation.ValidateSingle;

using AddressValidation.Api.Domain;
using AddressValidation.Api.Features.Validation.ValidateSingle;
using Xunit;

/// <summary>
/// Unit tests for <see cref="ValidateSingleRequest"/> and <see cref="ValidateSingleResponse"/> models.
/// </summary>
public class ValidateSingleModelsTests
{
    // ── ValidateSingleRequest ─────────────────────────────────────────────────

    [Fact]
    public void ToAddressInput_MapsAllFields()
    {
        var request = new ValidateSingleRequest
        {
            Street  = "123 Main St",
            Street2 = "Suite 1",
            City    = "Springfield",
            State   = "IL",
            ZipCode = "62701",
            Plus4   = "1234"
        };

        var input = request.ToAddressInput();

        Assert.Equal("123 Main St", input.Street);
        Assert.Equal("Suite 1",     input.Street2);
        Assert.Equal("Springfield", input.City);
        Assert.Equal("IL",          input.State);
        Assert.Equal("62701-1234",  input.ZipCode);
    }

    [Fact]
    public void ToAddressInput_WithoutPlus4_MapsZipCodeOnly()
    {
        var request = new ValidateSingleRequest
        {
            Street  = "123 Main St",
            ZipCode = "62701"
        };

        var input = request.ToAddressInput();

        Assert.Equal("62701", input.ZipCode);
    }

    [Fact]
    public void ToAddressInput_NullZipCode_NullPlus4_ZipCodeIsNull()
    {
        var request = new ValidateSingleRequest
        {
            Street = "123 Main St",
            City   = "Springfield",
            State  = "IL"
        };

        var input = request.ToAddressInput();

        Assert.Null(input.ZipCode);
    }

    // ── ValidateSingleResponse ────────────────────────────────────────────────

    [Fact]
    public void FromDomain_MapsAllFields()
    {
        var domain = new ValidationResponse
        {
            InputAddress = new AddressInput { Street = "123 Main St", ZipCode = "62701" },
            ValidatedAddress = new ValidatedAddress
            {
                DeliveryLine1 = "123 Main St",
                LastLine      = "Springfield IL 62701-1234"
            },
            Analysis  = new AddressAnalysis { DpvMatchCode = "Y" },
            Geocoding = new GeocodingResult { Latitude = 39.78, Longitude = -89.65 },
            Status    = "validated",
            Metadata  = new ValidationMetadata
            {
                ProviderName  = "Smarty",
                ValidatedAt   = DateTimeOffset.UtcNow,
                CacheSource   = "L1",
                ApiVersion    = "1.0"
            }
        };

        var response = ValidateSingleResponse.FromDomain(domain);

        Assert.Equal(domain.InputAddress,     response.Input);
        Assert.Equal(domain.ValidatedAddress, response.Address);
        Assert.Equal(domain.Analysis,         response.Analysis);
        Assert.Equal(domain.Geocoding,        response.Geocoding);
        Assert.Equal(domain.Metadata,         response.Metadata);
    }

    [Fact]
    public void FromDomain_NullOptionals_MapsWithNulls()
    {
        var domain = new ValidationResponse
        {
            InputAddress = new AddressInput { Street = "123 Main St", ZipCode = "62701" },
            Status = "undeliverable",
            Metadata = new ValidationMetadata
            {
                ProviderName = "Smarty",
                ValidatedAt  = DateTimeOffset.UtcNow,
                CacheSource  = "PROVIDER",
                ApiVersion   = "1.0"
            }
        };

        var response = ValidateSingleResponse.FromDomain(domain);

        Assert.Null(response.Address);
        Assert.Null(response.Analysis);
        Assert.Null(response.Geocoding);
    }
}
