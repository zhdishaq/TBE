namespace TBE.Contracts.Inventory.Models;
public sealed record UnifiedCarOffer
{
    public Guid OfferId { get; init; } = Guid.NewGuid();
    public string Source { get; init; } = default!;
    public string SourceRef { get; init; } = default!;
    public DateTimeOffset ExpiresAt { get; init; }
    public string VehicleCategory { get; init; } = default!;
    public string VehicleDescription { get; init; } = default!;
    public string SupplierName { get; init; } = default!;
    public PriceBreakdown Price { get; init; } = default!;
}
