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
        Assert.True(isValid);
        Assert.Empty(results);
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
        Assert.True(isValid);
        Assert.Empty(results);
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
        Assert.True(isValid);
        Assert.Empty(results);
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
        Assert.False(isValid);
        Assert.NotEmpty(results);
        // Check that error message contains the expected content (ignore period differences)
        Assert.True(results.Any(r => r.ErrorMessage?.Contains("Either (City + State) or ZipCode must be provided") == true));
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
        Assert.False(isValid);
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
        Assert.Equal(hash1, hash2);
        Assert.Equal(64, hash1.Length); // SHA-256 = 64 hex characters
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
        Assert.Equal(hash1, hash2);
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
        Assert.NotEqual(hash1, hash2);
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
        Assert.StartsWith("addr:v1:", cacheKey);
        Assert.Equal("addr:v1:".Length + 64, cacheKey.Length); // Version + hash
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
        Assert.True(isValid);
    }

    [Theory]
    [InlineData("invalid:key:format")]
    [InlineData("addr:v2:abc")] // Wrong length hash
    [InlineData("addr:v1:xyz")] // Invalid hex characters
    [InlineData(null)]
    [InlineData("")]
    public void CacheKey_Validation_InvalidKeysFail(string invalidKey)
    {
        // Act
        bool isValid = AddressHashExtensions.IsValidCacheKey(invalidKey);

        // Assert
        Assert.False(isValid);
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
        string extractedHash = AddressHashExtensions.ExtractHashFromCacheKey(cacheKey);

        // Assert
        Assert.Equal(expectedHash, extractedHash);
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
        Assert.NotNull(response.InputAddress);
        Assert.NotNull(response.ValidatedAddress);
        Assert.NotNull(response.Analysis);
        Assert.NotNull(response.Geocoding);
        Assert.NotNull(response.Metadata);
        Assert.Equal("validated", response.Status);
        Assert.Equal("Smarty", response.Metadata.ProviderName);
    }
}
