using Microsoft.AspNetCore.Mvc;
using TBE.Contracts.Inventory.Models;
using TBE.PricingService.Application.Rules;
using TBE.PricingService.Application.Rules.Models;

namespace TBE.PricingService.API.Controllers;

[ApiController]
[Route("pricing")]
public class PricingController(IPricingRulesEngine engine) : ControllerBase
{
    public sealed class ApplyPricingRequest
    {
        public IReadOnlyList<UnifiedFlightOffer> Offers { get; init; } = [];
        public string Channel { get; init; } = "B2C";
        public Guid? AgencyId { get; init; }
    }

    [HttpPost("apply")]
    public async Task<IActionResult> Apply([FromBody] ApplyPricingRequest request, CancellationToken ct)
    {
        if (request.Offers.Count == 0) return Ok(Array.Empty<PricedOffer>());

        var results = await Task.WhenAll(request.Offers.Select(offer =>
            engine.ApplyAsync(offer, new PricingContext
            {
                Channel = request.Channel,
                AgencyId = request.AgencyId,
                ProductType = "flight",
                CarrierCode = offer.Segments.Count > 0 ? offer.Segments[0].CarrierCode : null,
                RouteOrigin = offer.Segments.Count > 0 ? offer.Segments[0].DepartureAirport : null,
            }, ct)));

        return Ok(results);
    }
}
