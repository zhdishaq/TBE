using TBE.Contracts.Inventory.Models;
namespace TBE.PricingService.Application.Rules.Models;
public sealed record PricedOffer
{
    public Guid OfferId { get; init; }
    public decimal NetFare { get; init; }          // raw GDS price (GrandTotal)
    public decimal MarkupAmount { get; init; }     // how much markup was applied
    public decimal GrossSelling { get; init; }     // net + markup (shown to customer)
    public string Currency { get; init; } = default!;
    public Guid? AppliedRuleId { get; init; }      // null if no rule matched
    public UnifiedFlightOffer OriginalOffer { get; init; } = default!;
}
