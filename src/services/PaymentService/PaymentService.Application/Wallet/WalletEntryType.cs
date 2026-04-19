namespace TBE.PaymentService.Application.Wallet;

/// <summary>
/// WalletTransactions.EntryType discriminator per D-14 append-only ledger.
///
/// SignedAmount computed column in SQL is:
///   CASE WHEN [EntryType] IN (1,2) THEN -[Amount] ELSE [Amount] END
/// → Reserve (1) and Commit (2) are negative (hold / consume funds).
/// → Release (3), TopUp (4), ManualCredit (5), CommissionPayout (6) are positive.
/// Values 5 and 6 are added by migration 20260601200000_AddManualCreditKind
/// (Plan 06-01 Task 6 / Plan 06-03) — the existing SignedAmount CASE handles
/// them without schema change because they fall outside the "IN (1,2)" set.
/// </summary>
public enum WalletEntryType : byte
{
    Reserve          = 1,
    Commit           = 2,
    Release          = 3,
    TopUp            = 4,

    /// <summary>
    /// D-39 — post-ticket refund or goodwill credit. Written by
    /// <c>WalletCreditApprovedConsumer</c> after a 4-eyes approval on
    /// <c>backoffice.WalletCreditRequests</c>. Positive SignedAmount.
    /// </summary>
    ManualCredit     = 5,

    /// <summary>
    /// D-54 / Plan 06-03 — agency commission payout. Reserved here so
    /// Plan 06-03 does not need another ledger-schema migration.
    /// Positive SignedAmount.
    /// </summary>
    CommissionPayout = 6,
}
