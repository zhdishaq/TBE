using System.Security.Claims;
using MassTransit;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using TBE.BookingService.Infrastructure;
using TBE.Contracts.Events;

namespace TBE.BookingService.API.Controllers;

/// <summary>
/// Public booking API for B2C + B2B channels. Class-level <see cref="AuthorizeAttribute"/>
/// enforces COMP-04 — every action rejects requests without a valid Keycloak Bearer token.
/// All mutations are published via the EF Core outbox (IPublishEndpoint) so the saga consumes
/// events inside an atomically-committed outbox transaction.
/// All GET responses are DTO projections; no passport/document PII is included (D-20 + COMP-01/02).
/// </summary>
[ApiController]
[Route("bookings")]
[Authorize]
public class BookingsController(
    BookingDbContext db,
    IPublishEndpoint publishEndpoint,
    ILogger<BookingsController> logger) : ControllerBase
{
    private const string BackofficeRole = "backoffice-staff";

    [HttpPost]
    public async Task<IActionResult> PostAsync([FromBody] CreateBookingRequest req, CancellationToken ct)
    {
        var errors = new List<string>();
        if (req.ProductType is not ("flight" or "hotel" or "car"))
            errors.Add("ProductType must be flight|hotel|car");
        if (string.IsNullOrWhiteSpace(req.Currency) || req.Currency.Length != 3)
            errors.Add("Currency must be a 3-letter ISO code");
        if (req.TotalAmount <= 0)
            errors.Add("TotalAmount must be positive");
        if (req.PaymentMethod is not ("card" or "wallet"))
            errors.Add("PaymentMethod must be card|wallet");
        if (errors.Count > 0)
            return BadRequest(new { errors });

        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier)
            ?? User.FindFirstValue("sub")
            ?? string.Empty;
        if (string.IsNullOrEmpty(userId))
            return Unauthorized();

        var bookingId = Guid.NewGuid();
        var bookingReference = $"TBE-{DateTime.UtcNow:yyMMdd}-{bookingId.ToString("N")[..8].ToUpperInvariant()}";

        logger.LogInformation(
            "Publishing BookingInitiated bookingId={BookingId} ref={Ref} user={User}",
            bookingId, bookingReference, userId);

        await publishEndpoint.Publish(new BookingInitiated(
            bookingId, req.ProductType, req.Channel, userId, bookingReference,
            req.TotalAmount, req.Currency, req.PaymentMethod, req.WalletId,
            DateTimeOffset.UtcNow), ct);

        return AcceptedAtAction(nameof(GetByIdAsync), new { id = bookingId },
            new { bookingId, status = "Initiated" });
    }

    [HttpGet("{id:guid}")]
    public async Task<IActionResult> GetByIdAsync(Guid id, CancellationToken ct)
    {
        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier)
            ?? User.FindFirstValue("sub")
            ?? string.Empty;

        var dto = await db.BookingSagaStates
            .AsNoTracking()
            .Where(s => s.CorrelationId == id)
            .Select(s => new BookingDto(
                s.CorrelationId, s.CurrentState, s.BookingReference,
                s.GdsPnr, s.TicketNumber, s.TotalAmount, s.Currency,
                s.UserId, s.InitiatedAtUtc))
            .FirstOrDefaultAsync(ct);

        if (dto is null) return NotFound();

        if (dto.UserId != userId && !User.IsInRole(BackofficeRole))
            return Forbid();

        return Ok(new BookingDtoPublic(
            dto.BookingId, dto.Status, dto.BookingReference, dto.Pnr,
            dto.TicketNumber, dto.TotalAmount, dto.Currency, dto.CreatedAt));
    }

    /// <summary>
    /// Plan 04-01 / CONTEXT D-17 — convenience route for the B2C dashboard.
    /// Resolves <c>customerId</c> from the JWT so the portal can call
    /// <c>GET /customers/me/bookings</c> without having to construct the sub
    /// claim on the client. Delegates to <see cref="ListForCustomerAsync"/>.
    /// </summary>
    [HttpGet("/customers/me/bookings")]
    public Task<IActionResult> ListForMeAsync(int page = 1, int size = 20, CancellationToken ct = default)
    {
        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier)
            ?? User.FindFirstValue("sub")
            ?? string.Empty;
        if (string.IsNullOrEmpty(userId))
            return Task.FromResult<IActionResult>(Unauthorized());

        return ListForCustomerAsync(userId, page, size, ct);
    }

    [HttpGet("/customers/{customerId}/bookings")]
    public async Task<IActionResult> ListForCustomerAsync(
        string customerId, int page = 1, int size = 20, CancellationToken ct = default)
    {
        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier)
            ?? User.FindFirstValue("sub")
            ?? string.Empty;

        if (!string.Equals(userId, customerId, StringComparison.Ordinal) && !User.IsInRole(BackofficeRole))
            return Forbid();

        if (page < 1) page = 1;
        if (size is < 1 or > 100) size = 20;

        var items = await db.BookingSagaStates
            .AsNoTracking()
            .Where(s => s.UserId == customerId)
            .OrderByDescending(s => s.InitiatedAtUtc)
            .Skip((page - 1) * size)
            .Take(size)
            .Select(s => new BookingDtoPublic(
                s.CorrelationId, s.CurrentState, s.BookingReference,
                s.GdsPnr, s.TicketNumber, s.TotalAmount, s.Currency, s.InitiatedAtUtc))
            .ToListAsync(ct);

        return Ok(new { page, size, items });
    }
}

/// <summary>Incoming body for POST /bookings. Contains only non-PII fields — passenger PII
/// enters the system in Phase 4 (D-20), and is never persisted to saga state.</summary>
public record CreateBookingRequest(
    string ProductType,
    string Channel,
    decimal TotalAmount,
    string Currency,
    string PaymentMethod,
    Guid? WalletId);

/// <summary>Internal projection used for authorization check (includes UserId).</summary>
internal record BookingDto(
    Guid BookingId,
    int Status,
    string BookingReference,
    string? Pnr,
    string? TicketNumber,
    decimal TotalAmount,
    string Currency,
    string UserId,
    DateTime CreatedAt);

/// <summary>Public response DTO. Intentionally omits UserId and all PII — COMP-01/02 + D-20.</summary>
public record BookingDtoPublic(
    Guid BookingId,
    int Status,
    string BookingReference,
    string? Pnr,
    string? TicketNumber,
    decimal TotalAmount,
    string Currency,
    DateTime CreatedAt);
