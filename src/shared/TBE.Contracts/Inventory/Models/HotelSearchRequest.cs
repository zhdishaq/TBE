namespace TBE.Contracts.Inventory.Models;
public sealed record HotelSearchRequest
{
    public string DestinationCode { get; init; } = default!;  // IATA city code
    public DateOnly CheckIn { get; init; }
    public DateOnly CheckOut { get; init; }
    public IReadOnlyList<RoomOccupancy> Rooms { get; init; } = [];
    public string CurrencyCode { get; init; } = "GBP";
}
public sealed record RoomOccupancy(int Adults, int Children);
