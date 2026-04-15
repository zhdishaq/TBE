namespace TBE.Contracts.Inventory.Models;
public sealed record FlightSearchRequest
{
    public string Origin { get; init; } = default!;           // IATA 3-letter code, e.g. "LHR"
    public string Destination { get; init; } = default!;      // IATA 3-letter code, e.g. "BKK"
    public DateOnly DepartureDate { get; init; }
    public DateOnly? ReturnDate { get; init; }
    public int Adults { get; init; } = 1;
    public int Children { get; init; }
    public int Infants { get; init; }
    public string? TravelClass { get; init; }                 // "ECONOMY" | "PREMIUM_ECONOMY" | "BUSINESS" | "FIRST"
    public bool? NonStop { get; init; }
    public string CurrencyCode { get; init; } = "GBP";
    public int MaxResults { get; init; } = 50;
}
