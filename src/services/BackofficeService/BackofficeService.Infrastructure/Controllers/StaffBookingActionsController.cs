using System.ComponentModel.DataAnnotations;
using MassTransit;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using TBE.BackofficeService.Application.Entities;
using TBE.BackofficeService.Infrastructure;
using TBE.BookingService.Application;
using TBE.Contracts.Events;

// Namespace kept as Application.Controllers per 06-01-PLAN Task 4 manifest
// (DlqController precedent). Physical project is Infrastructure because
// the controller depends on BackofficeDbContext + IPublishEndpoint +
// IBookingEventsWriter. The API project auto-discovers it via
// ApplicationPart scan of referenced assemblies.
namespace TBE.BackofficeService.Application.Controllers;

/// <summary>
/// Plan 06-01 Task 6 — BO-03 staff-initiated booking cancellation with
/// 4-eyes approval (D-48). ops-cs opens a request; a different ops-admin
/// approves. On approve:
///   1. CancellationRequest.Status flips to "Approved".
///   2. BookingEvents audit row written via IBookingEventsWriter.
///   3. BookingCancellationApproved published via IPublishEndpoint
///      (EF outbox ensures the publish+row-flip are committed together —
///      Plan 03-01 pattern).
///
/// All failure paths return RFC-7807 problem+json with enumerated type
/// URIs per PATTERNS.md Pattern G:
///   - /errors/four-eyes-self-approval       (403)
///   - /errors/four-eyes-expired             (409, also flips Status→Expired)
///   - /errors/four-eyes-already-decided     (409)
///   - /errors/cancellation-invalid-reason   (400)
///
/// Pitfall 4 (scheme pin) is already applied in Program.cs via
/// AddAuthenticationSchemes("Backoffice") on every policy, so the
/// [Authorize(Policy=...)] attributes below automatically inherit it.
///
/// Pitfall 28 (fail-closed actor extraction): every mutation reads the
/// <c>preferred_username</c> claim and returns 401 problem+json when it
/// is missing. Never fall back to "system".
/// </summary>
[ApiController]
[Route("api/backoffice/bookings")]
[Authorize(Policy = "BackofficeReadPolicy")]
public sealed class StaffBookingActionsController : ControllerBase
{
    private static readonly HashSet<string> ValidReasonCodes = new(StringComparer.Ordinal)
    {
        "CustomerRequest",
        "SupplierInitiated",
        "FareRuleViolation",
        "FraudSuspected",
        "DuplicateBooking",
        "Other",
    };

    private readonly BackofficeDbContext _db;
    private readonly IPublishEndpoint _publish;
    private readonly IBookingEventsWriter _bookingEventsWriter;
    private readonly ILogger<StaffBookingActionsController> _logger;

    public StaffBookingActionsController(
        BackofficeDbContext db,
        IPublishEndpoint publish,
        IBookingEventsWriter bookingEventsWriter,
        ILogger<StaffBookingActionsController> logger)
    {
        _db = db;
        _publish = publish;
        _bookingEventsWriter = bookingEventsWriter;
        _logger = logger;
    }

    public sealed class CancelBookingReq
    {
        [Required]
        [StringLength(64, MinimumLength = 1)]
        public string ReasonCode { get; set; } = string.Empty;

        [Required]
        [StringLength(500, MinimumLength = 1)]
        public string Reason { get; set; } = string.Empty;
    }

    public sealed class ApproveCancelReq
    {
        [Required]
        [StringLength(500, MinimumLength = 1)]
        public string ApprovalReason { get; set; } = string.Empty;
    }

