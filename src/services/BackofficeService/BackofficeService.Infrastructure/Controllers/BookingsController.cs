using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using TBE.BackofficeService.Application.Entities;
using TBE.BackofficeService.Infrastructure;
using TBE.BookingService.Infrastructure;

// Namespace kept as Application.Controllers per DlqController /
// StaffBookingActionsController / WalletCreditRequestsController
// precedent. Physical project is Infrastructure because the controller
// depends on BackofficeDbContext (and optionally BookingEventsDbContext,
// provided via DI).
namespace TBE.BackofficeService.Application.Controllers;

/// <summary>
/// Plan 06-01 Task 7 (BO-01) — unified cross-channel booking list for
/// backoffice ops staff. GET returns a paged view of
/// <c>Saga.BookingSagaState</c> spanning Channel ∈ {B2C=0, B2B=1,
/// Manual=2 (Plan 06-02 reserves)}. All four ops-* roles can read;
/// there is intentionally NO agency_id filter (T-6-05 accept).
///
/// <para>
/// Detail endpoint includes the audit timeline from <c>dbo.BookingEvents</c>
/// (read via an optional injected <see cref="BookingEventsDbContext"/> —
/// tests that don't need the timeline pass null and get an empty list)
/// plus any <c>backoffice.CancellationRequests</c> for the same booking.
/// </para>
///
/// <para>
/// Pitfall 4 inherited from Program.cs — every policy pins the
/// "Backoffice" scheme so B2B / B2C tokens cannot satisfy the gate.
/// Pitfall 10 (cross-tenant 404) does not apply: this IS the cross-
/// tenant endpoint by design.
/// </para>
/// </summary>
[ApiController]
[Route("api/backoffice/bookings")]
[Authorize(Policy = "BackofficeReadPolicy")]
public sealed class BookingsController : ControllerBase
{
    private readonly BackofficeDbContext _db;
    private readonly BookingEventsDbContext? _bookingEvents;
    private readonly ILogger<BookingsController> _logger;

    public BookingsController(
        BackofficeDbContext db,
        ILogger<BookingsController> logger)
    {
        _db = db;
        _bookingEvents = null;
        _logger = logger;
    }

    public BookingsController(
        BackofficeDbContext db,
        BookingEventsDbContext bookingEvents,
        ILogger<BookingsController> logger)
    {
        _db = db;
        _bookingEvents = bookingEvents;
        _logger = logger;
    }

    public sealed class ListQuery
    {
        /// <summary>"B2C" | "B2B" | "Manual" | null (all channels).</summary>
        public string? Channel { get; set; }

        /// <summary>Free-text match against PNR / CustomerName / CustomerEmail / BookingReference.</summary>
        public string? Q { get; set; }

        /// <summary>Inclusive lower bound on <c>InitiatedAtUtc</c>.</summary>
        public DateTime? From { get; set; }

        /// <summary>Inclusive upper bound on <c>InitiatedAtUtc</c>.</summary>
        public DateTime? To { get; set; }

        public int Page { get; set; } = 1;
        public int PageSize { get; set; } = 20;
    }

    public sealed record BookingListRow(
        Guid BookingId,
        string Channel,
        int CurrentState,
        string? Pnr,
        string? TicketNumber,
        string? CustomerName,
        string? CustomerEmail,
        Guid? AgencyId,
        decimal GrossAmount,
        string Currency,
        string BookingReference,
        DateTime CreatedAt);

    public sealed record BookingListResponse(
        IReadOnlyList<BookingListRow> Rows,
        int TotalCount,
        int Page,
        int PageSize);

    public sealed record BookingEventDto(
        Guid EventId,
        Guid BookingId,
        string EventType,
        DateTime OccurredAt,
        string Actor,
        Guid CorrelationId);

    public sealed record CancellationRequestDto(
        Guid Id,
        Guid BookingId,
        string ReasonCode,
        string Reason,
        string RequestedBy,
        DateTime RequestedAt,
        DateTime ExpiresAt,
        string Status,
        string? ApprovedBy,
        DateTime? ApprovedAt);

