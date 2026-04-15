namespace TBE.Contracts.Inventory.Models;
public sealed record UnifiedFlightOffer
{
    public Guid OfferId { get; init; } = Guid.NewGuid();
    public string Source { get; init; } = default!;      // "amadeus" | "sabre"
    public string SourceRef { get; init; } = default!;    // opaque GDS token for booking — stored in Redis
    public DateTimeOffset ExpiresAt { get; init; }        // from GDS offer expiry — use as Redis TTL
    public PriceBreakdown Price { get; init; } = default!;
    public IReadOnlyList<FlightSegment> Segments { get; init; } = [];
    public IReadOnlyList<FareRule> FareRules { get; init; } = [];
    public string CabinClass { get; init; } = default!;
    public int NumberOfStops => Segments.Count - 1;
}
