namespace AddressValidation.Api.Domain;

/// <summary>
/// Represents geocoding information for a validated address.
/// Contains latitude, longitude, and precision metadata from Smarty's geocoding enrichment.
/// </summary>
public class GeocodingResult
{
    /// <summary>
    /// Latitude coordinate in decimal degrees (WGS84).
    /// Range: -90.0 to 90.0
    /// Null if geocoding was not available or failed.
    /// Example: 37.42199999 (Google's Mountain View HQ)
    /// </summary>
    public double? Latitude { get; set; }

    /// <summary>
    /// Longitude coordinate in decimal degrees (WGS84).
    /// Range: -180.0 to 180.0
    /// Null if geocoding was not available or failed.
    /// Example: -122.08400000 (Google's Mountain View HQ)
    /// </summary>
    public double? Longitude { get; set; }

    /// <summary>
    /// Precision level of the geocoding coordinates.
    /// Indicates the granularity at which the address was geocoded.
    /// 
    /// Values:
    /// - "Zip9" = Precise to ZIP+4 (most accurate)
    /// - "Zip5" = Precise to ZIP code
    /// - "City" = Precise to city center
    /// - "State" = Precise to state center
    /// - "Rooftop" = Rooftop-level precision (highest accuracy)
    /// - "RooftopGeo Code" = Rooftop accuracy via external geocoding
    /// </summary>
    public string? Precision { get; set; }

    /// <summary>
    /// Coordinate license code indicating data source/terms.
    /// Indicates which Smarty license covers the geocoding data.
    /// Example: 1 (for US Rooftop Geocoding license)
    /// </summary>
    public int? CoordinateLicense { get; set; }
}
