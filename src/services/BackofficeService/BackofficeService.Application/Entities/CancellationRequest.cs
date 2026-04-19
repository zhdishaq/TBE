using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace TBE.BackofficeService.Application.Entities;

/// <summary>
/// BO-03 / D-48 — a staff-initiated booking cancellation request. Opened
/// by ops-cs; approved by a different ops-admin (4-eyes). On approval the
/// consumer publishes <c>BookingCancellationApproved</c> to the
/// BookingService compensation pipeline.
/// </summary>
[Table("CancellationRequests", Schema = "backoffice")]
public sealed class CancellationRequest
{
    [Key]
    public Guid Id { get; set; }

    public Guid BookingId { get; set; }

    /// <summary>
    /// Locked enum (CHECK constraint):
    /// CustomerRequest | SupplierInitiated | FareRuleViolation |
    /// FraudSuspected | DuplicateBooking | Other.
    /// </summary>
    [MaxLength(64)]
    public string ReasonCode { get; set; } = string.Empty;

    [MaxLength(500)]
    public string Reason { get; set; } = string.Empty;

    [MaxLength(128)]
    public string RequestedBy { get; set; } = string.Empty;

    public DateTime RequestedAt { get; set; }

    public DateTime ExpiresAt { get; set; }

    /// <summary>PendingApproval | Approved | Denied | Expired</summary>
    [MaxLength(32)]
    public string Status { get; set; } = "PendingApproval";

    [MaxLength(128)]
    public string? ApprovedBy { get; set; }

    public DateTime? ApprovedAt { get; set; }

    [MaxLength(500)]
    public string? ApprovalReason { get; set; }
}
