namespace TBE.PaymentService.Application.Wallet;

/// <summary>
/// Plan 06-04 / CRM-02 / D-61 / T-6-59 — audit row written on every
/// <c>AgencyCreditLimitController.PATCH</c>. Combined with the
/// <c>AgencyCreditLimitChanged</c> outbox publish this gives
/// non-repudiation for credit-limit changes: the SQL row is the local
/// ledger, the event is the fan-out to downstream observers.
/// </summary>
public sealed class CreditLimitAuditLogRow
{
    public Guid Id { get; set; } = Guid.NewGuid();

    /// <summary>Agency whose <c>AgencyWallets.CreditLimit</c> was changed.</summary>
    public Guid AgencyId { get; set; }

    /// <summary>Previous <c>CreditLimit</c> value (pre-patch).</summary>
    public decimal OldLimit { get; set; }

    /// <summary>New <c>CreditLimit</c> value (post-patch).</summary>
    public decimal NewLimit { get; set; }

    /// <summary>Keycloak <c>preferred_username</c> of the ops-finance /
    /// ops-admin staff member who issued the change (Pitfall 28 fail-closed).</summary>
    public string ChangedBy { get; set; } = string.Empty;

    /// <summary>Free-form reason (10-500 chars) captured by the portal.</summary>
    public string Reason { get; set; } = string.Empty;

    /// <summary>Wall-clock UTC of the change.</summary>
    public DateTime ChangedAtUtc { get; set; } = DateTime.UtcNow;
}
