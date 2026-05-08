namespace AddressValidation.Api.Infrastructure.Providers.Smarty;

using System.Text.Json.Serialization;

/// <summary>
/// Represents a single address candidate returned by the Smarty US Street Address API.
/// See: https://www.smarty.com/docs/us-street-api#results
/// </summary>
public sealed class SmartyCandidate
{
    [JsonPropertyName("input_index")]
    public int InputIndex { get; init; }

    [JsonPropertyName("candidate_index")]
    public int CandidateIndex { get; init; }

    [JsonPropertyName("delivery_line_1")]
    public string? DeliveryLine1 { get; init; }

    [JsonPropertyName("delivery_line_2")]
    public string? DeliveryLine2 { get; init; }

    [JsonPropertyName("last_line")]
    public string? LastLine { get; init; }

    [JsonPropertyName("delivery_point_barcode")]
    public string? DeliveryPointBarcode { get; init; }

    [JsonPropertyName("components")]
    public SmartyComponents? Components { get; init; }

    [JsonPropertyName("metadata")]
    public SmartyMetadata? Metadata { get; init; }

    [JsonPropertyName("analysis")]
    public SmartyAnalysis? Analysis { get; init; }
}

/// <summary>
/// Parsed address components from Smarty US Street API.
/// </summary>
public sealed class SmartyComponents
{
    [JsonPropertyName("primary_number")]
    public string? PrimaryNumber { get; init; }

    [JsonPropertyName("street_predirection")]
    public string? StreetPredirection { get; init; }

    [JsonPropertyName("street_name")]
    public string? StreetName { get; init; }

    [JsonPropertyName("street_suffix")]
    public string? StreetSuffix { get; init; }

    [JsonPropertyName("street_postdirection")]
    public string? StreetPostdirection { get; init; }

    [JsonPropertyName("secondary_designator")]
    public string? SecondaryDesignator { get; init; }

    [JsonPropertyName("secondary_number")]
    public string? SecondaryNumber { get; init; }

    [JsonPropertyName("city_name")]
    public string? CityName { get; init; }

    [JsonPropertyName("default_city_name")]
    public string? DefaultCityName { get; init; }

    [JsonPropertyName("state_abbreviation")]
    public string? StateAbbreviation { get; init; }

    [JsonPropertyName("zipcode")]
    public string? Zipcode { get; init; }

    [JsonPropertyName("plus4_code")]
    public string? Plus4Code { get; init; }

    [JsonPropertyName("delivery_point")]
    public string? DeliveryPoint { get; init; }

    [JsonPropertyName("delivery_point_check_digit")]
    public string? DeliveryPointCheckDigit { get; init; }
}

/// <summary>
/// Geocoding and USPS metadata from Smarty US Street API.
/// </summary>
public sealed class SmartyMetadata
{
    [JsonPropertyName("record_type")]
    public string? RecordType { get; init; }

    [JsonPropertyName("zip_type")]
    public string? ZipType { get; init; }

    [JsonPropertyName("county_fips")]
    public string? CountyFips { get; init; }

    [JsonPropertyName("county_name")]
    public string? CountyName { get; init; }

    [JsonPropertyName("carrier_route")]
    public string? CarrierRoute { get; init; }

    [JsonPropertyName("congressional_district")]
    public string? CongressionalDistrict { get; init; }

    [JsonPropertyName("latitude")]
    public double? Latitude { get; init; }

    [JsonPropertyName("longitude")]
    public double? Longitude { get; init; }

    [JsonPropertyName("precision")]
    public string? Precision { get; init; }

    [JsonPropertyName("coordinate_license")]
    public int? CoordinateLicense { get; init; }
}

/// <summary>
/// DPV and CASS analysis from Smarty US Street API.
/// </summary>
public sealed class SmartyAnalysis
{
    [JsonPropertyName("dpv_match_code")]
    public string? DpvMatchCode { get; init; }

    [JsonPropertyName("dpv_footnotes")]
    public string? DpvFootnotes { get; init; }

    [JsonPropertyName("dpv_cmra")]
    public string? DpvCmra { get; init; }

    [JsonPropertyName("dpv_vacant")]
    public string? DpvVacant { get; init; }

    [JsonPropertyName("active")]
    public string? Active { get; init; }

    [JsonPropertyName("footnotes")]
    public string? Footnotes { get; init; }

    [JsonPropertyName("lacs_link_code")]
    public string? LacsLinkCode { get; init; }

    [JsonPropertyName("lacs_link_indicator")]
    public string? LacsLinkIndicator { get; init; }

    [JsonPropertyName("suite_link_match")]
    public bool SuiteLinkMatch { get; init; }
}
