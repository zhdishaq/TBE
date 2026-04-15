using TBE.FlightConnectorService.Application.Amadeus;
using TBE.FlightConnectorService.Application.Amadeus.Models;
using FluentAssertions;
using Xunit;

namespace TBE.Tests.Unit.FlightConnectorService;

[Trait("Category", "Unit")]
public class AmadeusProviderTests
{
    [Fact(DisplayName = "INV02: YQ and YR codes map to Surcharges, all others to Taxes")]
    public void MapOffer_SeparatesYqYrSurchargesFromGovernmentTaxes()
    {
        var raw = new AmadeusFlightOffer
        {
            Id = "offer-1", Source = "GDS",
            LastTicketingDate = DateOnly.FromDateTime(DateTime.UtcNow.AddDays(1)).ToString("yyyy-MM-dd"),
            Price = new AmadeusPrice
            {
                Currency = "GBP", Base = "420.00", GrandTotal = "542.30",
                Taxes =
                [
                    new AmadeusTax { Code = "YQ", Amount = "77.30" },
                    new AmadeusTax { Code = "GB", Amount = "45.00" },
                ]
            },
            Itineraries = [new AmadeusItinerary { Segments = [new AmadeusSegment {
                Departure = new AmadeusPoint { IataCode = "LHR", At = DateTimeOffset.UtcNow },
                Arrival   = new AmadeusPoint { IataCode = "BKK", At = DateTimeOffset.UtcNow.AddHours(11) },
                CarrierCode = "BA", Number = "9", Duration = "PT11H0M"
            }]}],
            TravelerPricings = []
        };

        var offer = AmadeusFlightProvider.MapOffer(raw);

        offer.Source.Should().Be("amadeus");
        offer.Price.Surcharges.Should().HaveCount(1);
        offer.Price.Surcharges[0].Code.Should().Be("YQ");
        offer.Price.Surcharges[0].Amount.Should().Be(77.30m);
        offer.Price.Taxes.Should().HaveCount(1);
        offer.Price.Taxes[0].Code.Should().Be("GB");
        offer.Price.Taxes[0].Amount.Should().Be(45.00m);
    }

    [Fact(DisplayName = "INV02: GrandTotal is Base + Surcharges + Taxes")]
    public void MapOffer_GrandTotalIsCorrect()
    {
        var raw = new AmadeusFlightOffer
        {
            Id = "offer-2", Source = "GDS", LastTicketingDate = null,
            Price = new AmadeusPrice
            {
                Currency = "GBP", Base = "420.00", GrandTotal = "542.30",
                Taxes = [
                    new AmadeusTax { Code = "YQ", Amount = "77.30" },
                    new AmadeusTax { Code = "GB", Amount = "45.00" },
                ]
            },
            Itineraries = [], TravelerPricings = []
        };

        var offer = AmadeusFlightProvider.MapOffer(raw);
        offer.Price.GrandTotal.Should().Be(420m + 77.30m + 45.00m);
    }

    [Fact(DisplayName = "INV02_Auth: AmadeusAuthHandler extends DelegatingHandler")]
    public void AmadeusAuthHandler_IsDelegatingHandler()
    {
        typeof(AmadeusAuthHandler).BaseType.Should().Be(typeof(System.Net.Http.DelegatingHandler));
    }
}
