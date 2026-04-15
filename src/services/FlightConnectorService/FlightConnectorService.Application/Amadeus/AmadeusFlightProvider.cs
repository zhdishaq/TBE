using Microsoft.Extensions.Logging;
using TBE.Contracts.Inventory;
using TBE.Contracts.Inventory.Models;
using TBE.FlightConnectorService.Application.Amadeus.Models;

namespace TBE.FlightConnectorService.Application.Amadeus;

public sealed class AmadeusFlightProvider(IAmadeusFlightApi api, ILogger<AmadeusFlightProvider> logger)
    : IFlightAvailabilityProvider
{
    public string Name => "amadeus";

    public async Task<IReadOnlyList<UnifiedFlightOffer>> SearchAsync(
        FlightSearchRequest request, CancellationToken ct = default)
    {
        var raw = await api.SearchAsync(
            origin:            request.Origin,
            destination:       request.Destination,
            departureDate:     request.DepartureDate.ToString("yyyy-MM-dd"),
            adults:            request.Adults,
            returnDate:        request.ReturnDate?.ToString("yyyy-MM-dd"),
            children:          request.Children > 0 ? request.Children : null,
            infants:           request.Infants > 0 ? request.Infants : null,
            travelClass:       request.TravelClass,
            nonStop:           request.NonStop,
            currencyCode:      request.CurrencyCode,
            max:               request.MaxResults,
            cancellationToken: ct);

        return raw.Data.Select(MapOffer).ToList();
    }

    public static UnifiedFlightOffer MapOffer(AmadeusFlightOffer o)
    {
        // CRITICAL: YQ and YR codes are carrier surcharges — separate from government taxes
        var surcharges = o.Price.Taxes
            .Where(t => t.Code is "YQ" or "YR")
            .Select(t => new PriceComponent(t.Code, decimal.Parse(t.Amount)))
            .ToList();
        var taxes = o.Price.Taxes
            .Where(t => t.Code is not "YQ" and not "YR")
            .Select(t => new PriceComponent(t.Code, decimal.Parse(t.Amount)))
            .ToList();

        var firstCabin = o.TravelerPricings
            .SelectMany(tp => tp.FareDetailsBySegment)
            .Select(fd => fd.Cabin)
            .FirstOrDefault() ?? "ECONOMY";

        // Offer expiry: Amadeus lastTicketingDate is a date, not time — use end of that day or +30min fallback
        var expiresAt = o.LastTicketingDate is not null
            ? (DateTimeOffset)DateOnly.Parse(o.LastTicketingDate).ToDateTime(TimeOnly.MaxValue, DateTimeKind.Utc)
            : DateTimeOffset.UtcNow.AddMinutes(30);

        return new UnifiedFlightOffer
        {
            Source     = "amadeus",
            SourceRef  = o.Id,
            ExpiresAt  = expiresAt,
            CabinClass = firstCabin,
            Price = new PriceBreakdown
            {
                Currency   = o.Price.Currency,
                Base       = decimal.Parse(o.Price.Base),
                Surcharges = surcharges,
                Taxes      = taxes,
            },
            Segments = o.Itineraries
                .SelectMany(i => i.Segments)
                .Select(s => new FlightSegment
                {
                    DepartureAirport = s.Departure.IataCode,
                    ArrivalAirport   = s.Arrival.IataCode,
                    DepartureAt      = s.Departure.At,
                    ArrivalAt        = s.Arrival.At,
                    CarrierCode      = s.CarrierCode,
                    FlightNumber     = s.Number,
                    AircraftCode     = s.Aircraft?.Code,
                    DurationMinutes  = ParseIsoDuration(s.Duration),
                })
                .ToList(),
        };
    }

    private static int ParseIsoDuration(string iso)
    {
        // Parse ISO 8601 duration "PT2H30M" manually — no external library needed
        var span = System.Xml.XmlConvert.ToTimeSpan(iso);
        return (int)span.TotalMinutes;
    }
}
