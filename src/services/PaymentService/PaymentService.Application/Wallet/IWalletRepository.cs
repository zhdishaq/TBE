namespace TBE.PaymentService.Application.Wallet;

/// <summary>
/// Append-only wallet ledger contract (D-14). All write paths go through here; the
/// implementation guards concurrency with <c>WITH (UPDLOCK, ROWLOCK, HOLDLOCK)</c>.
/// </summary>
public interface IWalletRepository
{
    Task<Guid> ReserveAsync(Guid walletId, Guid bookingId, decimal amount, string currency, CancellationToken ct);
    Task CommitAsync(Guid walletId, Guid bookingId, Guid reservationTxId, CancellationToken ct);
    Task ReleaseAsync(Guid walletId, Guid bookingId, Guid reservationTxId, CancellationToken ct);
    Task<Guid> TopUpAsync(Guid walletId, decimal amount, string currency, string idempotencyKey, CancellationToken ct);
    Task<decimal> GetBalanceAsync(Guid walletId, CancellationToken ct);
    Task<IReadOnlyList<WalletTransactionDto>> ListAsync(
        Guid walletId, DateTimeOffset? from, DateTimeOffset? to, int page, int size, CancellationToken ct);
}

public sealed record WalletTransactionDto(
    Guid TxId,
    Guid WalletId,
    Guid? BookingId,
    WalletEntryType EntryType,
    decimal Amount,
    decimal SignedAmount,
    string Currency,
    string IdempotencyKey,
    DateTime CreatedAtUtc);
