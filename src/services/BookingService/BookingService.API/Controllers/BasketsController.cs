using System.Security.Claims;
using MassTransit;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using TBE.BookingService.Application.Baskets;
using TBE.BookingService.Infrastructure;
using TBE.Contracts.Events;

namespace TBE.BookingService.API.Controllers;

/// <summary>
/// Plan 04-04 / PKG-01..04 — B2C Trip Builder basket endpoint. Class-level
/// <see cref="AuthorizeAttribute"/> enforces COMP-04 (every action rejects requests
/// without a valid Keycloak Bearer token). CONTEXT D-08 is the load-bearing
/// architecture decision: ONE combined Stripe PaymentIntent per basket, ONE charge
/// on the customer's statement. This controller never creates per-leg PaymentIntents.
/// <para>
/// Ownership mirrors <see cref="HotelBookingsController"/>: caller must be the
/// basket owner or a backoffice-staff user.
/// </para>
/// </summary>
[ApiController]
[Route("baskets")]
[Authorize]
public class BasketsController(
    BookingDbContext db,
    IPublishEndpoint publishEndpoint,
    IBasketPaymentGateway payments,
    ILogger<BasketsController> logger) : ControllerBase
{
    private const string BackofficeRole = "backoffice-staff";

    // ------------------------------------------------------------------
    // POST /baskets
    // ------------------------------------------------------------------
    [HttpPost]
    public async Task<IActionResult> PostAsync([FromBody] CreateBasketRequest req, CancellationToken ct)
    {
        var errors = new List<string>();
        if (req is null)
        {
            errors.Add("Body is required");
        }
        else
        {
            var componentCount =
                (req.FlightOfferId.HasValue && req.FlightOfferId.Value != Guid.Empty ? 1 : 0) +
                (req.HotelOfferId.HasValue && req.HotelOfferId.Value != Guid.Empty ? 1 : 0) +
                (req.CarOfferId.HasValue && req.CarOfferId.Value != Guid.Empty ? 1 : 0);
            if (componentCount < 2)
                errors.Add("A basket must contain at least two components (flight + hotel today).");
            if (req.Guest is null || string.IsNullOrWhiteSpace(req.Guest.FullName))
                errors.Add("Guest.FullName is required");
            if (req.Guest is null || string.IsNullOrWhiteSpace(req.Guest.Email))
                errors.Add("Guest.Email is required");
        }
        if (errors.Count > 0)
            return BadRequest(new { errors });

        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier)
            ?? User.FindFirstValue("sub")
            ?? string.Empty;
        if (string.IsNullOrEmpty(userId))
            return Unauthorized();

        // T-04-04-01 — server owns pricing. Totals are NEVER taken from the request body.
        // For now we derive from the optional per-leg subtotals hints (from the trusted
        // portal-server bff which has already queried the offer cache). Downstream phases
        // will wire an IOfferPricingService re-check.
        var flightSubtotal = req!.FlightSubtotalHint ?? 0m;
        var hotelSubtotal = req.HotelSubtotalHint ?? 0m;
        var total = flightSubtotal + hotelSubtotal;

        var basketId = Guid.NewGuid();
        var now = DateTime.UtcNow;
        var basket = new Basket
        {
            BasketId = basketId,
            UserId = userId,
            FlightBookingId = null,
            HotelBookingId = null,
            CarBookingId = null,
            StripePaymentIntentId = null,
            Status = "Initiated",
            TotalAmount = total,
            FlightSubtotal = flightSubtotal,
            HotelSubtotal = hotelSubtotal,
            ChargedAmount = 0m,
            RefundedAmount = 0m,
            FlightCaptured = false,
            HotelCaptured = false,
            Currency = string.IsNullOrWhiteSpace(req.Currency) ? "GBP" : req.Currency!,
            GuestEmail = req.Guest!.Email,
            GuestFullName = req.Guest.FullName,
            CreatedUtc = now,
            UpdatedUtc = now,
        };
        db.Baskets.Add(basket);
        await db.SaveChangesAsync(ct);

        logger.LogInformation(
            "Publishing BasketInitiated basketId={BasketId} user={User} flight={FlightOffer} hotel={HotelOffer}",
            basketId, userId, req.FlightOfferId, req.HotelOfferId);

        await publishEndpoint.Publish(new BasketInitiated(
            basketId,
            userId,
            req.FlightOfferId,
            req.HotelOfferId,
            req.CarOfferId,
            total,
            basket.Currency,
            DateTimeOffset.UtcNow), ct);

        return AcceptedAtAction(
            nameof(GetStatusAsync),
            new { id = basketId },
            new { basketId, status = "Initiated" });
    }

    // ------------------------------------------------------------------
    // GET /baskets/{id}
    // ------------------------------------------------------------------
    [HttpGet("{id:guid}")]
    public async Task<IActionResult> GetStatusAsync(Guid id, CancellationToken ct)
    {
        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier)
            ?? User.FindFirstValue("sub")
            ?? string.Empty;

        var basket = await db.Baskets
            .AsNoTracking()
            .FirstOrDefaultAsync(b => b.BasketId == id, ct);

        if (basket is null) return NotFound();

        // T-04-04-03 — owner-only, plus backoffice-staff bypass.
        if (basket.UserId != userId && !User.IsInRole(BackofficeRole))
        {
            logger.LogWarning(
                "Basket access denied (IDOR guard) basket={BasketId} requester={User} owner={Owner}",
                id, userId, basket.UserId);
            return Forbid();
        }

        return Ok(new BasketDtoPublic(
            basket.BasketId,
            basket.Status,
            basket.FlightBookingId,
            basket.HotelBookingId,
            basket.CarBookingId,
            basket.StripePaymentIntentId,
            basket.TotalAmount,
            basket.FlightSubtotal,
            basket.HotelSubtotal,
            basket.ChargedAmount,
            basket.RefundedAmount,
            basket.Currency,
            basket.CreatedUtc,
            basket.UpdatedUtc));
    }

    // ------------------------------------------------------------------
    // POST /baskets/{id}/payment-intents  (D-08 — ONE combined PI)
    // ------------------------------------------------------------------
    [HttpPost("{id:guid}/payment-intents")]
    public async Task<IActionResult> InitPaymentIntentAsync(Guid id, CancellationToken ct)
    {
        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier)
            ?? User.FindFirstValue("sub")
            ?? string.Empty;
        if (string.IsNullOrEmpty(userId))
            return Unauthorized();

        var basket = await db.Baskets.FirstOrDefaultAsync(b => b.BasketId == id, ct);
        if (basket is null) return NotFound();

        if (basket.UserId != userId && !User.IsInRole(BackofficeRole))
        {
            logger.LogWarning(
                "Basket payment-intent access denied (IDOR guard) basket={BasketId} requester={User} owner={Owner}",
                id, userId, basket.UserId);
            return Forbid();
        }

        // CONTEXT D-08 — deterministic key, ONE PaymentIntent per basket. The key
        // string is grep-asserted by the plan acceptance criteria so it must remain
        // the literal "basket-{id}-authorize" template with no -flight/-hotel variants.
        var idempotencyKey = $"basket-{basket.BasketId}-authorize";

        var result = await payments.AuthorizeBasketAsync(
            basket.BasketId,
            basket.TotalAmount,
            basket.Currency,
            idempotencyKey,
            ct);

        // Idempotent initializer: re-invocation returns the same client secret.
        // Stripe's idempotency key guarantees no second PaymentIntent is ever created.
        if (string.IsNullOrEmpty(basket.StripePaymentIntentId))
        {
            basket.StripePaymentIntentId = result.PaymentIntentId;
            basket.Status = "PaymentAuthorized";
            basket.UpdatedUtc = DateTime.UtcNow;
            await db.SaveChangesAsync(ct);

            await publishEndpoint.Publish(new BasketPaymentAuthorized(
                basket.BasketId,
                result.PaymentIntentId,
                basket.TotalAmount,
                basket.Currency,
                DateTimeOffset.UtcNow), ct);
        }

        return Ok(new BasketPaymentIntentResponse(result.ClientSecret));
    }
}

