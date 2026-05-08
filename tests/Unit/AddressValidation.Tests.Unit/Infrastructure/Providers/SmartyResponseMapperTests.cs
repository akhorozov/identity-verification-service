using AddressValidation.Api.Domain;
using AddressValidation.Api.Infrastructure.Providers.Smarty;

namespace AddressValidation.Tests.Unit.Infrastructure.Providers;

/// <summary>
/// Unit tests for <see cref="SmartyResponseMapper"/>.
/// </summary>
public class SmartyResponseMapperTests
{
    private static AddressInput MakeInput() => new()
    {
        Street = "1600 Amphitheatre Pkwy",
        City = "Mountain View",
        State = "CA",
        ZipCode = "94043"
    };

    private static SmartyCandidate MakeCandidate(string dpvMatchCode = "Y") => new()
    {
        InputIndex = 0,
        CandidateIndex = 0,
        DeliveryLine1 = "1600 AMPHITHEATRE PKWY",
        LastLine = "MOUNTAIN VIEW CA 94043-1351",
        Components = new SmartyComponents
        {
            PrimaryNumber = "1600",
            StreetName = "AMPHITHEATRE",
            StreetSuffix = "PKWY",
            CityName = "MOUNTAIN VIEW",
            StateAbbreviation = "CA",
            Zipcode = "94043",
            Plus4Code = "1351",
            DeliveryPoint = "00",
            DeliveryPointCheckDigit = "5"
        },
        Metadata = new SmartyMetadata
        {
            Latitude = 37.422,
            Longitude = -122.084,
            Precision = "Zip9",
            CoordinateLicense = 1
        },
        Analysis = new SmartyAnalysis
        {
            DpvMatchCode = dpvMatchCode,
            DpvFootnotes = "AABB",
            DpvCmra = "N",
            DpvVacant = "N",
            Active = "Y"
        }
    };

    [Fact]
    public void MapToResponse_WhenCandidateIsNull_ThrowsArgumentNullException()
    {
        var input = MakeInput();
        Assert.Throws<ArgumentNullException>(() =>
            SmartyResponseMapper.MapToResponse(null!, input));
    }

    [Fact]
    public void MapToResponse_WhenInputIsNull_ThrowsArgumentNullException()
    {
        var candidate = MakeCandidate();
        Assert.Throws<ArgumentNullException>(() =>
            SmartyResponseMapper.MapToResponse(candidate, null!));
    }

    [Fact]
    public void MapToResponse_MapsDeliveryLines()
    {
        var candidate = MakeCandidate();
        var result = SmartyResponseMapper.MapToResponse(candidate, MakeInput());

        result.ValidatedAddress!.DeliveryLine1.ShouldBe("1600 AMPHITHEATRE PKWY");
        result.ValidatedAddress.LastLine.ShouldBe("MOUNTAIN VIEW CA 94043-1351");
    }

    [Fact]
    public void MapToResponse_MapsComponents()
    {
        var candidate = MakeCandidate();
        var result = SmartyResponseMapper.MapToResponse(candidate, MakeInput());

        var addr = result.ValidatedAddress!;
        addr.PrimaryNumber.ShouldBe("1600");
        addr.StreetName.ShouldBe("AMPHITHEATRE");
        addr.StreetSuffix.ShouldBe("PKWY");
        addr.CityName.ShouldBe("MOUNTAIN VIEW");
        addr.StateAbbreviation.ShouldBe("CA");
        addr.ZipCode.ShouldBe("94043");
        addr.Plus4Code.ShouldBe("1351");
        addr.DeliveryPoint.ShouldBe("00");
        addr.DeliveryPointCheckDigit.ShouldBe("5");
    }

    [Fact]
    public void MapToResponse_MapsGeocoding()
    {
        var candidate = MakeCandidate();
        var result = SmartyResponseMapper.MapToResponse(candidate, MakeInput());

        result.Geocoding!.Latitude.ShouldBe(37.422);
        result.Geocoding.Longitude.ShouldBe(-122.084);
        result.Geocoding.Precision.ShouldBe("Zip9");
        result.Geocoding.CoordinateLicense.ShouldBe(1);
    }

    [Fact]
    public void MapToResponse_MapsAnalysis()
    {
        var candidate = MakeCandidate("Y");
        var result = SmartyResponseMapper.MapToResponse(candidate, MakeInput());

        result.Analysis!.DpvMatchCode.ShouldBe("Y");
        result.Analysis.DpvFootnotes.ShouldBe("AABB");
        result.Analysis.DpvCmra.ShouldBe("N");
        result.Analysis.DpvVacant.ShouldBe("N");
        result.Analysis.Active.ShouldBe("Y");
    }

    [Fact]
    public void MapToResponse_SetsMetadata()
    {
        var candidate = MakeCandidate();
        var result = SmartyResponseMapper.MapToResponse(candidate, MakeInput(), cacheSource: "PROVIDER", apiVersion: "1.0");

        result.Metadata.ProviderName.ShouldBe("Smarty");
        result.Metadata.CacheSource.ShouldBe("PROVIDER");
        result.Metadata.ApiVersion.ShouldBe("1.0");
        result.Metadata.ValidatedAt.ShouldBeLessThanOrEqualTo(DateTimeOffset.UtcNow);
    }

    [Theory]
    [InlineData("Y", "validated")]
    [InlineData("S", "validated")]
    [InlineData("D", "validated")]
    [InlineData("N", "invalid")]
    [InlineData("", "undeliverable")]
    [InlineData(null, "undeliverable")]
    public void MapToResponse_ResolvesStatus_FromDpvMatchCode(string? dpvMatchCode, string expectedStatus)
    {
        var candidate = new SmartyCandidate
        {
            DeliveryLine1 = "1600 AMPHITHEATRE PKWY",
            Analysis = new SmartyAnalysis { DpvMatchCode = dpvMatchCode }
        };

        var result = SmartyResponseMapper.MapToResponse(candidate, MakeInput());

        result.Status.ShouldBe(expectedStatus);
    }

    [Fact]
    public void MapToResponse_WhenNoMetadata_GeocodingIsNull()
    {
        var candidate = new SmartyCandidate
        {
            DeliveryLine1 = "123 Main St",
            Analysis = new SmartyAnalysis { DpvMatchCode = "Y" }
        };

        var result = SmartyResponseMapper.MapToResponse(candidate, MakeInput());

        result.Geocoding.ShouldBeNull();
    }

    [Fact]
    public void MapToResponse_WhenNoAnalysis_AnalysisIsNull()
    {
        var candidate = new SmartyCandidate
        {
            DeliveryLine1 = "123 Main St"
        };

        var result = SmartyResponseMapper.MapToResponse(candidate, MakeInput());

        result.Analysis.ShouldBeNull();
    }

    [Fact]
    public void MapToResponse_PreservesInputAddress()
    {
        var input = MakeInput();
        var candidate = MakeCandidate();

        var result = SmartyResponseMapper.MapToResponse(candidate, input);

        result.InputAddress.ShouldBeSameAs(input);
    }
}
