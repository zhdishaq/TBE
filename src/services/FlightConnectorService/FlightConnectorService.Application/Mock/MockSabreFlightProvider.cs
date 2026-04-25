using TBE.Contracts.Inventory;
using TBE.Contracts.Inventory.Models;

namespace TBE.FlightConnectorService.Application.Mock;

/// <summary>
/// Mock Sabre flight provider — returns realistic fake data for dev/testing
/// when real Sabre CERT PCC is not yet available.
/// Activated by setting Sabre:UseMock=true in appsettings or env.
/// </summary>
public sealed class MockSabreFlightProvider : IFlightAvailabilityProvider
{
    public string Name => "sabre";

    private static readonly Random Rng = new();

    private static readonly string[] Airlines = ["SV", "EK", "FZ", "GF", "WY", "RJ", "MS", "KU", "XY", "J9"];

    private static readonly Dictionary<string, int> RouteDurations = new()
    {
        ["JED-DXB"] = 150, ["DXB-JED"] = 150,
        ["JED-RUH"] = 75,  ["RUH-JED"] = 75,
        ["JED-AMM"] = 180, ["AMM-JED"] = 180,
        ["JED-CAI"] = 165, ["CAI-JED"] = 165,
        ["JED-KWI"] = 120, ["KWI-JED"] = 120,
        ["JED-BAH"] = 135, ["BAH-JED"] = 135,
        ["JED-MCT"] = 165, ["MCT-JED"] = 165,
        ["DXB-RUH"] = 90,  ["RUH-DXB"] = 90,
        ["DXB-AMM"] = 180, ["AMM-DXB"] = 180,
    };

    public Task<IReadOnlyList<UnifiedFlightOffer>> SearchAsync(
        FlightSearchRequest request, CancellationToken ct = default)
    {
        var offers = new List<UnifiedFlightOffer>();
        var routeKey = $"{request.Origin}-{request.Destination}";
        var baseDuration = RouteDurations.TryGetValue(routeKey, out var d) ? d : 180;

        // Generate 8-15 fake offers
        var count = Rng.Next(8, 16);
        var departureBase = request.DepartureDate.ToDateTime(TimeOnly.MinValue, DateTimeKind.Utc)
                            .AddHours(Rng.Next(5, 9));

        for (int i = 0; i < count; i++)
        {
            var airline    = Airlines[Rng.Next(Airlines.Length)];
            var flightNum  = Rng.Next(100, 999).ToString();
            var depTime    = departureBase.AddHours(i * Rng.Next(1, 3)).AddMinutes(Rng.Next(0, 60));
            var arrTime    = depTime.AddMinutes(baseDuration + Rng.Next(-10, 20));
            var basePrice  = 300m + (i * 45m) + (decimal)Rng.Next(0, 200);
            var taxes      = Math.Round(basePrice * 0.15m, 2);
            var yq         = Math.Round(basePrice * 0.05m, 2);
            var cabin      = request.TravelClass ?? new[] { "ECONOMY", "ECONOMY", "ECONOMY", "BUSINESS", "FIRST" }[Rng.Next(5)];
            var aircraft   = new[] { "73H", "77W", "32A", "321", "788", "359" }[Rng.Next(6)];
            var hasStop    = Rng.Next(0, 3) == 0 && i > 5;

            var segments = new List<FlightSegment>();

            if (!hasStop)
            {
                segments.Add(new FlightSegment
                {
                    DepartureAirport = request.Origin,
                    ArrivalAirport   = request.Destination,
                    DepartureAt      = new DateTimeOffset(depTime),
                    ArrivalAt        = new DateTimeOffset(arrTime),
                    CarrierCode      = airline,
                    FlightNumber     = $"{airline}{flightNum}",
                    AircraftCode     = aircraft,
                    DurationMinutes  = baseDuration,
                });
            }
            else
            {
                var via     = request.Origin == "JED" ? "DXB" : "RUH";
                var leg1Dur = baseDuration / 2;
                var layover = Rng.Next(60, 150);
                var leg2Dep = depTime.AddMinutes(leg1Dur + layover);
                var leg2Arr = leg2Dep.AddMinutes(leg1Dur);

                segments.Add(new FlightSegment
                {
                    DepartureAirport = request.Origin,
                    ArrivalAirport   = via,
                    DepartureAt      = new DateTimeOffset(depTime),
                    ArrivalAt        = new DateTimeOffset(depTime.AddMinutes(leg1Dur)),
                    CarrierCode      = airline,
                    FlightNumber     = $"{airline}{flightNum}",
                    AircraftCode     = aircraft,
                    DurationMinutes  = leg1Dur,
                });
                segments.Add(new FlightSegment
                {
                    DepartureAirport = via,
                    ArrivalAirport   = request.Destination,
                    DepartureAt      = new DateTimeOffset(leg2Dep),
                    ArrivalAt        = new DateTimeOffset(leg2Arr),
                    CarrierCode      = airline,
                    FlightNumber     = $"{airline}{Rng.Next(100, 999)}",
                    AircraftCode     = aircraft,
                    DurationMinutes  = leg1Dur,
                });
            }

            offers.Add(new UnifiedFlightOffer
            {
                Source    = "sabre",
                SourceRef = $"mock-sabre-{request.Origin}{request.Destination}-{i + 1:D3}",
                ExpiresAt = DateTimeOffset.UtcNow.AddMinutes(30),
                CabinClass = cabin,
                Price = new PriceBreakdown
                {
                    Currency   = request.CurrencyCode ?? "SAR",
                    Base       = Math.Round(basePrice, 2),
                    Surcharges = [new PriceComponent("YQ", yq)],
                    Taxes      = [new PriceComponent("XT", taxes)],
                },
                Segments = segments,
            });
        }

        // FIX: MaxResults is int (not int?) — use it directly, default 10
        var maxResults = request.MaxResults > 0 ? request.MaxResults : 10;

        IReadOnlyList<UnifiedFlightOffer> result = offers
            .OrderBy(o => o.Price.Base + o.Price.Surcharges.Sum(s => s.Amount) + o.Price.Taxes.Sum(t => t.Amount))
            .Take(maxResults)
            .ToList();

        return Task.FromResult(result);
    }
}
