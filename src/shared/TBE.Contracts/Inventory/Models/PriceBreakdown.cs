namespace TBE.Contracts.Inventory.Models;
public sealed record PriceComponent(string Code, decimal Amount);
public sealed record PriceBreakdown
{
    public string Currency { get; init; } = default!;
    public decimal Base { get; init; }
    public IReadOnlyList<PriceComponent> Surcharges { get; init; } = [];  // carrier surcharges only
    public IReadOnlyList<PriceComponent> Taxes { get; init; } = [];       // government taxes
    public decimal GrandTotal => Base + Surcharges.Sum(s => s.Amount) + Taxes.Sum(t => t.Amount);
    public decimal? GrossSellingPrice { get; init; }  // set when markup has been applied; null = raw net fare shown
    public bool MarkupApplied { get; init; }          // true when PricingService has applied a rule
}
