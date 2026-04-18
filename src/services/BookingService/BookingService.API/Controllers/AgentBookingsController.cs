using System.Security.Claims;
using MassTransit;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using TBE.BookingService.Application.Saga;
using TBE.BookingService.Infrastructure;
using TBE.Contracts.Enums;
using TBE.Contracts.Events;

namespace TBE.BookingService.API.Controllers;

/// <summary>
/// Plan 05-02 Task 2 — B2B booking endpoints (create-on-behalf + agency-wide
/// list).
///
/// D-34 OVERRIDE — All agent roles (agent, agent-admin, agent-readonly) see
/// AGENCY-WIDE bookings. Filter by <c>agency_id</c> claim ONLY; never
/// additionally by <c>sub</c>. See
/// <c>.planning/phases/05-b2b-agent-portal/05-CONTEXT.md</c> D-34 (overrides
/// ROADMAP Phase 5 UAT wording).
/// </summary>
/// <remarks>
/// <para>
/// <b>T-05-02-01 / T-05-02-08 (cross-tenant tampering):</b> the
/// <see cref="CreateAgentBookingRequest"/> DTO deliberately omits
/// <c>AgencyId</c> and <c>Channel</c> so they cannot be forged in the
/// request body. The controller stamps both from the JWT
/// <c>agency_id</c> claim and a literal <see cref="Channel.B2B"/>.
/// </para>
/// <para>
/// <b>T-05-02-02 (markup-override elevation):</b> if
/// <see cref="CreateAgentBookingRequest.AgencyMarkupOverride"/> is present
/// and the caller is NOT <c>agent-admin</c>, the request is rejected
/// with 403 + a structured warn log — never silently dropped.
/// </para>
/// <para>
/// <b>D-35 readonly-write gate:</b> agent-readonly role cannot create
/// bookings. Returns 403.
/// </para>
/// </remarks>
[ApiController]
[Route("agent/bookings")]
[Authorize(Policy = "B2BPolicy")]
public sealed class AgentBookingsController(
    BookingDbContext db,
    IPublishEndpoint publishEndpoint,
    ILogger<AgentBookingsController> logger) : ControllerBase
{
    [HttpPost]
    public async Task<IActionResult> CreateAsync(
        [FromBody] CreateAgentBookingRequest req,
        CancellationToken ct)
    {
        if (req is null)
            return BadRequest(new { error = "request body required" });

        // T-05-02-01 — agency_id NEVER from body. Read from JWT claim.
        var agencyIdClaim = User.FindFirst("agency_id")?.Value;
        if (string.IsNullOrWhiteSpace(agencyIdClaim) || !Guid.TryParse(agencyIdClaim, out var agencyId))
            return Unauthorized(new { error = "missing agency_id claim" });

        var sub = User.FindFirstValue(ClaimTypes.NameIdentifier)
            ?? User.FindFirstValue("sub")
            ?? string.Empty;

        // D-35 — agent-readonly cannot create bookings.
        if (HasRole("agent-readonly") && !HasRole("agent") && !HasRole("agent-admin"))
        {
            logger.LogWarning(
                "agent-readonly-write-forbidden agency={AgencyId} user={Sub}",
                agencyId, sub);
            return Forbid();
        }

        // T-05-02-02 — AgencyMarkupOverride admin-only.
        if (req.AgencyMarkupOverride is not null && !HasRole("agent-admin"))
        {
            logger.LogWarning(
                "markup-override-forbidden agency={AgencyId} user={Sub}",
                agencyId, sub);
            return Forbid();
        }

        var bookingId = Guid.NewGuid();
        var bookingReference = $"TBE-{DateTime.UtcNow:yyMMdd}-{bookingId.ToString("N")[..8].ToUpperInvariant()}";

        // T-05-02-07 — audit log at Info level (structured fields for ops reconciliation).
        logger.LogInformation(
            "BOOK-CREATE booking={BookingId} agency={AgencyId} agent={Sub} customer_email={Email} net={Net} gross={Gross}",
            bookingId, agencyId, sub, req.CustomerEmail, req.AgencyNetFare, req.AgencyGrossAmount);

        // Kick off the saga. We reuse BookingInitiated (Channel="b2b" string
        // matching the Phase-3 contract); the saga parses the string into the
        // typed Channel.B2B on Initially and stamps it on the saga state.
        //
        // The AgencyMarkupOverride + customer-contact + agency pricing frozen
        // amounts flow onto the saga state through a dedicated event (below)
        // published immediately after the BookingInitiated so the EF-outbox
        // keeps a deterministic ordering.
        await publishEndpoint.Publish(new BookingInitiated(
            BookingId: bookingId,
            ProductType: req.ProductType,
            Channel: "b2b",              // server-side stamp (T-05-02-01)
            UserId: sub,
            BookingReference: bookingReference,
            TotalAmount: req.AgencyGrossAmount,
            Currency: req.Currency,
            PaymentMethod: "wallet",     // B2B always debits the agency wallet
            WalletId: req.WalletId,
            InitiatedAt: DateTimeOffset.UtcNow), ct);

        await publishEndpoint.Publish(new AgentBookingDetailsCaptured(
            BookingId: bookingId,
            AgencyId: agencyId,
            AgencyNetFare: req.AgencyNetFare,
            AgencyMarkupAmount: req.AgencyMarkupAmount,
            AgencyGrossAmount: req.AgencyGrossAmount,
            AgencyCommissionAmount: req.AgencyCommissionAmount,
            AgencyMarkupOverride: req.AgencyMarkupOverride,
            CustomerName: req.CustomerName,
            CustomerEmail: req.CustomerEmail,
            CustomerPhone: req.CustomerPhone,
            OfferId: req.OfferId,
            At: DateTimeOffset.UtcNow), ct);

        return AcceptedAtAction(
            nameof(GetByIdAsync),
            new { id = bookingId },
            new { bookingId, status = "Initiated" });
    }

    /// <summary>
    /// D-34 — agency-wide booking list for every agent role. Filter by
    /// <c>agency_id</c> claim ONLY; never additionally by <c>sub</c>.
    ///
    /// <para>
    /// Plan 05-04 Task 1 adds server-side client-name contains filter, PNR
    /// equals filter, page-size clamp [20, 100] with default 20, and
    /// deterministic <c>InitiatedAtUtc</c> desc ordering (nuqs URL-synced
    /// filters land client-side).
    /// </para>
    /// </summary>
    [HttpGet("me")]
    public async Task<IActionResult> ListForAgencyAsync(
        int page = 1,
        int size = 20,
        string? client = null,
        string? pnr = null,
        CancellationToken ct = default)
    {
        var agencyIdClaim = User.FindFirst("agency_id")?.Value;
        if (string.IsNullOrWhiteSpace(agencyIdClaim) || !Guid.TryParse(agencyIdClaim, out var agencyId))
            return Unauthorized(new { error = "missing agency_id claim" });

        if (page < 1) page = 1;
        // Plan 05-04 — clamp pager options to [20, 50, 100]. Any out-of-range
        // size collapses to the 20 floor or 100 ceiling deliberately (no 400).
        if (size < 20) size = 20;
        else if (size > 100) size = 100;

        // D-34 — filter by AgencyId ONLY. Deliberately not appending
        // `&& s.UserId == sub`.
        var query = db.BookingSagaStates
            .AsNoTracking()
            .Where(s => s.AgencyId == agencyId);

        if (!string.IsNullOrWhiteSpace(client))
        {
            // Case-insensitive contains (EF Core lowers both sides).
            var needle = client.ToLower();
            query = query.Where(s => s.CustomerName != null
                && s.CustomerName.ToLower().Contains(needle));
        }

        if (!string.IsNullOrWhiteSpace(pnr))
        {
            var pnrNorm = pnr.ToUpper();
            query = query.Where(s => s.GdsPnr != null && s.GdsPnr.ToUpper() == pnrNorm);
        }

        var items = await query
            .OrderByDescending(s => s.InitiatedAtUtc)
            .Skip((page - 1) * size)
            .Take(size)
            .Select(s => new AgentBookingListItem(
                s.CorrelationId,
                s.CurrentState,
                s.BookingReference,
                s.GdsPnr,
                s.TicketNumber,
                s.AgencyGrossAmount ?? s.TotalAmount,
                s.Currency,
                s.CustomerName,
                s.InitiatedAtUtc))
            .ToListAsync(ct);

        return Ok(new { page, size, items });
    }

    /// <summary>
    /// Plan 05-04 Task 1 (B2B-10) — admin-only pre-ticket void.
    ///
    /// Security rules (Pitfall 10 — NEVER leak booking existence):
    /// <list type="bullet">
    ///   <item>Caller not <c>agent-admin</c> → 403 (B2BAdminPolicy).</item>
    ///   <item>Booking not found OR belongs to a different agency → 404
    ///         (NEVER 403; a 403 would leak existence to cross-tenant scans).</item>
    ///   <item>Booking already ticketed (has <c>TicketNumber</c>) → 409 +
    ///         <c>application/problem+json</c> with type
    ///         <c>/errors/post-ticket-cancel-unsupported</c> (D-39).</item>
    ///   <item>Pre-ticket → 202 AcceptedAtAction; a
    ///         <see cref="TBE.Contracts.Events.VoidRequested"/> event is
    ///         published on the EF+MassTransit outbox so the saga can run
    ///         the compensation chain (release wallet, void PNR).</item>
    /// </list>
    /// </summary>
    [HttpPost("{id:guid}/void")]
    [Authorize(Policy = "B2BAdminPolicy")]
    public async Task<IActionResult> VoidAsync(Guid id, CancellationToken ct)
    {
        var agencyIdClaim = User.FindFirst("agency_id")?.Value;
        if (string.IsNullOrWhiteSpace(agencyIdClaim) || !Guid.TryParse(agencyIdClaim, out var agencyId))
            return Unauthorized(new { error = "missing agency_id claim" });

        // Defence-in-depth — mirrors the B2BAdminPolicy decision so unit tests
        // without the auth pipeline still honour D-39 / agent-admin-only, and
        // so a future middleware misconfig cannot leak void permissions to
        // non-admins.
        if (!HasRole("agent-admin"))
            return Forbid();

        var state = await db.BookingSagaStates
            .AsNoTracking()
            .Where(s => s.CorrelationId == id)
            .Select(s => new { s.AgencyId, s.CorrelationId, s.TicketNumber })
            .FirstOrDefaultAsync(ct);

        // Pitfall 10 — cross-tenant = 404. Missing = 404. Never leak existence.
        if (state is null || state.AgencyId != agencyId)
            return NotFound();

        // D-39 — post-ticket cancel is refused with 409 + problem+json.
        if (!string.IsNullOrWhiteSpace(state.TicketNumber))
        {
            var problem = new ProblemDetails
            {
                Title = "Post-ticket cancel unsupported",
                Status = StatusCodes.Status409Conflict,
                Type = "/errors/post-ticket-cancel-unsupported",
                Detail = "This booking has already been ticketed. Post-ticket cancellations require manual reconciliation (Phase 6).",
                Instance = HttpContext?.Request.Path.Value,
            };
            return new ObjectResult(problem)
            {
                StatusCode = StatusCodes.Status409Conflict,
                ContentTypes = { "application/problem+json" },
            };
        }

        var sub = User.FindFirstValue(ClaimTypes.NameIdentifier)
            ?? User.FindFirstValue("sub")
            ?? string.Empty;

        logger.LogInformation(
            "BOOK-VOID booking={BookingId} agency={AgencyId} requester={Sub}",
            state.CorrelationId, agencyId, sub);

        await publishEndpoint.Publish(new TBE.Contracts.Events.VoidRequested(
            BookingId: state.CorrelationId,
            RequestedByUserId: sub,
            Reason: "admin_requested_void",
            RequestedAt: DateTimeOffset.UtcNow), ct);

        return AcceptedAtAction(
            nameof(GetByIdAsync),
            new { id = state.CorrelationId },
            new { bookingId = state.CorrelationId, status = "VoidRequested" });
    }

    [HttpGet("{id:guid}")]
    public async Task<IActionResult> GetByIdAsync(Guid id, CancellationToken ct)
    {
        var agencyIdClaim = User.FindFirst("agency_id")?.Value;
        if (string.IsNullOrWhiteSpace(agencyIdClaim) || !Guid.TryParse(agencyIdClaim, out var agencyId))
            return Unauthorized(new { error = "missing agency_id claim" });

        var state = await db.BookingSagaStates
            .AsNoTracking()
            .Where(s => s.CorrelationId == id)
            .Select(s => new { s.AgencyId, s.CorrelationId, s.CurrentState, s.BookingReference, s.GdsPnr, s.TicketNumber, s.AgencyGrossAmount, s.TotalAmount, s.Currency, s.CustomerName, s.InitiatedAtUtc })
            .FirstOrDefaultAsync(ct);

        if (state is null) return NotFound();

        // IDOR guard — cross-tenant read must 403 even when the booking id is valid.
        if (state.AgencyId != agencyId)
            return Forbid();

        return Ok(new AgentBookingListItem(
            state.CorrelationId,
            state.CurrentState,
            state.BookingReference,
            state.GdsPnr,
            state.TicketNumber,
            state.AgencyGrossAmount ?? state.TotalAmount,
            state.Currency,
            state.CustomerName,
            state.InitiatedAtUtc));
    }

    private bool HasRole(string role)
        => User.HasClaim("roles", role)
           || User.IsInRole(role);
}

/// <summary>
/// POST /agent/bookings request body. Intentionally OMITS <c>AgencyId</c> and
/// <c>Channel</c> so they cannot be forged (T-05-02-01 / T-05-02-08) — both
/// are stamped server-side by the controller.
/// </summary>
public sealed record CreateAgentBookingRequest(
    string ProductType,
    string OfferId,
    decimal AgencyNetFare,
    decimal AgencyMarkupAmount,
    decimal AgencyGrossAmount,
    decimal AgencyCommissionAmount,
    decimal? AgencyMarkupOverride,
    string Currency,
    Guid? WalletId,
    string CustomerName,
    string CustomerEmail,
    string CustomerPhone);

/// <summary>Public projection for agency-wide booking list (no PII beyond booked customer first name).</summary>
public sealed record AgentBookingListItem(
    Guid BookingId,
    int Status,
    string BookingReference,
    string? Pnr,
    string? TicketNumber,
    decimal GrossAmount,
    string Currency,
    string? CustomerName,
    DateTime InitiatedAt);
