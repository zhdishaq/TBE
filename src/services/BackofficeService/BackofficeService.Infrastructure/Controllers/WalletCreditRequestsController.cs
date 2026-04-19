using System.ComponentModel.DataAnnotations;
using MassTransit;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using TBE.BackofficeService.Application.Entities;
using TBE.BackofficeService.Infrastructure;
using TBE.Contracts.Events;

// Namespace kept as Application.Controllers per 06-01-PLAN Task 6 manifest
// (DlqController + StaffBookingActionsController precedent). Physical
// project is Infrastructure because the controller depends on
// BackofficeDbContext + IPublishEndpoint.
namespace TBE.BackofficeService.Application.Controllers;

/// <summary>
/// Plan 06-01 Task 6 — D-39 manual wallet credit with 4-eyes approval.
/// ops-finance opens a request; a different ops-admin approves. On
/// approve the controller flips Status→Approved and publishes
/// <see cref="WalletCreditApproved"/> via the EF outbox — this is the
/// signal the PaymentService <c>WalletCreditApprovedConsumer</c> waits
/// for before writing a <c>payment.WalletTransactions</c> row of
/// Kind=ManualCredit.
///
/// Amount is validated against [0.01, 100000] at the controller layer
/// with a 400 problem+json (/errors/wallet-credit-invalid-amount) and
/// re-enforced at the DB layer via a CHECK constraint on the row's
/// <see cref="WalletCreditRequest.Amount"/> column. D-53 ReasonCode is
/// similarly double-guarded.
///
/// RFC-7807 problem+json type URIs per PATTERNS.md Pattern G:
///   - /errors/four-eyes-self-approval          (403)
///   - /errors/four-eyes-expired                (409, flips Status→Expired)
///   - /errors/four-eyes-already-decided        (409)
///   - /errors/wallet-credit-invalid-amount     (400)
///   - /errors/wallet-credit-invalid-reason     (400)
/// </summary>
[ApiController]
[Route("api/backoffice/wallet-credits")]
[Authorize(Policy = "BackofficeReadPolicy")]
public sealed class WalletCreditRequestsController : ControllerBase
{
    private static readonly HashSet<string> ValidReasonCodes = new(StringComparer.Ordinal)
    {
        "RefundedBooking",
        "GoodwillCredit",
        "DisputeResolution",
        "SupplierRefundPassthrough",
    };

    private const decimal AmountMin = 0.01m;
    private const decimal AmountMax = 100_000m;

    private readonly BackofficeDbContext _db;
    private readonly IPublishEndpoint _publish;
    private readonly ILogger<WalletCreditRequestsController> _logger;

    public WalletCreditRequestsController(
        BackofficeDbContext db,
        IPublishEndpoint publish,
        ILogger<WalletCreditRequestsController> logger)
    {
        _db = db;
        _publish = publish;
        _logger = logger;
    }

    public sealed class CreateCreditReq
    {
        public Guid AgencyId { get; set; }
        public decimal Amount { get; set; }

        [Required]
        [StringLength(3, MinimumLength = 3)]
        public string Currency { get; set; } = "GBP";

        [Required]
        [StringLength(64, MinimumLength = 1)]
        public string ReasonCode { get; set; } = string.Empty;

        public Guid? LinkedBookingId { get; set; }

        [Required]
        [StringLength(1000, MinimumLength = 1)]
        public string Notes { get; set; } = string.Empty;
    }

    public sealed class ApproveCreditReq
    {
        [Required]
        [StringLength(500, MinimumLength = 1)]
        public string ApprovalNotes { get; set; } = string.Empty;
    }

