namespace TBE.PricingService.Application.Rules.Models;
public sealed record PricingContext
{
    public string Channel { get; init; } = "B2C";         // "B2C" | "B2B"
    public Guid? AgencyId { get; init; }
    public string ProductType { get; init; } = "flight";  // "flight" | "hotel" | "car"
    public string? CarrierCode { get; init; }             // for airline-specific rules
    public string? RouteOrigin { get; init; }             // for route-specific rules
}
