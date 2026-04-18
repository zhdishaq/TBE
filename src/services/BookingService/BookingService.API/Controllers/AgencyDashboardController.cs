using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using TBE.BookingService.Infrastructure;

namespace TBE.BookingService.API.Controllers;

/// <summary>
/// Plan 05-04 Task 1 — Agency dashboard summary endpoint.
///
/// <para>
/// Returns a single DTO for the caller's agency covering TTL alert counts
/// (urgent/warn), pending-booking count, and the top-5 recent bookings.
/// One RSC-friendly request replaces the separate lookups the portal would
/// otherwise have to chain.
/// </para>
///
/// <para>
/// Wallet balance and threshold are intentionally NOT queried here — those
/// live in PaymentService behind <c>/api/wallet/me</c> (Plan 05-01) and the
/// portal composes the two responses client-side to keep the per-service
/// query surface narrow. This controller returns zeroed placeholders for
/// those fields so the DTO shape remains stable across calls.
/// </para>
///
/// <para>
/// <b>D-34 OVERRIDE</b> — All agent roles see AGENCY-WIDE counts. Filter by
/// <c>agency_id</c> claim ONLY; never additionally by <c>sub</c>. See
/// <c>.planning/phases/05-b2b-agent-portal/05-CONTEXT.md</c> D-34.
/// </para>
///
/// <para>
/// <b>Pitfall 28</b> — missing <c>agency_id</c> claim is a fail-closed 401
/// not a "default to all agencies" smell.
/// </para>
/// </summary>
[ApiController]
[Route("api/dashboard")]
[Authorize(Policy = "B2BPolicy")]
public sealed class AgencyDashboardController(
    BookingDbContext db,
    ILogger<AgencyDashboardController> logger) : ControllerBase
{
    [HttpGet("summary")]
    public async Task<IActionResult> GetSummaryAsync(CancellationToken ct)
    {
        // Pitfall 28 — fail-closed if the JWT did not supply agency_id.
        var agencyIdClaim = User.FindFirst("agency_id")?.Value;
        if (string.IsNullOrWhiteSpace(agencyIdClaim) || !Guid.TryParse(agencyIdClaim, out var agencyId))
            return Unauthorized(new { error = "missing agency_id claim" });

        var now = DateTime.UtcNow;
        var twoHoursOut = now.AddHours(2);
        var twentyFourHoursOut = now.AddHours(24);

        // D-34 — filter by AgencyId ONLY. Do NOT append `s.UserId == sub`.
        var agencyBookings = db.BookingSagaStates
            .AsNoTracking()
            .Where(s => s.AgencyId == agencyId);

        // Urgent: deadline within 2h from now.
        var urgentTtlCount = await agencyBookings
            .CountAsync(s => s.TicketingDeadlineUtc > now && s.TicketingDeadlineUtc <= twoHoursOut, ct);

        // Warn: deadline within 24h but still > 2h out (matches TTL monitor window).
        var warning24hTtlCount = await agencyBookings
            .CountAsync(s => s.TicketingDeadlineUtc > twoHoursOut && s.TicketingDeadlineUtc <= twentyFourHoursOut, ct);

        // Pending = not yet ticketed (no TicketNumber).
        var pendingBookingCount = await agencyBookings
            .CountAsync(s => s.TicketNumber == null, ct);

        // Top-5 recent bookings by InitiatedAtUtc desc — plan-mandated cap.
        var recent = await agencyBookings
            .OrderByDescending(s => s.InitiatedAtUtc)
            .Take(5)
            .Select(s => new AgencyDashboardRecentBooking(
                s.CorrelationId,
                s.BookingReference,
                s.GdsPnr,
                s.TicketNumber,
                s.AgencyGrossAmount ?? s.TotalAmount,
                s.Currency,
                s.CustomerName,
                s.TicketingDeadlineUtc,
                s.InitiatedAtUtc))
            .ToListAsync(ct);

        logger.LogInformation(
            "DASHBOARD-SUMMARY agency={AgencyId} urgent={Urgent} warn24={Warn24} pending={Pending}",
            agencyId, urgentTtlCount, warning24hTtlCount, pendingBookingCount);

        return Ok(new AgencyDashboardSummaryDto(
            WalletBalance: 0m,
            WalletThreshold: 0m,
            Currency: recent.FirstOrDefault()?.Currency ?? string.Empty,
            PendingBookingCount: pendingBookingCount,
            UrgentTtlCount: urgentTtlCount,
            Warning24hTtlCount: warning24hTtlCount,
            RecentBookings: recent));
    }
}

/// <summary>
/// Plan 05-04 Task 1 — agency dashboard summary DTO. Wallet fields are
/// placeholders that the portal overlays from PaymentService's
/// <c>/api/wallet/me</c> response client-side; see controller remarks.
/// </summary>
public sealed record AgencyDashboardSummaryDto(
    decimal WalletBalance,
    decimal WalletThreshold,
    string Currency,
    int PendingBookingCount,
    int UrgentTtlCount,
    int Warning24hTtlCount,
    IReadOnlyList<AgencyDashboardRecentBooking> RecentBookings);

/// <summary>Projection for the top-5 recent-bookings rail on the dashboard.</summary>
public sealed record AgencyDashboardRecentBooking(
    Guid BookingId,
    string BookingReference,
    string? Pnr,
    string? TicketNumber,
    decimal GrossAmount,
    string Currency,
    string? CustomerName,
    DateTime TicketingDeadlineUtc,
    DateTime InitiatedAtUtc);
