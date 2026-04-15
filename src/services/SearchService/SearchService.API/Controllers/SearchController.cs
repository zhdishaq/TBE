using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using System.Net.Http.Json;
using TBE.Contracts.Inventory.Models;
using TBE.SearchService.Application.Cache;

namespace TBE.SearchService.API.Controllers;

[ApiController]
[Route("search")]
public class SearchController(
    IHttpClientFactory httpClientFactory,
    ISearchCacheService cacheService) : ControllerBase
{
    [HttpPost("flights")]
    // NOTE: In-process rate limit. Per-replica only. Replace with Redis sliding window before scaling.
    [EnableRateLimiting("gds-rate-limit")]   // sliding window policy — T-02-04-03
    public async Task<IActionResult> SearchFlights(
        [FromBody] FlightSearchRequest request, CancellationToken ct)
    {
        if (!System.Text.RegularExpressions.Regex.IsMatch(request.Origin, @"^[A-Z]{3}$") ||
            !System.Text.RegularExpressions.Regex.IsMatch(request.Destination, @"^[A-Z]{3}$"))
            return BadRequest("Origin and Destination must be valid 3-letter IATA codes.");

        // Cache key from validated inputs only — T-02-04-01
        var cacheKey = $"search:flights:{request.Origin}:{request.Destination}:" +
                       $"{request.DepartureDate:yyyy-MM-dd}:{request.Adults}:{request.TravelClass ?? "ECO"}";

        var offers = await cacheService.GetOrSearchAsync(
            cacheKey,
            async innerCt =>
            {
                // Cache miss: call FlightConnectorService (GDS fan-out)
                var connector = httpClientFactory.CreateClient("flight-connector");
                var connResp = await connector.PostAsJsonAsync("/flights/search", request, innerCt);
                connResp.EnsureSuccessStatusCode();
                var rawOffers = await connResp.Content
                    .ReadFromJsonAsync<IReadOnlyList<UnifiedFlightOffer>>(innerCt)
                    ?? [];

                // Apply pricing markup before caching (INV-09)
                var pricingClient = httpClientFactory.CreateClient("pricing-service");
                var pricingResp = await pricingClient.PostAsJsonAsync(
                    "/pricing/apply",
                    new { Offers = rawOffers, Channel = "B2C" },
                    innerCt);

                if (pricingResp.IsSuccessStatusCode)
                {
                    var pricedOffers = await pricingResp.Content
                        .ReadFromJsonAsync<IReadOnlyList<PricingServiceClient.PricedOfferDto>>(innerCt)
                        ?? [];

                    // Map priced results back to UnifiedFlightOffer with GrossSelling applied
                    var pricedResult = rawOffers.Select(raw =>
                    {
                        var priced = pricedOffers.FirstOrDefault(p => p.OfferId == raw.OfferId);
                        if (priced is null) return raw;
                        return raw with
                        {
                            Price = raw.Price with
                            {
                                GrossSellingPrice = priced.GrossSelling,
                                MarkupApplied = true,
                            }
                        };
                    }).ToList();

                    return (IReadOnlyList<UnifiedFlightOffer>)pricedResult;
                }

                // Pricing service unavailable — return raw offers rather than failing the search
                return rawOffers;
            },
            isSelection: false,
            ct: ct);

        return Ok(offers);
    }

    /// <summary>
    /// Selection endpoint: customer selects a specific offer.
    /// Stores booking token in Redis with offer.ExpiresAt TTL.
    /// Returns a session ID for the booking saga to retrieve the token.
    /// </summary>
    [HttpPost("flights/select")]
    public async Task<IActionResult> SelectFlight(
        [FromBody] SelectFlightRequest request, CancellationToken ct)
    {
        var sessionId = Guid.NewGuid().ToString();
        await cacheService.StoreBookingTokenAsync(sessionId, request.Offer, ct);
        return Ok(new { SessionId = sessionId, ExpiresAt = request.Offer.ExpiresAt });
    }

    /// <summary>
    /// Booking saga retrieval: get the fare snapshot stored at selection time.
    /// </summary>
    [HttpGet("flights/token/{sessionId}")]
    public async Task<IActionResult> GetBookingToken(string sessionId, CancellationToken ct)
    {
        var offer = await cacheService.GetBookingTokenAsync(sessionId, ct);
        if (offer is null) return NotFound(new { Error = "Booking token expired or not found." });
        return Ok(offer);
    }
}

public sealed class SelectFlightRequest
{
    public UnifiedFlightOffer Offer { get; init; } = default!;
}

// Internal DTO for deserializing PricingService /pricing/apply response
// Mirrors PricedOffer from PricingService.Application but does not reference that project (service boundary)
internal static class PricingServiceClient
{
    public sealed class PricedOfferDto
    {
        public Guid OfferId { get; init; }
        public decimal GrossSelling { get; init; }
        public decimal NetFare { get; init; }
        public decimal MarkupAmount { get; init; }
        public string Currency { get; init; } = default!;
    }
}
