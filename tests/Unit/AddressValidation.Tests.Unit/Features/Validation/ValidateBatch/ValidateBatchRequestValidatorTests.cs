namespace AddressValidation.Tests.Unit.Features.Validation.ValidateBatch;

using AddressValidation.Api.Domain;
using AddressValidation.Api.Features.Validation.ValidateBatch;
using FluentValidation.TestHelper;
using Xunit;

/// <summary>
/// Unit tests for <see cref="ValidateBatchRequestValidator"/>.
/// </summary>
public class ValidateBatchRequestValidatorTests
{
    private readonly ValidateBatchRequestValidator _sut = new();

    private static ValidateBatchItem ValidItem(string street = "123 Main St", string zip = "90210") => new()
    {
        Street  = street,
        ZipCode = zip,
    };

    // ── Array-level rules ────────────────────────────────────────────────────

    [Fact]
    public void Should_Pass_For_Valid_Single_Item()
    {
        var request = new ValidateBatchRequest { Addresses = [ValidItem()] };
        var result = _sut.TestValidate(request);
        result.ShouldNotHaveAnyValidationErrors();
    }

    [Fact]
    public void Should_Pass_For_100_Items()
    {
        var addresses = Enumerable.Range(0, 100).Select(_ => ValidItem()).ToArray();
        var request = new ValidateBatchRequest { Addresses = addresses };
        var result = _sut.TestValidate(request);
        result.ShouldNotHaveAnyValidationErrors();
    }

    [Fact]
    public void Should_Fail_When_Addresses_Empty()
    {
        var request = new ValidateBatchRequest { Addresses = [] };
        var result = _sut.TestValidate(request);
        result.ShouldHaveValidationErrorFor(x => x.Addresses);
    }

    [Fact]
    public void Should_Fail_When_Over_100_Items()
    {
        var addresses = Enumerable.Range(0, 101).Select(_ => ValidItem()).ToArray();
        var request = new ValidateBatchRequest { Addresses = addresses };
        var result = _sut.TestValidate(request);
        result.ShouldHaveValidationErrorFor(x => x.Addresses);
    }

    // ── Per-item street rules ────────────────────────────────────────────────

    [Fact]
    public void Should_Fail_When_Item_Street_Empty()
    {
        var request = new ValidateBatchRequest { Addresses = [ValidItem(street: "")] };
        var result = _sut.TestValidate(request);
        result.ShouldHaveAnyValidationError();
    }

    [Fact]
    public void Should_Fail_When_Item_Street_Too_Long()
    {
        var request = new ValidateBatchRequest { Addresses = [ValidItem(street: new string('A', 101))] };
        var result = _sut.TestValidate(request);
        result.ShouldHaveAnyValidationError();
    }

    // ── Per-item state rules ─────────────────────────────────────────────────

    [Fact]
    public void Should_Fail_When_Item_State_Invalid()
    {
        var item = new ValidateBatchItem { Street = "123 Main St", City = "Nowhere", State = "XX" };
        var request = new ValidateBatchRequest { Addresses = [item] };
        var result = _sut.TestValidate(request);
        result.ShouldHaveAnyValidationError();
    }

    [Theory]
    [InlineData("CA")]
    [InlineData("TX")]
    [InlineData("PR")]
    [InlineData("DC")]
    public void Should_Pass_For_Valid_State_Abbreviations(string state)
    {
        var item = new ValidateBatchItem { Street = "123 Main St", City = "Anytown", State = state };
        var request = new ValidateBatchRequest { Addresses = [item] };
        var result = _sut.TestValidate(request);
        result.ShouldNotHaveAnyValidationErrors();
    }

    // ── Per-item ZIP rules ───────────────────────────────────────────────────

    [Theory]
    [InlineData("12345")]
    [InlineData("12345-6789")]
    public void Should_Pass_For_Valid_ZipCode_Formats(string zip)
    {
        var request = new ValidateBatchRequest { Addresses = [ValidItem(zip: zip)] };
        var result = _sut.TestValidate(request);
        result.ShouldNotHaveAnyValidationErrors();
    }

    [Theory]
    [InlineData("1234")]
    [InlineData("abcde")]
    [InlineData("12345-678")]
    public void Should_Fail_For_Invalid_ZipCode_Formats(string zip)
    {
        var request = new ValidateBatchRequest { Addresses = [ValidItem(zip: zip)] };
        var result = _sut.TestValidate(request);
        result.ShouldHaveAnyValidationError();
    }

    // ── Location presence rule ───────────────────────────────────────────────

    [Fact]
    public void Should_Fail_When_No_Location_Provided()
    {
        var item = new ValidateBatchItem { Street = "123 Main St" };
        var request = new ValidateBatchRequest { Addresses = [item] };
        var result = _sut.TestValidate(request);
        result.ShouldHaveAnyValidationError();
    }

    [Fact]
    public void Should_Pass_When_City_And_State_Provided()
    {
        var item = new ValidateBatchItem { Street = "123 Main St", City = "Austin", State = "TX" };
        var request = new ValidateBatchRequest { Addresses = [item] };
        var result = _sut.TestValidate(request);
        result.ShouldNotHaveAnyValidationErrors();
    }
}
