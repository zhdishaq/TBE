namespace TBE.Contracts.Events;

/// <summary>
/// Plan 06-01 Task 6 (BO-03 / D-39) — MassTransit contracts published by
/// BackofficeService controllers when a 4-eyes approval flow completes.
/// These land in the downstream consumer's queue via the MassTransit
/// outbox (atomic with the SaveChangesAsync that flips the approval-row
/// Status → Approved), so the publish + row-flip are committed together
/// (Plan 03-01 pattern).
/// </summary>

/// <summary>
/// D-39 — ops-finance has opened a manual wallet credit request. Captured
/// for observability + audit (Phase 7 hardening may subscribe to feed a
/// "pending approvals" dashboard). PaymentService currently ignores this
/// event and only reacts to <see cref="WalletCreditApproved"/>.
/// </summary>
public record WalletCreditRequested(
    Guid RequestId,
    Guid AgencyId,
    decimal Amount,
    string Currency,
    string ReasonCode,
    Guid? LinkedBookingId,
    string Notes,
    string RequestedBy,
    DateTime RequestedAt,
    DateTime ExpiresAt);

/// <summary>
/// D-39 — ops-admin has approved a manual wallet credit request (4-eyes
/// gate passed: Approver ≠ Requester, not expired, status was Pending).
/// Consumed by PaymentService <c>WalletCreditApprovedConsumer</c> which
/// appends a <c>payment.WalletTransactions</c> row of Kind=ManualCredit.
/// Idempotent via MassTransit InboxState dedup on MessageId (a redelivery
/// of the same event MUST NOT double-credit the wallet).
/// </summary>
public record WalletCreditApproved(
    Guid RequestId,
    Guid AgencyId,
    decimal Amount,
    string Currency,
    string ReasonCode,
    Guid? LinkedBookingId,
    string RequestedBy,
    string ApprovedBy,
    string ApprovalNotes,
    DateTime ApprovedAt);

/// <summary>
/// BO-03 — ops-cs opened a staff-initiated cancellation request (awaiting
/// approval). Published for observability; the saga does not act on this
/// event — it waits for <see cref="BookingCancellationApproved"/>.
/// </summary>
public record BookingCancelledByStaff(
    Guid BookingId,
    string ReasonCode,
    string Reason,
    string RequestedBy,
    string ApprovedBy,
    DateTime ApprovedAt);

/// <summary>
/// BO-03 — ops-admin approved a staff-initiated cancellation (4-eyes
/// gate passed). Consumed by the BookingService saga compensation
/// pipeline (Plan 03-01). The approval is atomic with:
///   1. Status flip on <c>backoffice.CancellationRequests</c>
///   2. Append to <c>dbo.BookingEvents</c> (BO-04 audit trail)
///   3. Publish via the BackofficeService EF outbox
///
/// Saga reacts by voiding the PNR (if pre-ticket) or surfacing the
/// booking for manual reconciliation (post-ticket per D-39 — wallet
/// credit is the refund path, NEVER a Stripe card refund v1).
/// </summary>
public record BookingCancellationApproved(
    Guid BookingId,
    string ReasonCode,
    string Reason,
    string RequestedBy,
    string ApprovedBy,
    string ApprovalReason,
    DateTime ApprovedAt);