    /// <summary>
    /// ops-finance (or ops-admin) opens a manual wallet credit request.
    /// Row lands in PendingApproval with a 72h ExpiresAt.
    /// </summary>
    [HttpPost("")]
    [Authorize(Policy = "BackofficeFinancePolicy")]
    public async Task<IActionResult> Open(
        [FromBody] CreateCreditReq body,
        CancellationToken ct)
    {
        var actor = User.FindFirst("preferred_username")?.Value;
        if (string.IsNullOrEmpty(actor))
            return Problem(
                statusCode: StatusCodes.Status401Unauthorized,
                type: "/errors/missing-actor",
                title: "missing_actor",
                detail: "missing preferred_username claim");

        if (body is null)
            return Problem(
                statusCode: StatusCodes.Status400BadRequest,
                type: "/errors/wallet-credit-invalid-amount",
                title: "wallet_credit_invalid_amount",
                detail: "request body required");

        // Amount bounds — 400 before any DB round-trip. This must be
        // checked BEFORE reason code so the 0-amount test (which uses a
        // valid reason) trips here rather than falling through.
        if (body.Amount < AmountMin || body.Amount > AmountMax)
        {
            return Problem(
                statusCode: StatusCodes.Status400BadRequest,
                type: "/errors/wallet-credit-invalid-amount",
                title: "wallet_credit_invalid_amount",
                detail: $"Amount must be in [{AmountMin}, {AmountMax}] (D-39)");
        }

        // Reason-code enum — 400.
        if (string.IsNullOrWhiteSpace(body.ReasonCode) || !ValidReasonCodes.Contains(body.ReasonCode))
        {
            return Problem(
                statusCode: StatusCodes.Status400BadRequest,
                type: "/errors/wallet-credit-invalid-reason",
                title: "wallet_credit_invalid_reason",
                detail: $"ReasonCode must be one of: {string.Join(", ", ValidReasonCodes)} (D-53)");
        }

        if (!ModelState.IsValid)
            return ValidationProblem(ModelState);

        var now = DateTime.UtcNow;
        var row = new WalletCreditRequest
        {
            Id = Guid.NewGuid(),
            AgencyId = body.AgencyId,
            Amount = body.Amount,
            Currency = body.Currency,
            ReasonCode = body.ReasonCode,
            LinkedBookingId = body.LinkedBookingId,
            Notes = body.Notes,
            RequestedBy = actor,
            RequestedAt = now,
            ExpiresAt = now.AddHours(72),
            Status = "PendingApproval",
        };
        _db.WalletCreditRequests.Add(row);
        await _db.SaveChangesAsync(ct);

        // Observability event — Phase 7 may subscribe for a pending-approvals
        // dashboard. PaymentService currently ignores this event (it only
        // reacts to WalletCreditApproved).
        await _publish.Publish(new WalletCreditRequested(
            row.Id,
            row.AgencyId,
            row.Amount,
            row.Currency,
            row.ReasonCode,
            row.LinkedBookingId,
            row.Notes,
            actor,
            now,
            row.ExpiresAt), ct);

        _logger.LogInformation(
            "wallet-credit-open {RequestId} agency={AgencyId} amount={Amount} {Currency} by={Actor}",
            row.Id, row.AgencyId, row.Amount, row.Currency, actor);

        return Accepted(new { Id = row.Id });
    }

    /// <summary>
    /// ops-admin approves the credit. 4-eyes gate: Approver MUST NOT equal
    /// Requester. Publishes <see cref="WalletCreditApproved"/> atomically
    /// with the row flip via EF outbox.
    /// </summary>
    [HttpPost("{requestId:guid}/approve")]
    [Authorize(Policy = "BackofficeAdminPolicy")]
    public async Task<IActionResult> Approve(
        Guid requestId,
        [FromBody] ApproveCreditReq body,
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

        var row = await _db.WalletCreditRequests.SingleOrDefaultAsync(r => r.Id == requestId, ct);
        if (row is null) return NotFound();

        if (!string.Equals(row.Status, "PendingApproval", StringComparison.Ordinal))
        {
            return Problem(
                statusCode: StatusCodes.Status409Conflict,
                type: "/errors/four-eyes-already-decided",
                title: "four_eyes_already_decided",
                detail: $"request is already {row.Status}");
        }

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
        row.ApprovalNotes = body.ApprovalNotes;

        await _publish.Publish(new WalletCreditApproved(
            row.Id,
            row.AgencyId,
            row.Amount,
            row.Currency,
            row.ReasonCode,
            row.LinkedBookingId,
            row.RequestedBy,
            actor,
            body.ApprovalNotes,
            approvedAt), ct);

        await _db.SaveChangesAsync(ct);

        _logger.LogInformation(
            "wallet-credit-approve {RequestId} agency={AgencyId} amount={Amount} by={Actor} requestedBy={RequestedBy}",
            row.Id, row.AgencyId, row.Amount, actor, row.RequestedBy);

        return NoContent();
    }
}
