namespace TBE.PaymentService.Application.Wallet;

/// <summary>
/// Plan 05-03 Task 2 — metadata repository for <see cref="AgencyWallet"/>.
/// Thin wrapper over the <c>payment.AgencyWallets</c> table; the append-only
/// ledger itself is still reached via <see cref="IWalletRepository"/>.
/// </summary>
public interface IAgencyWalletRepository
{
    /// <summary>Find the agency's metadata row, or <c>null</c> when absent.</summary>
    Task<AgencyWallet?> GetAsync(Guid agencyId, CancellationToken ct);

    /// <summary>Upsert the threshold + currency. Resets
    /// <see cref="AgencyWallet.LowBalanceEmailSent"/> to <c>false</c> so lowering
    /// the threshold below current balance re-arms the alert (hysteresis).</summary>
    Task SetThresholdAsync(Guid agencyId, decimal threshold, string currency, CancellationToken ct);

    /// <summary>Flip <see cref="AgencyWallet.LowBalanceEmailSent"/> to
    /// <c>true</c> and stamp <see cref="AgencyWallet.LastLowBalanceEmailAtUtc"/>.
    /// Called by the consumer after a successful e-mail send.</summary>
    Task MarkLowBalanceEmailSentAsync(Guid agencyId, DateTime atUtc, CancellationToken ct);

    /// <summary>Reset the sent flag (the monitor will re-evaluate). Called by
    /// <c>WalletTopUpService</c> when balance crosses back above threshold and
    /// by <c>PUT /api/wallet/threshold</c>.</summary>
    Task ResetLowBalanceEmailFlagAsync(Guid agencyId, CancellationToken ct);

    /// <summary>Return every agency whose SUM(SignedAmount) &lt; threshold and
    /// whose sent flag is <c>false</c>. Projected with the pre-computed balance
    /// so the monitor can publish <c>WalletLowBalanceDetected</c> without an extra
    /// round-trip per agency.</summary>
    Task<IReadOnlyList<AgencyBalanceSnapshot>> ListAgenciesBelowThresholdAsync(CancellationToken ct);
}

/// <summary>Projection of (agencyId, balance, threshold, currency) used by the monitor.</summary>
public sealed record AgencyBalanceSnapshot(
    Guid AgencyId,
    decimal Balance,
    decimal Threshold,
    string Currency);
