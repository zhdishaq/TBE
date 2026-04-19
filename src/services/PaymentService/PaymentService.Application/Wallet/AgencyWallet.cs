namespace TBE.PaymentService.Application.Wallet;

/// <summary>
/// Plan 05-03 Task 2 — per-agency wallet metadata (the append-only
/// <see cref="WalletEntryType"/> ledger itself lives in
/// <c>payment.WalletTransactions</c>). 1:1 with agency —
/// <see cref="Id"/> equals the agency's id so the existing
/// <see cref="IWalletRepository"/> (keyed by walletId) keeps working without
/// an extra lookup.
/// </summary>
/// <remarks>
/// Hysteresis state for low-balance alerts lives here:
/// <list type="bullet">
///   <item><see cref="LowBalanceThresholdAmount"/> — the configured floor.</item>
///   <item><see cref="LowBalanceEmailSent"/> — flipped to <c>true</c> by the
///   consumer after a successful e-mail send; reset to <c>false</c> by
///   <c>WalletTopUpService</c> on balance-cross-up or by
///   <c>PUT /api/wallet/threshold</c>. T-05-03-07.</item>
///   <item><see cref="LastLowBalanceEmailAtUtc"/> — persisted so the consumer
///   can enforce the <c>EmailCooldownHours</c> defence-in-depth.</item>
/// </list>
/// </remarks>
public sealed class AgencyWallet
{
    /// <summary>Primary key — equal to the agency id (1:1).</summary>
    public Guid Id { get; set; }

    /// <summary>Redundant with <see cref="Id"/> but enforced via a UNIQUE index
    /// to make cross-tenant mis-writes a loud failure mode.</summary>
    public Guid AgencyId { get; set; }

    /// <summary>ISO-4217 currency code.</summary>
    public string Currency { get; set; } = "GBP";

    /// <summary>Configured alert floor. Admin can edit via <c>PUT /api/wallet/threshold</c>.</summary>
    public decimal LowBalanceThresholdAmount { get; set; } = 500m;

    /// <summary>Gate for the monitor query: only agencies with <c>false</c> are polled.</summary>
    public bool LowBalanceEmailSent { get; set; }

    /// <summary>Last successful low-balance e-mail timestamp (UTC), or <c>null</c> if never sent.</summary>
    public DateTime? LastLowBalanceEmailAtUtc { get; set; }

    /// <summary>
    /// Plan 06-04 / CRM-02 / D-61 — agent overdraft allowance. Reserves up to
    /// <c>balance + CreditLimit</c> succeed; anything above fails with
    /// <c>/errors/wallet-credit-over-limit</c>. Defaults to 0 (no overdraft)
    /// so the backfilled migration is a no-op for existing wallets.
    /// Editable only via <c>AgencyCreditLimitController.PATCH</c>
    /// (BackofficeFinancePolicy + audit-log row + outbox publish).
    /// </summary>
    public decimal CreditLimit { get; set; } = 0m;

    /// <summary>Last mutation wall-clock (UTC).</summary>
    public DateTime UpdatedAtUtc { get; set; } = DateTime.UtcNow;
}
