using TBE.HotelConnectorService.Application.Car;
using TBE.HotelConnectorService.Application.Car.Models;
using FluentAssertions;
using Xunit;

namespace TBE.Tests.Unit.HotelConnectorService;

[Trait("Category", "Unit")]
public class CarProviderTests
{
    [Fact(DisplayName = "INV05: AmadeusCarProvider implements ICarAvailabilityProvider")]
    public void CarProvider_ImplementsICarAvailabilityProvider()
    {
        typeof(AmadeusCarProvider).GetInterface("ICarAvailabilityProvider").Should().NotBeNull();
    }

    [Fact(DisplayName = "INV05: MapOffer sets Source='amadeus-transfers'")]
    public void MapOffer_SetsSourceToAmadeusTransfers()
    {
        var raw = new AmadeusTransferOffer
        {
            Id = "transfer-1", TransferType = "PRIVATE",
            Vehicle = new AmadeusTransferVehicle { Code = "CAT_STANDARD", Description = "Economy Car", Seats = [] },
            ServiceProvider = new AmadeusServiceProvider { Code = "SP1", Name = "Budget Car Hire" },
            Quotation = new AmadeusTransferQuotation
            {
                CurrencyCode = "GBP",
                Base = new AmadeusTransferAmount { MonetaryAmount = "45.00" },
                TotalAmount = new AmadeusTransferAmount { MonetaryAmount = "50.00" },
                Taxes = [new AmadeusTransferTax { MonetaryAmount = "5.00" }]
            }
        };

        var offer = AmadeusCarProvider.MapOffer(raw);

        offer.Source.Should().Be("amadeus-transfers");
        offer.SourceRef.Should().Be("transfer-1");
        offer.VehicleCategory.Should().Be("CAT_STANDARD");
        offer.VehicleDescription.Should().Be("Economy Car");
        offer.SupplierName.Should().Be("Budget Car Hire");
        offer.Price.Base.Should().Be(45.00m);
        offer.Price.Currency.Should().Be("GBP");
    }

    [Fact(DisplayName = "INV05: MapOffer GrandTotal includes base + taxes")]
    public void MapOffer_GrandTotalIncludesTaxes()
    {
        var raw = new AmadeusTransferOffer
        {
            Id = "t2", TransferType = "PRIVATE",
            Vehicle = new AmadeusTransferVehicle { Code = "CAT_LUX", Description = "Luxury", Seats = [] },
            ServiceProvider = new AmadeusServiceProvider { Code = "SP2", Name = "Hertz" },
            Quotation = new AmadeusTransferQuotation
            {
                CurrencyCode = "GBP",
                Base = new AmadeusTransferAmount { MonetaryAmount = "100.00" },
                TotalAmount = new AmadeusTransferAmount { MonetaryAmount = "120.00" },
                Taxes = [
                    new AmadeusTransferTax { MonetaryAmount = "10.00" },
                    new AmadeusTransferTax { MonetaryAmount = "10.00" },
                ]
            }
        };

        var offer = AmadeusCarProvider.MapOffer(raw);
        offer.Price.GrandTotal.Should().Be(120.00m);
    }
}