    public sealed record BookingDetailResponse(
        Guid BookingId,
        string Channel,
        int CurrentState,
        string? Pnr,
        string? TicketNumber,
        string? CustomerName,
        string? CustomerEmail,
        Guid? AgencyId,
        decimal GrossAmount,
        string Currency,
        string BookingReference,
        DateTime CreatedAt,
        IReadOnlyList<BookingEventDto> BookingEvents,
        IReadOnlyList<CancellationRequestDto> CancellationRequests);

    [HttpGet]
    public async Task<IActionResult> List(
        [FromQuery] ListQuery query,
        CancellationToken ct)
    {
        var page = Math.Max(1, query.Page);
        var pageSize = Math.Clamp(query.PageSize, 1, 100);

        IQueryable<BookingReadRow> q = _db.BookingReadModel;

        if (!string.IsNullOrWhiteSpace(query.Channel))
        {
            var parsed = ParseChannel(query.Channel);
            if (parsed.HasValue)
                q = q.Where(r => r.ChannelKind == parsed.Value);
            else
                return Ok(new BookingListResponse(Array.Empty<BookingListRow>(), 0, page, pageSize));
        }

        if (query.From is { } from)
            q = q.Where(r => r.InitiatedAtUtc >= from);
        if (query.To is { } to)
            q = q.Where(r => r.InitiatedAtUtc <= to);

        if (!string.IsNullOrWhiteSpace(query.Q))
        {
            var term = query.Q.Trim();
            q = q.Where(r =>
                (r.GdsPnr != null && r.GdsPnr.Contains(term)) ||
                (r.CustomerName != null && r.CustomerName.Contains(term)) ||
                (r.CustomerEmail != null && r.CustomerEmail.Contains(term)) ||
                r.BookingReference.Contains(term));
        }

        var total = await q.CountAsync(ct);

        var rows = await q
            .OrderByDescending(r => r.InitiatedAtUtc)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(r => new BookingListRow(
                r.CorrelationId,
                ChannelLabel(r.ChannelKind),
                r.CurrentState,
                r.GdsPnr,
                r.TicketNumber,
                r.CustomerName,
                r.CustomerEmail,
                r.AgencyId,
                r.TotalAmount,
                r.Currency,
                r.BookingReference,
                r.InitiatedAtUtc))
            .ToListAsync(ct);

        return Ok(new BookingListResponse(rows, total, page, pageSize));
    }

    [HttpGet("{bookingId:guid}")]
    public async Task<IActionResult> Detail(Guid bookingId, CancellationToken ct)
    {
        var row = await _db.BookingReadModel
            .SingleOrDefaultAsync(r => r.CorrelationId == bookingId, ct);
        if (row is null) return NotFound();

        var events = Array.Empty<BookingEventDto>();
        if (_bookingEvents is not null)
        {
            events = (await _bookingEvents.Events
                .Where(e => e.BookingId == bookingId)
                .OrderBy(e => e.OccurredAt)
                .Select(e => new BookingEventDto(
                    e.EventId,
                    e.BookingId,
                    e.EventType,
                    e.OccurredAt,
                    e.Actor,
                    e.CorrelationId))
                .ToListAsync(ct))
                .ToArray();
        }

        var cancellations = await _db.CancellationRequests
            .Where(c => c.BookingId == bookingId)
            .OrderByDescending(c => c.RequestedAt)
            .Select(c => new CancellationRequestDto(
                c.Id,
                c.BookingId,
                c.ReasonCode,
                c.Reason,
                c.RequestedBy,
                c.RequestedAt,
                c.ExpiresAt,
                c.Status,
                c.ApprovedBy,
                c.ApprovedAt))
            .ToListAsync(ct);

        return Ok(new BookingDetailResponse(
            row.CorrelationId,
            ChannelLabel(row.ChannelKind),
            row.CurrentState,
            row.GdsPnr,
            row.TicketNumber,
            row.CustomerName,
            row.CustomerEmail,
            row.AgencyId,
            row.TotalAmount,
            row.Currency,
            row.BookingReference,
            row.InitiatedAtUtc,
            events,
            cancellations));
    }

    private static int? ParseChannel(string s) => s.Trim().ToUpperInvariant() switch
    {
        "B2C" => 0,
        "B2B" => 1,
        "MANUAL" => 2,
        _ => null,
    };

    private static string ChannelLabel(int kind) => kind switch
    {
        0 => "B2C",
        1 => "B2B",
        2 => "Manual",
        _ => $"Unknown({kind})",
    };
}
