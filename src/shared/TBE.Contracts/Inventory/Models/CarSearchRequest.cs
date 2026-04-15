namespace TBE.Contracts.Inventory.Models;
public sealed record CarSearchRequest
{
    public string PickupLocationCode { get; init; } = default!;  // IATA airport code
    public string DropoffLocationCode { get; init; } = default!;
    public DateTimeOffset PickupDateTime { get; init; }
    public DateTimeOffset DropoffDateTime { get; init; }
    public string? VehicleCategory { get; init; }
    public string CurrencyCode { get; init; } = "GBP";
}
