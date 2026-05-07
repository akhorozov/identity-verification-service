namespace AddressValidation.Api.Domain;

using System.ComponentModel.DataAnnotations;

/// <summary>
/// Represents the raw address input from a client request.
/// Either (City + State) OR ZipCode must be provided along with Street.
/// </summary>
public class AddressInput : IValidatableObject
{
    /// <summary>
    /// Primary delivery address line (street address and house/building number).
    /// Required. Example: "1600 Amphitheatre Pkwy"
    /// </summary>
    [Required(ErrorMessage = "Street address is required.")]
    [StringLength(100, MinimumLength = 1, ErrorMessage = "Street must be between 1 and 100 characters.")]
    public required string Street { get; set; }

    /// <summary>
    /// Secondary address line (apartment, suite, unit number, etc.).
    /// Optional. Example: "Suite 100"
    /// </summary>
    [StringLength(100, ErrorMessage = "Street2 must not exceed 100 characters.")]
    public string? Street2 { get; set; }

    /// <summary>
    /// City name.
    /// Either this + State OR ZipCode must be provided.
    /// Example: "Mountain View"
    /// </summary>
    [StringLength(50, ErrorMessage = "City must not exceed 50 characters.")]
    public string? City { get; set; }

    /// <summary>
    /// Two-letter US state abbreviation.
    /// Either City + this OR ZipCode must be provided.
    /// Example: "CA", "NY", "TX"
    /// </summary>
    [StringLength(2, MinimumLength = 2, ErrorMessage = "State must be exactly 2 characters.")]
    [RegularExpression(@"^[A-Z]{2}$", ErrorMessage = "State must be a valid 2-letter US state abbreviation.")]
    public string? State { get; set; }

    /// <summary>
    /// Five or nine-digit US ZIP code.
    /// Either this OR (City + State) must be provided.
    /// Formats: "12345" or "12345-6789"
    /// </summary>
    [StringLength(10, ErrorMessage = "ZipCode must not exceed 10 characters.")]
    [RegularExpression(@"^\d{5}(-\d{4})?$", ErrorMessage = "ZipCode must be in format 12345 or 12345-6789.")]
    public string? ZipCode { get; set; }

    /// <summary>
    /// Optional recipient name or business name.
    /// Used for context but not required for validation.
    /// Example: "John Doe" or "Google Inc."
    /// </summary>
    [StringLength(100, ErrorMessage = "Addressee must not exceed 100 characters.")]
    public string? Addressee { get; set; }

    /// <summary>
    /// Validates that either (City + State) OR ZipCode is provided.
    /// </summary>
    public IEnumerable<ValidationResult> Validate(ValidationContext context)
    {
        // Check if either (City + State) or ZipCode is provided
        bool hasCityAndState = !string.IsNullOrWhiteSpace(City) && !string.IsNullOrWhiteSpace(State);
        bool hasZipCode = !string.IsNullOrWhiteSpace(ZipCode);

        if (!hasCityAndState && !hasZipCode)
        {
            yield return new ValidationResult(
                "Either (City + State) or ZipCode must be provided.",
                new[] { nameof(City), nameof(State), nameof(ZipCode) });
        }
    }
}
