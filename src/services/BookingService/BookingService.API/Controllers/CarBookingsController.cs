using System.Security.Claims;
using MassTransit;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using TBE.BookingService.Application.Cars;
using TBE.BookingService.Infrastructure;
using TBE.Contracts.Events;

namespace TBE.BookingService.API.Controllers;

/// <summary>
/// Plan 04-04 / CARB-01..03 — public car-hire booking API for the B2C portal. Thin
/// controller analog of <see cref="HotelBookingsController"/>. Class-level
/// <see cref="AuthorizeAttribute"/> enforces COMP-04 (every action rejects requests
/// without a valid Keycloak Bearer token). Ownership-plus-backoffice-bypass pattern
/// matches the other aggregates. Pricing is server-computed from <c>OfferId</c> via the
/// downstream saga — the request body's hint is never trusted (T-04-03-03 equivalent).
/// </summary>
[ApiController]
[Route("car-bookings")]
[Authorize]
public class CarBookingsController(
    BookingDbContext db,
    IPublishEndpoint publishEndpoint,
    ILogger<CarBookingsController> logger) : ControllerBase
{
    private const string BackofficeRole = "backoffice-staff";

    [HttpPost]
    public async Task<IActionResult> PostAsync([FromBody] CreateCarBookingRequest req, CancellationToken ct)
    {
        var errors = new List<string>();
        if (req is null)
            errors.Add("Body is required");
        else
        {
            if (req.OfferId == Guid.Empty) errors.Add("OfferId must be a valid GUID");
            if (req.DropoffAtUtc <= req.PickupAtUtc) errors.Add("DropoffAtUtc must be after PickupAtUtc");
            if (req.DriverAge is < 18 or > 99) errors.Add("DriverAge must be between 18 and 99");
            if (string.IsNullOrWhiteSpace(req.VendorName)) errors.Add("VendorName is required");
            if (string.IsNullOrWhiteSpace(req.PickupLocation)) errors.Add("PickupLocation is required");
            if (string.IsNullOrWhiteSpace(req.DropoffLocation)) errors.Add("DropoffLocation is required");
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
        var bookingReference = $"CB-{DateTime.UtcNow:yyMMdd}-{bookingId.ToString("N")[..8].ToUpperInvariant()}";

        // T-04-03-03 equivalent — server-computed pricing is resolved from OfferId in the saga.
        // We record 0m here and the saga downstream updates TotalAmount + SupplierRef on confirmation.
        var booking = new CarBooking
        {
            BookingId = bookingId,
            UserId = userId,
            OfferId = req!.OfferId,
            BookingReference = bookingReference,
            VendorName = req.VendorName,
            PickupLocation = req.PickupLocation,
            DropoffLocation = req.DropoffLocation,
            PickupAtUtc = req.PickupAtUtc,
            DropoffAtUtc = req.DropoffAtUtc,
            DriverAge = req.DriverAge,
            TotalAmount = 0m,
            Currency = "GBP",
            GuestEmail = req.Guest!.Email,
            GuestFullName = req.Guest.FullName,
            Status = "Pending",
            CreatedUtc = DateTime.UtcNow,
            UpdatedUtc = DateTime.UtcNow,
        };
        db.CarBookings.Add(booking);
        await db.SaveChangesAsync(ct);

        logger.LogInformation(
            "Publishing CarBookingInitiated bookingId={BookingId} ref={Ref} user={User} offer={Offer}",
            bookingId, bookingReference, userId, req.OfferId);

        await publishEndpoint.Publish(new CarBookingInitiated(
            BookingId: bookingId,
            UserId: userId,
            OfferId: req.OfferId,
            VendorName: req.VendorName,
            PickupLocation: req.PickupLocation,
            DropoffLocation: req.DropoffLocation,
            PickupAtUtc: req.PickupAtUtc,
            DropoffAtUtc: req.DropoffAtUtc,
            DriverAge: req.DriverAge,
            TotalAmount: 0m,
            Currency: "GBP",
            GuestEmail: req.Guest.Email,
            GuestFullName: req.Guest.FullName,
            At: DateTimeOffset.UtcNow), ct);

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

        var booking = await db.CarBookings
            .AsNoTracking()
            .FirstOrDefaultAsync(s => s.BookingId == id, ct);

        if (booking is null) return NotFound();

        if (booking.UserId != userId && !User.IsInRole(BackofficeRole))
        {
            logger.LogWarning(
                "Car booking access denied (IDOR guard) booking={BookingId} requester={User} owner={Owner}",
                id, userId, booking.UserId);
            return Forbid();
        }

        return Ok(new CarBookingDtoPublic(
            booking.BookingId,
            booking.Status,
            booking.BookingReference,
            booking.SupplierRef,
            booking.VendorName,
            booking.PickupLocation,
            booking.DropoffLocation,
            booking.PickupAtUtc,
            booking.DropoffAtUtc,
            booking.TotalAmount,
            booking.Currency,
            booking.CreatedUtc));
    }
}

/// <summary>
/// POST /car-bookings body. The server never trusts <c>TotalAmount</c> from the client —
/// pricing resolves from <c>OfferId</c> in the saga.
/// </summary>
public record CreateCarBookingRequest(
    Guid OfferId,
    string VendorName,
    string PickupLocation,
    string DropoffLocation,
    DateTime PickupAtUtc,
    DateTime DropoffAtUtc,
    int DriverAge,
    CarGuestRequest? Guest);

public record CarGuestRequest(string FullName, string Email, string? PhoneNumber);

/// <summary>
/// Public response DTO. Excludes UserId + PII (COMP-01/02). Exposes <c>SupplierRef</c> so
/// the voucher + dashboard can surface it (CARB-03).
/// </summary>
public record CarBookingDtoPublic(
    Guid Id,
    string Status,
    string BookingReference,
    string? SupplierRef,
    string VendorName,
    string PickupLocation,
    string DropoffLocation,
    DateTime PickupAtUtc,
    DateTime DropoffAtUtc,
    decimal TotalAmount,
    string Currency,
    DateTime CreatedUtc);
