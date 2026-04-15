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
            passengers:        request.Passengers,
            cancellationToken: ct);

        return raw.Data.Select(MapOffer).ToList();
    }

    private static decimal SafeParseDecimal(string? s)
    {
        if (decimal.TryParse(s, System.Globalization.NumberStyles.Any,
                System.Globalization.CultureInfo.InvariantCulture, out var value))
            return value;
        return 0m;
    }

    public static UnifiedCarOffer MapOffer(AmadeusTransferOffer o)
    {
        var baseAmount = SafeParseDecimal(o.Quotation.Base.MonetaryAmount);

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
                    .Select(t => new PriceComponent("TAX", SafeParseDecimal(t.MonetaryAmount)))
                    .ToList(),
            },
        };
    }
}
