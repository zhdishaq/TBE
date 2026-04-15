namespace TBE.Contracts.Inventory.Models;
public sealed record UnifiedHotelOffer
{
    public Guid OfferId { get; init; } = Guid.NewGuid();
    public string Source { get; init; } = default!;
    public string SourceRef { get; init; } = default!;
    public DateTimeOffset ExpiresAt { get; init; }
    public string HotelCode { get; init; } = default!;
    public string PropertyName { get; init; } = default!;
    public string RoomType { get; init; } = default!;
    public string CancellationPolicy { get; init; } = default!;
    public PriceBreakdown Price { get; init; } = default!;
}
