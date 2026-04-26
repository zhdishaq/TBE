using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using System.Net.Http.Json;
using System.Net.Http.Headers;
using TBE.Contracts.Inventory.Models;
using TBE.SearchService.Application.Cache;

namespace TBE.SearchService.API.Controllers;

[ApiController]
[Authorize]
[Route("search")]
public class SearchController(
    IHttpClientFactory httpClientFactory,
    ISearchCacheService cacheService,
    IConfiguration configuration) : ControllerBase
{
    [HttpPost("flights")]
    [AllowAnonymous]
    [EnableRateLimiting("gds-rate-limit")]
    public async Task<IActionResult> SearchFlights(
        [FromBody] FlightSearchRequest request, CancellationToken ct)
    {
        if (!System.Text.RegularExpressions.Regex.IsMatch(request.Origin, @"^[A-Z]{3}$") ||
            !System.Text.RegularExpressions.Regex.IsMatch(request.Destination, @"^[A-Z]{3}$"))
            return BadRequest("Origin and Destination must be valid 3-letter IATA codes.");

        var cacheKey = $"search:flights:{request.Origin}:{request.Destination}:" +
                       $"{request.DepartureDate:yyyy-MM-dd}:{request.Adults}:{request.TravelClass ?? "ECO"}";

        // Capture incoming Bearer token (if any) so we can forward it to
        // downstream services that require auth (flight-connector,
        // pricing-service). For anonymous searches this will be null and
        // we use a service-to-service token instead.
        var incomingBearer = ExtractBearerToken(HttpContext.Request);

        var offers = await cacheService.GetOrSearchAsync(
            cacheKey,
            async innerCt =>
            {
                // Cache miss: call FlightConnectorService (GDS fan-out)
                var connector = httpClientFactory.CreateClient("flight-connector");

                var token = incomingBearer ?? await GetServiceTokenAsync(innerCt);

                var connReq = new HttpRequestMessage(HttpMethod.Post, "/flights/search")
                {
                    Content = JsonContent.Create(request)
                };
                if (!string.IsNullOrEmpty(token))
                    connReq.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);

                var connResp = await connector.SendAsync(connReq, innerCt);
                connResp.EnsureSuccessStatusCode();
                var rawOffers = await connResp.Content
                    .ReadFromJsonAsync<IReadOnlyList<UnifiedFlightOffer>>(innerCt)
                    ?? [];

                // Apply pricing markup before caching (INV-09)
                var pricingClient = httpClientFactory.CreateClient("pricing-service");
                var pricingReq = new HttpRequestMessage(HttpMethod.Post, "/pricing/apply")
                {
                    Content = JsonContent.Create(new { Offers = rawOffers, Channel = "B2C" })
                };
                if (!string.IsNullOrEmpty(token))
                    pricingReq.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);

                var pricingResp = await pricingClient.SendAsync(pricingReq, innerCt);

                if (pricingResp.IsSuccessStatusCode)
                {
                    var pricedOffers = await pricingResp.Content
                        .ReadFromJsonAsync<IReadOnlyList<PricingServiceClient.PricedOfferDto>>(innerCt)
                        ?? [];

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

                return rawOffers;
            },
            isSelection: false,
            ct: ct);

        return Ok(offers);
    }

    [HttpPost("flights/select")]
    public async Task<IActionResult> SelectFlight(
        [FromBody] SelectFlightRequest request, CancellationToken ct)
    {
        var sessionId = Guid.NewGuid().ToString();
        await cacheService.StoreBookingTokenAsync(sessionId, request.Offer, ct);
        return Ok(new { SessionId = sessionId, ExpiresAt = request.Offer.ExpiresAt });
    }

    [HttpGet("flights/token/{sessionId}")]
    public async Task<IActionResult> GetBookingToken(string sessionId, CancellationToken ct)
    {
        var offer = await cacheService.GetBookingTokenAsync(sessionId, ct);
        if (offer is null) return NotFound(new { Error = "Booking token expired or not found." });
        return Ok(offer);
    }

    /// <summary>
    /// Extract the raw Bearer token from the incoming Authorization header.
    /// Returns null if no Authorization header is present (anonymous search).
    /// </summary>
    private static string? ExtractBearerToken(HttpRequest req)
    {
        var auth = req.Headers["Authorization"].ToString();
        if (string.IsNullOrEmpty(auth) || !auth.StartsWith("Bearer ", StringComparison.OrdinalIgnoreCase))
            return null;
        return auth["Bearer ".Length..].Trim();
    }

    /// <summary>
    /// Fallback: get a service-to-service token using client_credentials when
    /// the incoming request is anonymous. Uses the tbe-b2c-admin client because
    /// it has both the audience mapper (tbe-b2c-api) and the manage-users role.
    /// Token is cached implicitly by HttpClient/Keycloak for ~5 minutes.
    /// </summary>
    private async Task<string?> GetServiceTokenAsync(CancellationToken ct)
    {
        try
        {
            var authority = configuration["Keycloak:Authority"];
            var clientId = configuration["Keycloak:ServiceClientId"] ?? "tbe-b2c-admin";
            var clientSecret = configuration["Keycloak:ServiceClientSecret"];
            if (string.IsNullOrEmpty(clientSecret) || string.IsNullOrEmpty(authority))
                return null;

            using var client = new HttpClient();
            var resp = await client.PostAsync($"{authority}/protocol/openid-connect/token",
                new FormUrlEncodedContent(new Dictionary<string, string>
                {
                    ["grant_type"] = "client_credentials",
                    ["client_id"] = clientId,
                    ["client_secret"] = clientSecret
                }), ct);

            if (!resp.IsSuccessStatusCode) return null;
            var json = await resp.Content.ReadFromJsonAsync<TokenResponse>(ct);
            return json?.access_token;
        }
        catch
        {
            return null;
        }
    }

    private sealed record TokenResponse(string? access_token, int? expires_in);
}

public sealed class SelectFlightRequest
{
    public UnifiedFlightOffer Offer { get; init; } = default!;
}

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
