using TBE.Contracts.Inventory.Models;
using TBE.PricingService.Application.Rules.Models;

namespace TBE.PricingService.Application.Rules;

public interface IPricingRulesEngine
{
    Task<PricedOffer> ApplyAsync(
        UnifiedFlightOffer rawOffer,
        PricingContext context,
        CancellationToken ct = default);
}
