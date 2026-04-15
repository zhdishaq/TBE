using TBE.FlightConnectorService.Application.Sabre;
using TBE.FlightConnectorService.Application.Sabre.Models;
using FluentAssertions;
using Xunit;

namespace TBE.Tests.Unit.FlightConnectorService;

[Trait("Category", "Unit")]
public class SabreProviderTests
{
    [Fact(DisplayName = "INV03: SabreFlightProvider implements IFlightAvailabilityProvider")]
    public void SabreProvider_NameIsSabre()
    {
        typeof(SabreFlightProvider).GetInterface("IFlightAvailabilityProvider").Should().NotBeNull();
    }

    [Fact(DisplayName = "INV03: MapItinerary sets Source='sabre'")]
    public void MapItinerary_SetsSourceToSabre()
    {
        var it = new SabreItinerary { Id = 42, PricingInformation = [], Legs = [] };
        var offer = SabreFlightProvider.MapItinerary(it, [], [], []);
        offer.Source.Should().Be("sabre");
        offer.SourceRef.Should().StartWith("sabre-");
    }

    [Fact(DisplayName = "INV03: MapItinerary separates YQ/YR from government taxes")]
    public void MapItinerary_SeparatesYqYrSurcharges()
    {
        var taxMap = new Dictionary<int, SabreTaxDesc>
        {
            [1] = new SabreTaxDesc { Id = 1, Code = "YQ", Amount = 50m },
            [2] = new SabreTaxDesc { Id = 2, Code = "GB", Amount = 20m },
        };
        var it = new SabreItinerary
        {
            Id = 1,
            Legs = [],
            PricingInformation = [new SabrePricingInfo
            {
                Fare = new SabreFare
                {
                    TotalFare = new SabreMonetary { Amount = 270m, Currency = "GBP" },
                    PassengerInfoList = []
                },
                Taxes = [new SabreTaxRef { Ref = 1 }, new SabreTaxRef { Ref = 2 }]
            }]
        };
        var offer = SabreFlightProvider.MapItinerary(it, [], taxMap, []);
        offer.Price.Surcharges.Should().ContainSingle(s => s.Code == "YQ" && s.Amount == 50m);
        offer.Price.Taxes.Should().ContainSingle(t => t.Code == "GB" && t.Amount == 20m);
        offer.Price.Base.Should().Be(200m); // 270 - 50 - 20
    }

    [Fact(DisplayName = "INV02_Auth: SabreAuthHandler extends DelegatingHandler")]
    public void SabreAuthHandler_IsDelegatingHandler()
    {
        typeof(SabreAuthHandler).BaseType.Should().Be(typeof(System.Net.Http.DelegatingHandler));
    }
}
