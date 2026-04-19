using TBE.PaymentService.Application.Wallet;

namespace TBE.PaymentService.Infrastructure.Wallet;

/// <summary>
/// Append-only ledger row per D-14 / PAY-05. No mutable balance column.
/// SignedAmount is a PERSISTED computed column (negative for Reserve/Commit, positive for Release/TopUp).
/// </summary>
public sealed class WalletTransaction
{
    public Guid TxId { get; set; }
    public Guid WalletId { get; set; }
    public Guid? BookingId { get; set; }
    public WalletEntryType EntryType { get; set; }
    public decimal Amount { get; set; }
    public decimal SignedAmount { get; set; }
    public string Currency { get; set; } = "GBP";
    public string IdempotencyKey { get; set; } = default!;
    public DateTime CreatedAtUtc { get; set; }
    public Guid? CorrelatesWithTx { get; set; }

    /// <summary>
    /// D-39 — for <see cref="WalletEntryType.ManualCredit"/> (and later
    /// <see cref="WalletEntryType.CommissionPayout"/>) rows, this captures
    /// the ops-admin who signed off the 4-eyes approval. NULL for system
    /// entries (Reserve/Commit/Release/TopUp).
    /// </summary>
    public string? ApprovedBy { get; set; }

    /// <summary>
    /// D-39 — audit free-text supplied by the approver on the
    /// <c>WalletCreditRequests</c> row. NULL for system entries.
    /// </summary>
    public string? ApprovalNotes { get; set; }
}
