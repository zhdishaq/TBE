using TBE.Contracts.Inventory;
using TBE.Contracts.Inventory.Models;
using TBE.HotelConnectorService.Application.Car.Models;

namespace TBE.HotelConnectorService.Application.Car;

public sealed class AmadeusCarProvider(IAmadeusTransferApi api) : ICarAvailabilityProvider
{
    public string Name => "amadeus-transfers";

    public async Task<IReadOnlyList<UnifiedCarOffer>> SearchAsync(
        CarSearchRequest request, CancellationToken ct = default)
    {
        var raw = await api.SearchAsync(
            startLocationCode: request.PickupLocationCode,
            endLocationCode:   request.DropoffLocationCode,
            transferType:      "PRIVATE",
            startDateTime:     request.PickupDateTime.ToString("yyyy-MM-ddTHH:mm:ss"),
            passengers:        1,
            cancellationToken: ct);

        return raw.Data.Select(MapOffer).ToList();
    }

    public static UnifiedCarOffer MapOffer(AmadeusTransferOffer o)
    {
        var taxTotal = o.Quotation.Taxes.Sum(t => decimal.Parse(t.MonetaryAmount));
        var baseAmount = decimal.Parse(o.Quotation.Base.MonetaryAmount);

        return new UnifiedCarOffer
        {
            Source             = "amadeus-transfers",
            SourceRef          = o.Id,
            ExpiresAt          = DateTimeOffset.UtcNow.AddMinutes(30),
            VehicleCategory    = o.Vehicle.Code,
            VehicleDescription = o.Vehicle.Description,
            SupplierName       = o.ServiceProvider.Name,
            Price = new PriceBreakdown
            {
                Currency   = o.Quotation.CurrencyCode,
                Base       = baseAmount,
                Surcharges = [],
                Taxes      = o.Quotation.Taxes
                    .Select(t => new PriceComponent("TAX", decimal.Parse(t.MonetaryAmount)))
                    .ToList(),
            },
        };
    }
}