    /// <summary>
    /// ops-cs (or ops-admin) opens a staff-initiated cancellation. Row
    /// lands in PendingApproval with a 72h ExpiresAt. Awaits a different
    /// ops-admin to call /approve or /deny.
    /// </summary>
    [HttpPost("{bookingId:guid}/cancel")]
    [Authorize(Policy = "BackofficeCsPolicy")]
    public async Task<IActionResult> Open(
        Guid bookingId,
        [FromBody] CancelBookingReq body,
        CancellationToken ct)
    {
        var actor = User.FindFirst("preferred_username")?.Value;
        if (string.IsNullOrEmpty(actor))
            return Problem(
                statusCode: StatusCodes.Status401Unauthorized,
                type: "/errors/missing-actor",
                title: "missing_actor",
                detail: "missing preferred_username claim");

        if (body is null || string.IsNullOrWhiteSpace(body.ReasonCode) || !ValidReasonCodes.Contains(body.ReasonCode))
        {
            return Problem(
                statusCode: StatusCodes.Status400BadRequest,
                type: "/errors/cancellation-invalid-reason",
                title: "cancellation_invalid_reason",
                detail: $"ReasonCode must be one of: {string.Join(", ", ValidReasonCodes)}");
        }

        if (!ModelState.IsValid)
            return ValidationProblem(ModelState);

        var now = DateTime.UtcNow;
        var row = new CancellationRequest
        {
            Id = Guid.NewGuid(),
            BookingId = bookingId,
            ReasonCode = body.ReasonCode,
            Reason = body.Reason,
            RequestedBy = actor,
            RequestedAt = now,
            ExpiresAt = now.AddHours(72),
            Status = "PendingApproval",
        };
        _db.CancellationRequests.Add(row);
        await _db.SaveChangesAsync(ct);

        _logger.LogInformation(
            "staff-cancel-open {RequestId} booking={BookingId} by={Actor} reason={ReasonCode}",
            row.Id, bookingId, actor, body.ReasonCode);

        return Accepted(new { Id = row.Id });
    }

    /// <summary>
    /// ops-admin approves the cancellation. 4-eyes gate: Approver MUST NOT
    /// equal Requester. Approval is atomic via EF outbox — the publish and
    /// the row-flip (+BookingEvents append) commit together.
    /// </summary>
    [HttpPost("cancellations/{requestId:guid}/approve")]
    [Authorize(Policy = "BackofficeAdminPolicy")]
    public async Task<IActionResult> Approve(
        Guid requestId,
        [FromBody] ApproveCancelReq body,
        CancellationToken ct)
    {
        var actor = User.FindFirst("preferred_username")?.Value;
        if (string.IsNullOrEmpty(actor))
            return Problem(
                statusCode: StatusCodes.Status401Unauthorized,
                type: "/errors/missing-actor",
                title: "missing_actor",
                detail: "missing preferred_username claim");

        if (!ModelState.IsValid)
            return ValidationProblem(ModelState);

        var row = await _db.CancellationRequests.SingleOrDefaultAsync(r => r.Id == requestId, ct);
        if (row is null) return NotFound();

        // Already decided → 409.
        if (!string.Equals(row.Status, "PendingApproval", StringComparison.Ordinal))
        {
            return Problem(
                statusCode: StatusCodes.Status409Conflict,
                type: "/errors/four-eyes-already-decided",
                title: "four_eyes_already_decided",
                detail: $"request is already {row.Status}");
        }

        // Expiry check — flip to Expired as a side-effect so it drops out of Pending.
        if (row.ExpiresAt <= DateTime.UtcNow)
        {
            row.Status = "Expired";
            await _db.SaveChangesAsync(ct);
            return Problem(
                statusCode: StatusCodes.Status409Conflict,
                type: "/errors/four-eyes-expired",
                title: "four_eyes_expired",
                detail: "request expired before approval");
        }

        // Self-approval guard — approver MUST NOT equal requester.
        if (string.Equals(row.RequestedBy, actor, StringComparison.Ordinal))
        {
            return Problem(
                statusCode: StatusCodes.Status403Forbidden,
                type: "/errors/four-eyes-self-approval",
                title: "four_eyes_self_approval",
                detail: "approver must differ from requester");
        }

        var approvedAt = DateTime.UtcNow;
        row.Status = "Approved";
        row.ApprovedBy = actor;
        row.ApprovedAt = approvedAt;
        row.ApprovalReason = body.ApprovalReason;

        var correlationId = Guid.NewGuid();

        // BookingEvents audit row — append-only per D-49 / BO-04.
        await _bookingEventsWriter.WriteAsync(
            row.BookingId,
            "BookingCancellationApproved",
            actor,
            correlationId,
            new
            {
                requestId = row.Id,
                row.ReasonCode,
                row.Reason,
                row.RequestedBy,
                approvedBy = actor,
                approvalReason = body.ApprovalReason,
                approvedAt,
            },
            ct);

        // Publish via EF outbox — atomic with SaveChangesAsync below.
        await _publish.Publish(new BookingCancellationApproved(
            row.BookingId,
            row.ReasonCode,
            row.Reason,
            row.RequestedBy,
            actor,
            body.ApprovalReason,
            approvedAt), ct);

        await _db.SaveChangesAsync(ct);

        _logger.LogInformation(
            "staff-cancel-approve {RequestId} booking={BookingId} by={Actor} requestedBy={RequestedBy}",
            row.Id, row.BookingId, actor, row.RequestedBy);

        return NoContent();
    }
}
