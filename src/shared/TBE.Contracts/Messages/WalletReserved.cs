namespace TBE.Contracts.Events;

/// <summary>
/// Plan 05-02 Task 2 — PaymentService response to
/// <see cref="TBE.Contracts.Commands.WalletReserveCommand"/> on success.
/// Published when a reservation hold has been written to the wallet ledger;
/// consumed by <c>BookingSaga</c> which then transitions to ticketing.
/// </summary>
/// <remarks>
/// <see cref="LedgerEntryId"/> is the reservation transaction id returned by
/// <c>IWalletRepository.ReserveAsync</c>. <see cref="BalanceAfter"/> exposes
/// the post-reserve available balance so downstream consumers (e.g. the
/// 05-03 wallet chip refresh) can surface it without a round-trip.
/// </remarks>
public sealed record WalletReserved(
    Guid CorrelationId,
    Guid BookingId,
    Guid LedgerEntryId,
    decimal BalanceAfter);
