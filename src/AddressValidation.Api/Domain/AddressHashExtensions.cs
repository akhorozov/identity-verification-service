namespace AddressValidation.Api.Domain;

using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

/// <summary>
/// Extension methods for address hashing and cache key generation.
/// Provides deterministic SHA-256 hashing for creating cache keys.
/// </summary>
public static class AddressHashExtensions
{
    /// <summary>
    /// API version for cache keys. Increment when address model structure changes.
    /// </summary>
    private const int CacheKeyVersion = 1;

    /// <summary>
    /// Computes a deterministic SHA-256 hash of an address for cache key generation.
    /// The hash is computed from a normalized, sorted JSON representation of the address.
    /// This ensures the same input always produces the same hash (deterministic).
    /// </summary>
    /// <param name="address">The address to hash.</param>
    /// <returns>A hexadecimal SHA-256 hash string (64 characters).</returns>
    /// <remarks>
    /// Normalization includes:
    /// - Whitespace trimming
    /// - Uppercase conversion
    /// - Null/empty field exclusion
    /// - Consistent field ordering
    /// This ensures that logically equivalent addresses produce the same hash.
    /// </remarks>
    public static string ComputeHash(this AddressInput address)
    {
        if (address == null)
            throw new ArgumentNullException(nameof(address));

        // Create a normalized, sorted representation for consistent hashing
        var normalized = new
        {
            street = (address.Street ?? string.Empty).Trim().ToUpperInvariant(),
            street2 = (address.Street2 ?? string.Empty).Trim().ToUpperInvariant(),
            city = (address.City ?? string.Empty).Trim().ToUpperInvariant(),
            state = (address.State ?? string.Empty).Trim().ToUpperInvariant(),
            zipCode = (address.ZipCode ?? string.Empty).Trim().ToUpperInvariant(),
            // Note: Addressee is excluded from hash as it doesn't affect address validation
        };

        // Serialize to JSON with consistent formatting
        string json = JsonSerializer.Serialize(normalized, new JsonSerializerOptions
        {
            WriteIndented = false, // Compact format
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        });

        // Compute SHA-256 hash
        byte[] bytes = Encoding.UTF8.GetBytes(json);
        using (var sha256 = SHA256.Create())
        {
            byte[] hashBytes = sha256.ComputeHash(bytes);
            return Convert.ToHexString(hashBytes).ToLowerInvariant();
        }
    }

    /// <summary>
    /// Generates a cache key for storing/retrieving validated addresses.
    /// Format: "addr:v{version}:{hash}"
    /// Example: "addr:v1:a7b3f4e2c1d9e8f7a6b5c4d3e2f1a0b9c8d7e6f5a4b3c2d1e0f9a8b7c6d5"
    /// </summary>
    /// <param name="address">The address to generate a cache key for.</param>
    /// <returns>A versioned cache key string suitable for Redis/CosmosDB keys.</returns>
    public static string GenerateCacheKey(this AddressInput address)
    {
        if (address == null)
            throw new ArgumentNullException(nameof(address));

        string hash = address.ComputeHash();
        return $"addr:v{CacheKeyVersion}:{hash}";
    }

    /// <summary>
    /// Extracts the hash from a cache key.
    /// Assumes key format: "addr:v{version}:{hash}"
    /// </summary>
    /// <param name="cacheKey">The cache key to parse.</param>
    /// <returns>The extracted hash portion, or null if key format is invalid.</returns>
    public static string? ExtractHashFromCacheKey(string cacheKey)
    {
        if (string.IsNullOrWhiteSpace(cacheKey))
            return null;

        // Format: "addr:v{version}:{hash}"
        var parts = cacheKey.Split(':');
        if (parts.Length != 3)
            return null;

        // Validate format: parts[0] = "addr", parts[1] = "v{version}", parts[2] = hash
        if (parts[0] != "addr" || !parts[1].StartsWith("v"))
            return null;

        return parts[2];
    }

    /// <summary>
    /// Validates that a cache key follows the expected format.
    /// Format: "addr:v{digit}:{64-char-hex-hash}"
    /// </summary>
    /// <param name="cacheKey">The cache key to validate.</param>
    /// <returns>True if the key matches the expected format; otherwise false.</returns>
    public static bool IsValidCacheKey(string cacheKey)
    {
        if (string.IsNullOrWhiteSpace(cacheKey))
            return false;

        const string prefixPattern = "addr:v";
        if (!cacheKey.StartsWith(prefixPattern))
            return false;

        // Extract version and hash
        var parts = cacheKey.Split(':');
        if (parts.Length != 3)
            return false;

        // Validate version is numeric
        if (!int.TryParse(parts[1].Substring(1), out _))
            return false;

        // Validate hash is 64 hex characters (SHA-256)
        string hash = parts[2];
        if (hash.Length != 64)
            return false;

        return hash.All(c => "0123456789abcdef".Contains(c));
    }
}
