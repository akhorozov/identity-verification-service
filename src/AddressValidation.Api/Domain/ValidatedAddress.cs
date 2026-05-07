namespace AddressValidation.Api.Domain;

/// <summary>
/// Represents a USPS-standardized, CASS-certified address component breakdown.
/// These values come from Smarty US Street API Candidate response.
/// All properties can be null for failed validations.
/// </summary>
public class ValidatedAddress
{
    /// <summary>
    /// First line of USPS standardized delivery address.
    /// Combined street number, name, suffix, and secondary unit designator.
    /// Example: "1600 AMPHITHEATRE PKWY"
    /// </summary>
    public string? DeliveryLine1 { get; set; }

    /// <summary>
    /// Last line of USPS standardized address (city, state, ZIP).
    /// Example: "MOUNTAIN VIEW CA 94043-1351"
    /// </summary>
    public string? LastLine { get; set; }

    /// <summary>
    /// Primary street number.
    /// Example: "1600"
    /// </summary>
    public string? PrimaryNumber { get; set; }

    /// <summary>
    /// Street name without number or suffix.
    /// Example: "AMPHITHEATRE"
    /// </summary>
    public string? StreetName { get; set; }

    /// <summary>
    /// Street suffix abbreviation (AVE, ST, RD, PKWY, etc.).
    /// Example: "PKWY"
    /// </summary>
    public string? StreetSuffix { get; set; }

    /// <summary>
    /// Secondary unit designator (APT, STE, UNIT, FLR, etc.).
    /// Example: "APT"
    /// </summary>
    public string? SecondaryDesignator { get; set; }

    /// <summary>
    /// Secondary unit number.
    /// Example: "5B" or "100"
    /// </summary>
    public string? SecondaryNumber { get; set; }

    /// <summary>
    /// City name in USPS standardized uppercase format.
    /// Example: "MOUNTAIN VIEW"
    /// </summary>
    public string? CityName { get; set; }

    /// <summary>
    /// Two-letter state abbreviation in uppercase.
    /// Example: "CA"
    /// </summary>
    public string? StateAbbreviation { get; set; }

    /// <summary>
    /// Five-digit ZIP code.
    /// Example: "94043"
    /// </summary>
    public string? ZipCode { get; set; }

    /// <summary>
    /// Four-digit ZIP+4 extension for precise delivery.
    /// Example: "1351"
    /// </summary>
    public string? Plus4Code { get; set; }

    /// <summary>
    /// USPS delivery point (two digits) indicating specific delivery location.
    /// Example: "00"
    /// </summary>
    public string? DeliveryPoint { get; set; }

    /// <summary>
    /// Check digit for delivery point validation (validates DeliveryPoint + ZipCode).
    /// Example: "5"
    /// </summary>
    public string? DeliveryPointCheckDigit { get; set; }
}
