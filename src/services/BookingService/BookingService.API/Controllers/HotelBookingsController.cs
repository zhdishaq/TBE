using System.Net.Http.Headers;
using System.Security.Claims;
using MassTransit;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using TBE.BookingService.Application.Saga;
using TBE.BookingService.Infrastructure;
using TBE.Contracts.Events;

namespace TBE.BookingService.API.Controllers;

/// <summary>
/// Plan 04-03 / HOTB-01..05 — public hotel-booking API for the B2C portal.
/// Class-level <see cref="AuthorizeAttribute"/> enforces COMP-04 (every action
/// rejects requests without a valid Keycloak Bearer token). Voucher downloads
/// stream through from NotificationService per CONTEXT D-16 — BookingService
/// never regenerates the PDF (single source of truth in NotificationService).
/// Mirrors the ownership pattern from <see cref="BookingsController"/>:
/// caller must be the booking owner or a backoffice-staff user.
/// </summary>
[ApiController]
[Route("hotel-bookings")]
[Authorize]
public class HotelBookingsController(
    BookingDbContext db,
    IPublishEndpoint publishEndpoint,
    IHttpClientFactory httpFactory,
    ILogger<HotelBookingsController> logger) : ControllerBase
{
    private const string BackofficeRole = "backoffice-staff";

    /// <summary>HttpClient name registered in Program.cs for NotificationService voucher streaming.</summary>
    public const string NotificationClientName = "notification-service";

    [HttpPost]
    public async Task<IActionResult> PostAsync([FromBody] CreateHotelBookingRequest req, CancellationToken ct)
    {
        var errors = new List<string>();
        if (req is null)
            errors.Add("Body is required");
        else
        {
            if (req.OfferId == Guid.Empty) errors.Add("OfferId must be a valid GUID");
            if (req.Rooms is < 1 or > 5) errors.Add("Rooms must be between 1 and 5");
            if (req.Adults is < 1 or > 9) errors.Add("Adults must be between 1 and 9");
            if (req.Children is < 0 or > 4) errors.Add("Children must be between 0 and 4");
            if (req.CheckOutDate <= req.CheckInDate) errors.Add("CheckOutDate must be after CheckInDate");
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

        var bookingId = Guid.NewGuid();
        var bookingReference = $"HB-{DateTime.UtcNow:yyMMdd}-{bookingId.ToString("N")[..8].ToUpperInvariant()}";

        // T-04-03-03 — server owns pricing; TotalAmount is resolved downstream from OfferId, not the request body.
        var state = new HotelBookingSagaState
        {
            CorrelationId = bookingId,
            UserId = userId,
            BookingReference = bookingReference,
            PropertyName = "",   // filled by saga once the offer is resolved
            AddressLine = "",
            CheckInDate = req!.CheckInDate,
            CheckOutDate = req.CheckOutDate,
            Rooms = req.Rooms,
            Adults = req.Adults,
            Children = req.Children,
            TotalAmount = 0m,
            Currency = "GBP",
            GuestEmail = req.Guest!.Email,
            GuestFullName = req.Guest.FullName,
            Status = "Pending",
            InitiatedAtUtc = DateTime.UtcNow,
        };
        db.HotelBookingSagaStates.Add(state);
        await db.SaveChangesAsync(ct);

        logger.LogInformation(
            "Publishing HotelBookingInitiated bookingId={BookingId} ref={Ref} user={User} offer={Offer}",
            bookingId, bookingReference, userId, req.OfferId);

        await publishEndpoint.Publish(new HotelBookingInitiated(
            bookingId,
            userId,
            req.OfferId,
            new HotelGuestDto(req.Guest.FullName, req.Guest.Email, req.Guest.PhoneNumber),
            DateTimeOffset.UtcNow), ct);

        return AcceptedAtAction(
            nameof(GetStatusAsync),
            new { id = bookingId },
            new { bookingId, status = "Pending" });
    }

    [HttpGet("{id:guid}")]
    public async Task<IActionResult> GetStatusAsync(Guid id, CancellationToken ct)
    {
        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier)
            ?? User.FindFirstValue("sub")
            ?? string.Empty;

        var booking = await db.HotelBookingSagaStates
            .AsNoTracking()
            .FirstOrDefaultAsync(s => s.CorrelationId == id, ct);

        if (booking is null) return NotFound();

        // T-04-03-01 — owner-only, plus backoffice-staff bypass.
        if (booking.UserId != userId && !User.IsInRole(BackofficeRole))
        {
            logger.LogWarning(
                "Hotel booking access denied (IDOR guard) booking={BookingId} requester={User} owner={Owner}",
                id, userId, booking.UserId);
            return Forbid();
        }

        // Public DTO — never exposes UserId.
        return Ok(new HotelBookingDtoPublic(
            booking.CorrelationId,
            booking.Status,
            booking.BookingReference,
            booking.SupplierRef,
            booking.PropertyName,
            booking.TotalAmount,
            booking.Currency,
            booking.CheckInDate,
            booking.CheckOutDate,
            booking.InitiatedAtUtc,
            booking.ConfirmedAtUtc));
    }

    /// <summary>
    /// HOTB-04 voucher download. Authorization first (ownership + confirmed status),
    /// then a STREAMING pass-through to NotificationService (D-16 / Pitfall 14 — we never
    /// regenerate or buffer the PDF here).
    /// </summary>
    [HttpGet("{id:guid}/voucher.pdf")]
    public async Task<IActionResult> GetVoucherAsync(Guid id, CancellationToken ct)
    {
        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier)
            ?? User.FindFirstValue("sub")
            ?? string.Empty;

        var booking = await db.HotelBookingSagaStates
            .AsNoTracking()
            .FirstOrDefaultAsync(s => s.CorrelationId == id, ct);

        if (booking is null) return NotFound();

        if (booking.UserId != userId && !User.IsInRole(BackofficeRole))
        {
            logger.LogWarning(
                "Hotel voucher access denied (IDOR guard) booking={BookingId} requester={User} owner={Owner}",
                id, userId, booking.UserId);
            return Forbid();
        }

        if (!string.Equals(booking.Status, "Confirmed", StringComparison.Ordinal))
        {
            // 404 (not 409) per CONTEXT — voucher literally does not exist until supplier confirms.
            return NotFound();
        }

        // Streaming pass-through — D-16 / Pitfall 14. NotificationService is the single PDF source.
        var client = httpFactory.CreateClient(NotificationClientName);
        using var upstreamReq = new HttpRequestMessage(HttpMethod.Get, $"/notifications/hotel-voucher/{id}.pdf");

        // Forward caller's bearer — T-04-03-08 (never accept token from query string).
        var auth = Request.Headers.Authorization.ToString();
        if (!string.IsNullOrEmpty(auth) && AuthenticationHeaderValue.TryParse(auth, out var parsed))
        {
            upstreamReq.Headers.Authorization = parsed;
        }

        var upstream = await client.SendAsync(upstreamReq, HttpCompletionOption.ResponseHeadersRead, ct);
        if (!upstream.IsSuccessStatusCode)
        {
            logger.LogWarning(
                "Voucher upstream returned {Status} for booking {BookingId}", upstream.StatusCode, id);
            return StatusCode((int)upstream.StatusCode);
        }

        Response.ContentType = upstream.Content.Headers.ContentType?.MediaType ?? "application/pdf";
        if (upstream.Content.Headers.ContentDisposition is { } cd)
        {
            Response.Headers["Content-Disposition"] = cd.ToString();
        }
        else
        {
            Response.Headers["Content-Disposition"] = $"attachment; filename=\"voucher-{booking.BookingReference}.pdf\"";
        }

        await upstream.Content.CopyToAsync(Response.Body, ct);
        return new EmptyResult();
    }
}

/// <summary>
/// POST /hotel-bookings body. The server never trusts <c>TotalAmount</c> from the
/// client (T-04-03-03) — pricing resolves from <c>OfferId</c> in the saga.
/// </summary>
public record CreateHotelBookingRequest(
    Guid OfferId,
    DateOnly CheckInDate,
    DateOnly CheckOutDate,
    int Rooms,
    int Adults,
    int Children,
    HotelGuestRequest? Guest);

public record HotelGuestRequest(string FullName, string Email, string? PhoneNumber);

/// <summary>
/// Public response DTO. Excludes UserId + PII (COMP-01/02). Exposes
/// <c>SupplierRef</c> so HOTB-05 dashboard + voucher copy can surface it.
/// </summary>
public record HotelBookingDtoPublic(
    Guid Id,
    string Status,
    string BookingReference,
    string? SupplierRef,
    string PropertyName,
    decimal TotalAmount,
    string Currency,
    DateOnly CheckInDate,
    DateOnly CheckOutDate,
    DateTime InitiatedAtUtc,
    DateTime? ConfirmedAtUtc);