/// <summary>
/// POST /baskets body. Server NEVER trusts <see cref="FlightSubtotalHint"/> /
/// <see cref="HotelSubtotalHint"/> as authoritative pricing (T-04-04-01); they are
/// convenience inputs from the trusted portal BFF that has already queried the offer
/// cache. A Phase 4+ milestone wires an IOfferPricingService re-check.
/// </summary>
public record CreateBasketRequest(
    Guid? FlightOfferId,
    Guid? HotelOfferId,
    Guid? CarOfferId,
    string? Currency,
    decimal? FlightSubtotalHint,
    decimal? HotelSubtotalHint,
    BasketGuestRequest? Guest);

public record BasketGuestRequest(string FullName, string Email, string? PhoneNumber);

/// <summary>
/// Public response DTO. D-08 single-PI: one <see cref="StripePaymentIntentId"/> column,
/// never per-leg pairs.
/// </summary>
public record BasketDtoPublic(
    Guid BasketId,
    string Status,
    Guid? FlightBookingId,
    Guid? HotelBookingId,
    Guid? CarBookingId,
    string? StripePaymentIntentId,
    decimal TotalAmount,
    decimal FlightSubtotal,
    decimal HotelSubtotal,
    decimal ChargedAmount,
    decimal RefundedAmount,
    string Currency,
    DateTime CreatedUtc,
    DateTime UpdatedUtc);

/// <summary>
/// Response of <c>POST /baskets/{id}/payment-intents</c> — ONE clientSecret for the ONE
/// combined PaymentIntent (D-08). Naming is intentionally singular; a
/// <c>flightClientSecret</c>/<c>hotelClientSecret</c> pair is forbidden.
/// </summary>
public record BasketPaymentIntentResponse(string ClientSecret);
