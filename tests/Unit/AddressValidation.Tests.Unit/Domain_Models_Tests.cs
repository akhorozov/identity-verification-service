namespace AddressValidation.Tests.Unit;

using System.ComponentModel.DataAnnotations;
using AddressValidation.Api.Domain;
using Xunit;

/// <summary>
/// Unit tests for domain models (AddressInput, ValidatedAddress, ValidationResponse, etc.)
/// and address hashing/cache key generation.
/// </summary>
public class DomainModelsTests
{
    [Fact]
    public void AddressInput_WithValidCityAndState_PassesValidation()
    {
        // Arrange
        var address = new AddressInput
        {
            Street = "1600 Amphitheatre Pkwy",
            City = "Mountain View",
            State = "CA"
        };

        // Act
        var context = new ValidationContext(address);
        var results = new List<ValidationResult>();
        bool isValid = Validator.TryValidateObject(address, context, results, validateAllProperties: true);

        // Assert
        isValid.ShouldBeTrue();
        results.ShouldBeEmpty();
    }

    [Fact]
    public void AddressInput_WithValidZipCode_PassesValidation()
    {
        // Arrange
        var address = new AddressInput
        {
            Street = "1600 Amphitheatre Pkwy",
            ZipCode = "94043"
        };

        // Act
        var context = new ValidationContext(address);
        var results = new List<ValidationResult>();
        bool isValid = Validator.TryValidateObject(address, context, results, validateAllProperties: true);

        // Assert
        isValid.ShouldBeTrue();
        results.ShouldBeEmpty();
    }

    [Fact]
    public void AddressInput_WithZipCodePlusFour_PassesValidation()
    {
        // Arrange
        var address = new AddressInput
        {
            Street = "1600 Amphitheatre Pkwy",
            ZipCode = "94043-1351"
        };

        // Act
        var context = new ValidationContext(address);
        var results = new List<ValidationResult>();
        bool isValid = Validator.TryValidateObject(address, context, results, validateAllProperties: true);

        // Assert
        isValid.ShouldBeTrue();
        results.ShouldBeEmpty();
    }

    [Fact]
    public void AddressInput_WithoutCityStateOrZipCode_FailsValidation()
    {
        // Arrange
        var address = new AddressInput
        {
            Street = "1600 Amphitheatre Pkwy"
        };

        // Act
        var context = new ValidationContext(address);
        var results = new List<ValidationResult>();
        bool isValid = Validator.TryValidateObject(address, context, results, validateAllProperties: true);

        // Assert
        isValid.ShouldBeFalse();
        results.ShouldNotBeEmpty();
        var errorWithExpectedMessage = results.FirstOrDefault(r => r.ErrorMessage?.Contains("Either (City + State) or ZipCode must be provided") == true);
        errorWithExpectedMessage.ShouldNotBeNull();
    }

    [Fact]
    public void AddressInput_WithInvalidState_FailsValidation()
    {
        // Arrange
        var address = new AddressInput
        {
            Street = "1600 Amphitheatre Pkwy",
            City = "Mountain View",
            State = "California" // Invalid: must be 2 letters
        };

        // Act
        var context = new ValidationContext(address);
        var results = new List<ValidationResult>();
        bool isValid = Validator.TryValidateObject(address, context, results, validateAllProperties: true);

        // Assert
        isValid.ShouldBeFalse();
    }

    [Fact]
    public void AddressHash_DeterministicForSameInput_ReturnsConsistentHash()
    {
        // Arrange
        var address = new AddressInput
        {
            Street = "1600 Amphitheatre Pkwy",
            City = "Mountain View",
            State = "CA",
            ZipCode = "94043"
        };

        // Act
        string hash1 = address.ComputeHash();
        string hash2 = address.ComputeHash();

        // Assert
        hash1.ShouldBe(hash2);
        hash1.Length.ShouldBe(64); // SHA-256 = 64 hex characters
    }

    [Fact]
    public void AddressHash_CaseInsensitive_SameHashForDifferentCases()
    {
        // Arrange
        var address1 = new AddressInput
        {
            Street = "1600 AMPHITHEATRE PKWY",
            City = "MOUNTAIN VIEW",
            State = "CA",
            ZipCode = "94043"
        };

        var address2 = new AddressInput
        {
            Street = "1600 amphitheatre pkwy",
            City = "mountain view",
            State = "ca",
            ZipCode = "94043"
        };

        // Act
        string hash1 = address1.ComputeHash();
        string hash2 = address2.ComputeHash();

        // Assert
        hash1.ShouldBe(hash2);
    }

