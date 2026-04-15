using TBE.Contracts.Inventory;
using TBE.Contracts.Inventory.Models;
using TBE.HotelConnectorService.Application.Hotelbeds.Models;

namespace TBE.HotelConnectorService.Application.Hotelbeds;

public sealed class HotelbedsProvider(IHotelbedsApi api) : IHotelAvailabilityProvider
{
    public string Name => "hotelbeds";

    public async Task<IReadOnlyList<UnifiedHotelOffer>> SearchAsync(
        HotelSearchRequest request, CancellationToken ct = default)
    {
        var body = new HotelbedsAvailabilityRequest
        {
            Stay = new HotelbedsStay
            {
                CheckIn  = request.CheckIn.ToString("yyyy-MM-dd"),
                CheckOut = request.CheckOut.ToString("yyyy-MM-dd"),
            },
            Destination = new HotelbedsDestination { Code = request.DestinationCode },
            Occupancies = request.Rooms.Select(r => new HotelbedsOccupancy
            {
                Rooms    = 1,
                Adults   = r.Adults,
                Children = r.Children,
            }).ToList(),
        };

        var raw = await api.SearchAvailabilityAsync(body, ct);
        return raw.Hotels?.Hotels
            .SelectMany(h => h.Rooms.SelectMany(r => r.Rates.Select(rate => MapOffer(h, r, rate))))
            .ToList() ?? [];
    }

    public static UnifiedHotelOffer MapOffer(
        HotelbedsHotel hotel, HotelbedsRoom room, HotelbedsRate rate)
    {
        // Build cancellation policy description from policies list
        var cancellationDesc = rate.CancellationPolicies.Count == 0
            ? "Non-refundable"
            : string.Join("; ", rate.CancellationPolicies.Select(
                p => $"Cancel by {p.From}: fee {p.Amount} {rate.Currency}"));

        return new UnifiedHotelOffer
        {
            Source             = "hotelbeds",
            SourceRef          = rate.RateKey,
            ExpiresAt          = DateTimeOffset.UtcNow.AddMinutes(30),
            HotelCode          = hotel.Code.ToString(),
            PropertyName       = hotel.Name,
            RoomType           = room.Name,
            CancellationPolicy = cancellationDesc,
            Price = new PriceBreakdown
            {
                Currency = rate.Currency,
                Base     = decimal.Parse(rate.Net),
                // Hotelbeds net rate is all-in; no separate surcharges/taxes in availability response
                Surcharges = [],
                Taxes      = [],
            },
        };
    }
}
