using TBE.HotelConnectorService.Application.Hotelbeds;
using TBE.HotelConnectorService.Application.Hotelbeds.Models;
using FluentAssertions;
using Xunit;

namespace TBE.Tests.Unit.HotelConnectorService;

[Trait("Category", "Unit")]
public class HotelbedsProviderTests
{
    [Fact(DisplayName = "INV04: MapOffer produces UnifiedHotelOffer with all required fields")]
    public void MapOffer_PopulatesRequiredFields()
    {
        var hotel = new HotelbedsHotel { Code = 123, Name = "Grand Hotel" };
        var room  = new HotelbedsRoom  { Code = "DBL", Name = "Double Room" };
        var rate  = new HotelbedsRate
        {
            RateKey = "rate-key-abc", Net = "150.00", Currency = "GBP",
            RateType = "BOOKABLE",
            CancellationPolicies = [new HotelbedsCancellationPolicy { Amount = "150.00", From = "2024-12-01" }]
        };

        var offer = HotelbedsProvider.MapOffer(hotel, room, rate);

        offer.Source.Should().Be("hotelbeds");
        offer.SourceRef.Should().Be("rate-key-abc");
        offer.PropertyName.Should().Be("Grand Hotel");
        offer.RoomType.Should().Be("Double Room");
        offer.CancellationPolicy.Should().NotBeNullOrEmpty();
        offer.Price.Currency.Should().Be("GBP");
        offer.Price.Base.Should().Be(150.00m);
    }

    [Fact(DisplayName = "INV04: Non-refundable rate maps cancellation policy correctly")]
    public void MapOffer_NonRefundable_CancellationPolicyIsNonRefundable()
    {
        var hotel = new HotelbedsHotel { Code = 1, Name = "Hotel" };
        var room  = new HotelbedsRoom  { Code = "SGL", Name = "Single Room" };
        var rate  = new HotelbedsRate
        {
            RateKey = "nr-key", Net = "100.00", Currency = "GBP",
            RateType = "BOOKABLE", CancellationPolicies = []
        };

        var offer = HotelbedsProvider.MapOffer(hotel, room, rate);
        offer.CancellationPolicy.Should().Be("Non-refundable");
    }
}