    [Fact]
    public void AddressHash_DifferentAddresses_DifferentHash()
    {
        // Arrange
        var address1 = new AddressInput
        {
            Street = "1600 Amphitheatre Pkwy",
            City = "Mountain View",
            State = "CA"
        };

        var address2 = new AddressInput
        {
            Street = "1 Microsoft Way",
            City = "Redmond",
            State = "WA"
        };

        // Act
        string hash1 = address1.ComputeHash();
        string hash2 = address2.ComputeHash();

        // Assert
        hash1.ShouldNotBe(hash2);
    }

    [Fact]
    public void CacheKey_GeneratedCorrectly_FolloresExpectedFormat()
    {
        // Arrange
        var address = new AddressInput
        {
            Street = "1600 Amphitheatre Pkwy",
            City = "Mountain View",
            State = "CA"
        };

        // Act
        string cacheKey = address.GenerateCacheKey();

        // Assert
        cacheKey.ShouldStartWith("addr:v1:");
        cacheKey.Length.ShouldBe("addr:v1:".Length + 64); // Version + hash
    }

    [Fact]
    public void CacheKey_Validation_ValidKeyPasses()
    {
        // Arrange
        var address = new AddressInput
        {
            Street = "1600 Amphitheatre Pkwy",
            City = "Mountain View",
            State = "CA"
        };
        string validKey = address.GenerateCacheKey();

        // Act
        bool isValid = AddressHashExtensions.IsValidCacheKey(validKey);

        // Assert
        isValid.ShouldBeTrue();
    }

    [Theory]
    [InlineData("invalid:key:format")]
    [InlineData("addr:v2:abc")] // Wrong length hash
    [InlineData("addr:v1:xyz")] // Invalid hex characters
    [InlineData("")]
    public void CacheKey_Validation_InvalidKeysFail(string invalidKey)
    {
        // Act
        bool isValid = AddressHashExtensions.IsValidCacheKey(invalidKey);

        // Assert
        isValid.ShouldBeFalse();
    }

    [Fact]
    public void CacheKey_Validation_NullKeyFails()
    {
        // Act
        bool isValid = AddressHashExtensions.IsValidCacheKey(null!);

        // Assert
        isValid.ShouldBeFalse();
    }

    [Fact]
    public void CacheKey_HashExtraction_CorrectlyExtractsHash()
    {
        // Arrange
        var address = new AddressInput
        {
            Street = "1600 Amphitheatre Pkwy",
            City = "Mountain View",
            State = "CA"
        };
        string cacheKey = address.GenerateCacheKey();
        string expectedHash = address.ComputeHash();

        // Act
        string? extractedHash = AddressHashExtensions.ExtractHashFromCacheKey(cacheKey);

        // Assert
        extractedHash.ShouldBe(expectedHash);
    }

    [Fact]
    public void ValidationResponse_Complete_ContainsAllData()
    {
        // Arrange
        var input = new AddressInput { Street = "123 Main", City = "City", State = "CA" };
        var validated = new ValidatedAddress { DeliveryLine1 = "123 MAIN ST", CityName = "CITY", StateAbbreviation = "CA" };
        var analysis = new AddressAnalysis { DpvMatchCode = "Y" };
        var geocoding = new GeocodingResult { Latitude = 37.42, Longitude = -122.084, Precision = "Zip9" };
        var metadata = new ValidationMetadata
        {
            ProviderName = "Smarty",
            ValidatedAt = DateTimeOffset.UtcNow,
            CacheSource = "PROVIDER",
            ApiVersion = "1.0",
            RequestDurationMs = 150
        };

        // Act
        var response = new ValidationResponse
        {
            InputAddress = input,
            ValidatedAddress = validated,
            Analysis = analysis,
            Geocoding = geocoding,
            Metadata = metadata,
            Status = "validated"
        };

        // Assert
        response.InputAddress.ShouldNotBeNull();
        response.ValidatedAddress.ShouldNotBeNull();
        response.Analysis.ShouldNotBeNull();
        response.Geocoding.ShouldNotBeNull();
        response.Metadata.ShouldNotBeNull();
        response.Status.ShouldBe("validated");
        response.Metadata.ProviderName.ShouldBe("Smarty");
    }
}
