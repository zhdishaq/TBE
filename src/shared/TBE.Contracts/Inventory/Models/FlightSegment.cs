namespace TBE.Contracts.Inventory.Models;
public sealed record FlightSegment
{
    public string DepartureAirport { get; init; } = default!;
    public string ArrivalAirport { get; init; } = default!;
    public DateTimeOffset DepartureAt { get; init; }
    public DateTimeOffset ArrivalAt { get; init; }
    public string CarrierCode { get; init; } = default!;
    public string FlightNumber { get; init; } = default!;
    public string? AircraftCode { get; init; }
    public int DurationMinutes { get; init; }
}
