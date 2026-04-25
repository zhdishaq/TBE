using Microsoft.Extensions.Logging;
using TBE.Contracts.Inventory;
using TBE.Contracts.Inventory.Models;
using TBE.FlightConnectorService.Application.Sabre.Models;
using Refit;

namespace TBE.FlightConnectorService.Application.Sabre;

public sealed class SabreFlightProvider(ISabreFlightApi api, ILogger<SabreFlightProvider> logger)
    : IFlightAvailabilityProvider
{
    public string Name => "sabre";

    public async Task<IReadOnlyList<UnifiedFlightOffer>> SearchAsync(
        FlightSearchRequest request, CancellationToken ct = default)
    {
        var body = BuildRequest(request);
        SabreBfmResponse raw;
        try
        {
            raw = await api.SearchAsync(body, ct);
        }
        catch (ApiException ex)
        {
            // Log full Sabre error response body for debugging
            var errorBody = await ex.GetContentAsAsync<object>() ?? ex.Content;
            logger.LogError("Sabre API error {StatusCode}: {ErrorBody}", (int)ex.StatusCode, errorBody);
            return [];
        }

        if (raw.GroupedItinerary is null) return [];

        var scheduleMap = raw.GroupedItinerary.ScheduleDescs.ToDictionary(s => s.Id);
        var taxMap      = raw.GroupedItinerary.TaxDescs.ToDictionary(t => t.Id);
        var fareMap     = raw.GroupedItinerary.FareComponentDescs.ToDictionary(f => f.Id);

        return raw.GroupedItinerary.ItineraryGroups
            .SelectMany(g => g.Itineraries)
            .Select(it => MapItinerary(it, scheduleMap, taxMap, fareMap))
            .ToList();
    }

    private static SabreBfmRequest BuildRequest(FlightSearchRequest req)
    {
        var ptqs = new List<SabrePtq>();
        if (req.Adults > 0)   ptqs.Add(new SabrePtq { Code = "ADT", Quantity = req.Adults });
        if (req.Children > 0) ptqs.Add(new SabrePtq { Code = "CNN", Quantity = req.Children });
        if (req.Infants > 0)  ptqs.Add(new SabrePtq { Code = "INF", Quantity = req.Infants });

        var origins = new List<SabreOriginDest>
        {
            new()
            {
                Rph = "1",
                DepartureDateTime   = req.DepartureDate.ToString("yyyy-MM-dd") + "T00:00:00",
                OriginLocation      = new SabreLocation { LocationCode = req.Origin },
                DestinationLocation = new SabreLocation { LocationCode = req.Destination },
            }
        };
        if (req.ReturnDate.HasValue)
            origins.Add(new SabreOriginDest
            {
                Rph = "2",
                DepartureDateTime   = req.ReturnDate.Value.ToString("yyyy-MM-dd") + "T00:00:00",
                OriginLocation      = new SabreLocation { LocationCode = req.Destination },
                DestinationLocation = new SabreLocation { LocationCode = req.Origin },
            });

        return new SabreBfmRequest
        {
            OtaRequest = new SabreOtaRequest
            {
                Version = "3.4.0",
                OriginDestinationInformation = origins,
                TravelerInfoSummary = new SabreTravelerInfo
                {
                    SeatsRequested = [req.Adults + req.Children],
                    AirTravelerAvail = [new SabreTravelerAvail { PassengerTypeQuantity = ptqs }]
                },
                TpaExtensions = new SabreTpaExtensions
                {
                    IntelliSellTransaction = new SabreIntelliSell
                    {
                        RequestType = new SabreRequestType { Name = "200ITINS" }
                    }
                }
            }
        };
    }

    public static UnifiedFlightOffer MapItinerary(
        SabreItinerary it,
        Dictionary<int, SabreScheduleDesc> scheduleMap,
        Dictionary<int, SabreTaxDesc> taxMap,
        Dictionary<int, SabreFareComponentDesc> fareMap)
    {
        var pricing = it.PricingInformation.FirstOrDefault();
        var currency = pricing?.Fare.TotalFare.Currency ?? "GBP";
        var totalAmount = pricing?.Fare.TotalFare.Amount ?? 0m;

        var allTaxes = (pricing?.Taxes ?? [])
            .Where(t => taxMap.ContainsKey(t.Ref))
            .Select(t => taxMap[t.Ref])
            .ToList();
        var surcharges = allTaxes
            .Where(t => t.Code is "YQ" or "YR")
            .Select(t => new PriceComponent(t.Code, t.Amount))
            .ToList();
        var taxes = allTaxes
            .Where(t => t.Code is not "YQ" and not "YR")
            .Select(t => new PriceComponent(t.Code, t.Amount))
            .ToList();
        var taxTotal   = allTaxes.Sum(t => t.Amount);
        var baseAmount = totalAmount - taxTotal;

        var cabin = pricing?.Fare.PassengerInfoList
            .FirstOrDefault()?.PassengerInfo.FareComponents
            .Select(fc => fareMap.TryGetValue(fc.Ref, out var fd) ? fd.Cabin : null)
            .FirstOrDefault(c => c is not null) ?? "ECONOMY";

        var segments = it.Legs
            .SelectMany(l => l.Schedules)
            .Where(s => scheduleMap.ContainsKey(s.Ref))
            .Select(s => scheduleMap[s.Ref])
            .Select(sd => new FlightSegment
            {
                DepartureAirport = sd.Departure.Airport,
                ArrivalAirport   = sd.Arrival.Airport,
                DepartureAt      = sd.Departure.Time,
                ArrivalAt        = sd.Arrival.Time,
                CarrierCode      = sd.Carrier.Marketing,
                FlightNumber     = sd.Carrier.MarketingFlightNumber.ToString(),
                AircraftCode     = sd.Carrier.Equipment?.Code,
                DurationMinutes  = sd.ElapsedTime,
            })
            .ToList();

        return new UnifiedFlightOffer
        {
            Source    = "sabre",
            SourceRef = $"sabre-{it.Id}",
            ExpiresAt = DateTimeOffset.UtcNow.AddMinutes(30),
            CabinClass = cabin,
            Price = new PriceBreakdown
            {
                Currency   = currency,
                Base       = baseAmount,
                Surcharges = surcharges,
                Taxes      = taxes,
            },
            Segments = segments,
        };
    }
}
