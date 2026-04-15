namespace TBE.PricingService.Application.Rules.Models;
public sealed class MarkupRule
{
    public Guid Id { get; init; } = Guid.NewGuid();
    public string ProductType { get; set; } = default!;   // "flight" | "hotel" | "car"
    public string? AirlineCode { get; set; }               // null = all airlines
    public string? RouteOrigin { get; set; }               // null = all origins
    public MarkupType Type { get; set; }
    public decimal Value { get; set; }                     // 5.0 = 5% or £5 fixed
    public decimal? MaxAmount { get; set; }                // cap; null = uncapped
    public bool IsActive { get; set; } = true;
    public string Channel { get; set; } = "B2C";          // "B2C" | "B2B" | "ALL"
}
