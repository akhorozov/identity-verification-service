namespace AddressValidation.Api.Infrastructure.Providers.Smarty;

using AddressValidation.Api.Domain;

/// <summary>
/// Maps a <see cref="SmartyCandidate"/> returned by the Smarty US Street API
/// to the domain <see cref="ValidationResponse"/>.
/// </summary>
public static class SmartyResponseMapper
{
    private const string ProviderName = "Smarty";

    /// <summary>
    /// Converts the first (best) Smarty candidate to a <see cref="ValidationResponse"/>.
    /// </summary>
    /// <param name="candidate">The Smarty candidate to map.</param>
    /// <param name="input">The original address input from the client.</param>
    /// <param name="cacheSource">The cache source identifier (e.g. "PROVIDER", "L1", "L2").</param>
    /// <returns>A fully populated <see cref="ValidationResponse"/>.</returns>
    public static ValidationResponse MapToResponse(
        SmartyCandidate candidate,
        AddressInput input,
        string cacheSource = "PROVIDER",
        string apiVersion = "1.0",
        string? correlationId = null)
    {
        ArgumentNullException.ThrowIfNull(candidate);
        ArgumentNullException.ThrowIfNull(input);

        var analysis = candidate.Analysis;
        var status = ResolveStatus(analysis?.DpvMatchCode);

        return new ValidationResponse
        {
            InputAddress = input,
            Status = status,
            ValidatedAddress = MapValidatedAddress(candidate),
            Analysis = MapAnalysis(analysis),
            Geocoding = MapGeocoding(candidate.Metadata),
            Metadata = new ValidationMetadata
            {
                ProviderName = ProviderName,
                ValidatedAt = DateTimeOffset.UtcNow,
                CacheSource = cacheSource,
                ApiVersion = apiVersion,
                CorrelationId = correlationId
            }
        };
    }

    private static string ResolveStatus(string? dpvMatchCode) => dpvMatchCode switch
    {
        "Y" or "S" or "D" => "validated",
        "N" => "invalid",
        _ => "undeliverable"
    };

    private static ValidatedAddress MapValidatedAddress(SmartyCandidate candidate)
    {
        var c = candidate.Components;
        return new ValidatedAddress
        {
            DeliveryLine1 = candidate.DeliveryLine1,
            LastLine = candidate.LastLine,
            PrimaryNumber = c?.PrimaryNumber,
            StreetName = c?.StreetName,
            StreetSuffix = c?.StreetSuffix,
            SecondaryDesignator = c?.SecondaryDesignator,
            SecondaryNumber = c?.SecondaryNumber,
            CityName = c?.CityName,
            StateAbbreviation = c?.StateAbbreviation,
            ZipCode = c?.Zipcode,
            Plus4Code = c?.Plus4Code,
            DeliveryPoint = c?.DeliveryPoint,
            DeliveryPointCheckDigit = c?.DeliveryPointCheckDigit
        };
    }

    private static AddressAnalysis? MapAnalysis(SmartyAnalysis? a)
    {
        if (a is null) return null;

        return new AddressAnalysis
        {
            DpvMatchCode = a.DpvMatchCode,
            DpvFootnotes = a.DpvFootnotes,
            DpvCmra = a.DpvCmra,
            DpvVacant = a.DpvVacant,
            Active = a.Active,
            Footnotes = a.Footnotes,
            LacsLinkCode = a.LacsLinkCode,
            LacsLinkIndicator = a.LacsLinkIndicator,
            SuiteLinkMatch = a.SuiteLinkMatch
        };
    }

    private static GeocodingResult? MapGeocoding(SmartyMetadata? m)
    {
        if (m is null) return null;

        return new GeocodingResult
        {
            Latitude = m.Latitude,
            Longitude = m.Longitude,
            Precision = m.Precision,
            CoordinateLicense = m.CoordinateLicense
        };
    }
}
