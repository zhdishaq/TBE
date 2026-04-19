using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace TBE.BackofficeService.Application.Entities;

/// <summary>
/// D-39 / D-48 — a request for a manual wallet credit (post-ticket
/// refund or goodwill). Opens under <c>PendingApproval</c> and flips
/// to <c>Approved</c> / <c>Denied</c> via the 4-eyes state machine;
/// expires automatically at <c>ExpiresAt</c> (72h TTL).
/// </summary>
[Table("WalletCreditRequests", Schema = "backoffice")]
public sealed class WalletCreditRequest
{
    [Key]
    public Guid Id { get; set; }

    public Guid AgencyId { get; set; }

    public decimal Amount { get; set; }

    [MaxLength(3)]
    public string Currency { get; set; } = "GBP";

    /// <summary>D-53 enum: RefundedBooking | GoodwillCredit | DisputeResolution | SupplierRefundPassthrough.</summary>
    [MaxLength(64)]
    public string ReasonCode { get; set; } = string.Empty;

    public Guid? LinkedBookingId { get; set; }

    [MaxLength(1000)]
    public string Notes { get; set; } = string.Empty;

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
    public string? ApprovalNotes { get; set; }
}
