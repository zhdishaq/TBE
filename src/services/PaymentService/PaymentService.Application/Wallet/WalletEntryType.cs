namespace TBE.PaymentService.Application.Wallet;

/// <summary>
/// WalletTransactions.EntryType discriminator per D-14 append-only ledger.
/// </summary>
public enum WalletEntryType : byte
{
    Reserve = 1,
    Commit  = 2,
    Release = 3,
    TopUp   = 4
}
