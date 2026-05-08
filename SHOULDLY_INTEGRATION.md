# Shouldly Integration Guide

## Overview

Shouldly has been integrated into the test projects for more readable and maintainable test assertions.

## Installation

- **Version**: 4.1.0
- **Package**: Added to `Directory.Packages.props` (Central Package Management)
- **Projects**: 
  - `AddressValidation.Tests.Unit`
  - `AddressValidation.Tests.Integration`

## Configuration

### Global Using Statement

Both test projects have a global `using Shouldly;` statement in their `.csproj` files:

```xml
<ItemGroup>
  <Using Include="Xunit" />
  <Using Include="Shouldly" />
</ItemGroup>
```

This makes Shouldly methods available without explicit imports.

## Syntax Comparison

### Before (xUnit Assert)
```csharp
Assert.True(isValid);
Assert.Empty(results);
Assert.Equal(hash1, hash2);
Assert.StartsWith("addr:v1:", cacheKey);
Assert.NotNull(response);
```

### After (Shouldly)
```csharp
isValid.ShouldBeTrue();
results.ShouldBeEmpty();
hash1.ShouldBe(hash2);
cacheKey.ShouldStartWith("addr:v1:");
response.ShouldNotBeNull();
```

## Key Benefits

1. **Readability**: Assertions read naturally left-to-right, like English
2. **Better Error Messages**: Shouldly provides more descriptive failure messages
3. **IntelliSense**: IDE autocomplete guides you to available assertions
4. **Fluent API**: Chain assertions intuitively

## Common Shouldly Assertions

| Assertion | Usage |
|-----------|-------|
| `.ShouldBe()` | Assert equality |
| `.ShouldNotBe()` | Assert inequality |
| `.ShouldBeTrue()` | Assert boolean is true |
| `.ShouldBeFalse()` | Assert boolean is false |
| `.ShouldBeNull()` | Assert null |
| `.ShouldNotBeNull()` | Assert not null |
| `.ShouldBeEmpty()` | Assert collection/string is empty |
| `.ShouldNotBeEmpty()` | Assert collection/string is not empty |
| `.ShouldStartWith()` | Assert string starts with value |
| `.ShouldContain()` | Assert string/collection contains value |
| `.ShouldThrow()` | Assert exception is thrown |

## Examples from Project

### Domain Model Tests
```csharp
[Fact]
public void AddressHash_DeterministicForSameInput_ReturnsConsistentHash()
{
    // Arrange
    var address = new AddressInput { /* ... */ };

    // Act
    string hash1 = address.ComputeHash();
    string hash2 = address.ComputeHash();

    // Assert
    hash1.ShouldBe(hash2);
    hash1.Length.ShouldBe(64); // SHA-256 = 64 hex characters
}
```

### Validation Tests
```csharp
[Fact]
public void CacheKey_Validation_ValidKeyPasses()
{
    // Arrange
    string validKey = address.GenerateCacheKey();

    // Act
    bool isValid = AddressHashExtensions.IsValidCacheKey(validKey);

    // Assert
    isValid.ShouldBeTrue();
}
```

## Build Configuration

All projects are configured with `TreatWarningsAsErrors=true` in `Directory.Build.props`, ensuring code quality standards are maintained.

## Next Steps

When writing new tests:
1. Use Shouldly assertions instead of xUnit Assert methods
2. Leverage the fluent API for clear, expressive test code
3. Take advantage of better error messages for debugging failures

For more information, visit: [Shouldly Documentation](https://docs.shouldly.io/)
