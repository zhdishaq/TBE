using Microsoft.EntityFrameworkCore;
using TBE.Contracts.Inventory.Models;
using TBE.PricingService.Application.Rules;
using TBE.PricingService.Application.Rules.Models;

namespace TBE.PricingService.Infrastructure.Rules;

public sealed class MarkupRulesEngine(PricingDbContext db) : IPricingRulesEngine
{
    public async Task<PricedOffer> ApplyAsync(
        UnifiedFlightOffer rawOffer,
        PricingContext context,
        CancellationToken ct = default)
    {
        // Load active rules matching product type and channel
        var rules = await db.MarkupRules
            .Where(r => r.IsActive
                && r.ProductType == context.ProductType
                && (r.Channel == "ALL" || r.Channel == context.Channel))
            .ToListAsync(ct);

        // Priority: most-specific rule wins
        // 1. Airline + Origin specific
        // 2. Airline specific (no origin)
        // 3. Origin specific (no airline)
        // 4. Generic (no airline, no origin)
        var matchedRule = rules
            .Where(r =>
                (r.AirlineCode == null || r.AirlineCode == context.CarrierCode) &&
                (r.RouteOrigin == null || r.RouteOrigin == context.RouteOrigin))
            .OrderByDescending(r => (r.AirlineCode != null ? 2 : 0) + (r.RouteOrigin != null ? 1 : 0))
            .FirstOrDefault();

        var netFare = rawOffer.Price.GrandTotal;

        if (matchedRule is null)
        {
            return new PricedOffer
            {
                OfferId = rawOffer.OfferId, NetFare = netFare,
                MarkupAmount = 0m, GrossSelling = netFare,
                Currency = rawOffer.Price.Currency,
                AppliedRuleId = null, OriginalOffer = rawOffer,
            };
        }

        decimal markupAmount = matchedRule.Type switch
        {
            MarkupType.Percentage  => netFare * matchedRule.Value / 100m,
            MarkupType.FixedAmount => matchedRule.Value,
            _ => 0m
        };

        // Apply MaxAmount cap
        if (matchedRule.MaxAmount.HasValue && markupAmount > matchedRule.MaxAmount.Value)
            markupAmount = matchedRule.MaxAmount.Value;

        return new PricedOffer
        {
            OfferId       = rawOffer.OfferId,
            NetFare       = netFare,
            MarkupAmount  = markupAmount,
            GrossSelling  = netFare + markupAmount,
            Currency      = rawOffer.Price.Currency,
            AppliedRuleId = matchedRule.Id,
            OriginalOffer = rawOffer,
        };
    }
}
