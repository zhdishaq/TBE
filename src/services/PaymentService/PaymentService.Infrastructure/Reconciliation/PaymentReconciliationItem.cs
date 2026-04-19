namespace TBE.PaymentService.Infrastructure.Reconciliation;

/// <summary>
/// Plan 06-02 Task 3 (BO-06) — row in <c>payment.PaymentReconciliationQueue</c>.
/// Written by the nightly <c>PaymentReconciliationService</c> and worked
/// down by ops-finance via the <c>ReconciliationController</c> resolve
/// endpoint.
///
/// <para>
/// <b>DiscrepancyType</b> enum:
/// <list type="bullet">
///   <item><c>OrphanStripeEvent</c>: Stripe charge.succeeded in window
///         with no matching WalletTransactions row.</item>
///   <item><c>OrphanWalletRow</c>: WalletTransactions row with no
///         matching Stripe event in the window.</item>
///   <item><c>AmountDrift</c>: both sides present, Amount differs.
///         Severity = Low (|drift| ≤ £5) or High (&gt; £5).</item>
///   <item><c>UnprocessedEvent</c>: StripeWebhookEvent with Processed=0
///         older than 1 hour.</item>
/// </list>
/// </para>
///
/// <para>
/// <b>Status</b>: Pending → Resolved. No hard delete — audit trail is
/// preserved via ResolvedBy / ResolvedAt / ResolutionNotes columns.
/// </para>
/// </summary>
public sealed class PaymentReconciliationItem
{
    public Guid Id { get; set; }

    /// <summary>OrphanStripeEvent | OrphanWalletRow | AmountDrift | UnprocessedEvent</summary>
    public string DiscrepancyType { get; set; } = string.Empty;

    /// <summary>Low | Medium | High — drives portal sort + banner colour.</summary>
    public string Severity { get; set; } = "Medium";

    /// <summary>NULL when only Stripe side is known (e.g. orphan Stripe event without booking metadata).</summary>
    public Guid? BookingId { get; set; }

    /// <summary>NULL for OrphanWalletRow discrepancies.</summary>
    public string? StripeEventId { get; set; }

    /// <summary>JSON blob with side-by-side Stripe vs wallet snapshots so the portal diff viewer can render without a second DB round-trip.</summary>
    public string Details { get; set; } = "{}";

    public DateTime DetectedAtUtc { get; set; }

    /// <summary>Pending | Resolved</summary>
    public string Status { get; set; } = "Pending";

    public string? ResolvedBy { get; set; }
    public DateTime? ResolvedAtUtc { get; set; }
    public string? ResolutionNotes { get; set; }
}
