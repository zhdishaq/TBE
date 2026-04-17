namespace TBE.Contracts.Events;

// WalletReserved moved to Messages/WalletReserved.cs in 05-02 Task 2 with
// plan-shape (CorrelationId, BookingId, LedgerEntryId, BalanceAfter) so the
// BookingSaga can correlate by CorrelationId and surface post-reserve balance
// to the wallet chip (Plan 05-03).
//
// WalletReservationFailed superseded by Messages/WalletReserveFailed.cs
// (plan-shape (CorrelationId, BookingId, Reason)). Old balance fields were
// consumer-implementation-specific and never observed outside PaymentService.

public record WalletCommitted(Guid BookingId, Guid WalletId, Guid ReservationTxId, DateTimeOffset At);

public record WalletReleased(Guid BookingId, Guid WalletId, Guid ReservationTxId, DateTimeOffset At);

// WalletLowBalance moved to NotificationEvents.cs (plan 03-04 contract owner).
// Duplicate record definition removed here in 03-03 (Rule 3 auto-fix: blocking build failure).

public record WalletToppedUp(Guid WalletId, Guid? AgencyId, decimal Amount, string Currency, string PaymentIntentId, DateTimeOffset At);
