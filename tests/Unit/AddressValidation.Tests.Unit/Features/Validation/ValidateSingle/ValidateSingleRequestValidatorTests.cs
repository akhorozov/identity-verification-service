namespace AddressValidation.Tests.Unit.Features.Validation.ValidateSingle;

using AddressValidation.Api.Features.Validation.ValidateSingle;
using FluentValidation.TestHelper;
using Xunit;

/// <summary>
/// Unit tests for <see cref="ValidateSingleRequestValidator"/>.
/// SRS Ref: FR-001, Section 9.3.1 — Input validation rules
/// </summary>
public class ValidateSingleRequestValidatorTests
{
    private readonly ValidateSingleRequestValidator _sut = new();

    // ── Street ───────────────────────────────────────────────────────────────

    [Fact]
    public void Street_Empty_FailsValidation()
    {
        var result = _sut.TestValidate(new ValidateSingleRequest { Street = "", ZipCode = "12345" });
        result.ShouldHaveValidationErrorFor(x => x.Street);
    }

    [Fact]
    public void Street_Over100Chars_FailsValidation()
    {
        var result = _sut.TestValidate(new ValidateSingleRequest
        {
            Street = new string('A', 101),
            ZipCode = "12345"
        });
        result.ShouldHaveValidationErrorFor(x => x.Street);
    }

    [Fact]
    public void Street_Valid_PassesValidation()
    {
        var result = _sut.TestValidate(new ValidateSingleRequest
        {
            Street = "123 Main St",
            ZipCode = "12345"
        });
        result.ShouldNotHaveValidationErrorFor(x => x.Street);
    }

    // ── State ─────────────────────────────────────────────────────────────────

    [Theory]
    [InlineData("ZZ")]
    [InlineData("XX")]
    [InlineData("A")]
    [InlineData("ABC")]
    public void State_Invalid_FailsValidation(string state)
    {
        var result = _sut.TestValidate(new ValidateSingleRequest
        {
            Street = "123 Main St",
            City = "Springfield",
            State = state
        });
        result.ShouldHaveValidationErrorFor(x => x.State);
    }

    [Theory]
    [InlineData("CA")]
    [InlineData("NY")]
    [InlineData("TX")]
    [InlineData("DC")]
    [InlineData("PR")]
    public void State_Valid_PassesValidation(string state)
    {
        var result = _sut.TestValidate(new ValidateSingleRequest
        {
            Street = "123 Main St",
            City = "Springfield",
            State = state
        });
        result.ShouldNotHaveValidationErrorFor(x => x.State);
    }

    // ── ZipCode ───────────────────────────────────────────────────────────────

    [Theory]
    [InlineData("1234")]
    [InlineData("123456")]
    [InlineData("ABCDE")]
    [InlineData("12345-678")]
    public void ZipCode_InvalidFormat_FailsValidation(string zip)
    {
        var result = _sut.TestValidate(new ValidateSingleRequest
        {
            Street = "123 Main St",
            ZipCode = zip
        });
        result.ShouldHaveValidationErrorFor(x => x.ZipCode);
    }

    [Theory]
    [InlineData("12345")]
    [InlineData("12345-6789")]
    public void ZipCode_ValidFormat_PassesValidation(string zip)
    {
        var result = _sut.TestValidate(new ValidateSingleRequest
        {
            Street = "123 Main St",
            ZipCode = zip
        });
        result.ShouldNotHaveValidationErrorFor(x => x.ZipCode);
    }

    // ── Location rule ─────────────────────────────────────────────────────────

    [Fact]
    public void NeitherCityStateNorZipCode_FailsValidation()
    {
        var result = _sut.TestValidate(new ValidateSingleRequest { Street = "123 Main St" });
        result.ShouldHaveAnyValidationError();
    }

    [Fact]
    public void CityWithoutState_FailsValidation()
    {
        var result = _sut.TestValidate(new ValidateSingleRequest
        {
            Street = "123 Main St",
            City = "Springfield"
        });
        result.ShouldHaveAnyValidationError();
    }

    [Fact]
    public void CityAndState_PassesLocationRule()
    {
        var result = _sut.TestValidate(new ValidateSingleRequest
        {
            Street = "123 Main St",
            City = "Springfield",
            State = "IL"
        });
        result.ShouldNotHaveAnyValidationErrors();
    }

    [Fact]
    public void ZipCodeOnly_PassesLocationRule()
    {
        var result = _sut.TestValidate(new ValidateSingleRequest
        {
            Street = "123 Main St",
            ZipCode = "62701"
        });
        result.ShouldNotHaveAnyValidationErrors();
    }

    // ── Plus4 ─────────────────────────────────────────────────────────────────

    [Theory]
    [InlineData("123")]
    [InlineData("12345")]
    [InlineData("ABCD")]
    public void Plus4_InvalidFormat_FailsValidation(string plus4)
    {
        var result = _sut.TestValidate(new ValidateSingleRequest
        {
            Street = "123 Main St",
            ZipCode = "62701",
            Plus4 = plus4
        });
        result.ShouldHaveValidationErrorFor(x => x.Plus4);
    }

    [Fact]
    public void Plus4_ValidFormat_PassesValidation()
    {
        var result = _sut.TestValidate(new ValidateSingleRequest
        {
            Street = "123 Main St",
            ZipCode = "62701",
            Plus4 = "1234"
        });
        result.ShouldNotHaveValidationErrorFor(x => x.Plus4);
    }
}
