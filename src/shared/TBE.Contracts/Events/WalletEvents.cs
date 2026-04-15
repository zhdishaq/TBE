namespace TBE.Contracts.Events;

public record WalletReserved(Guid BookingId, Guid WalletId, Guid ReservationTxId, decimal Amount, DateTimeOffset At);

public record WalletReservationFailed(Guid BookingId, Guid WalletId, string Cause, decimal AttemptedAmount, decimal AvailableBalance);

public record WalletCommitted(Guid BookingId, Guid WalletId, Guid ReservationTxId, DateTimeOffset At);

public record WalletReleased(Guid BookingId, Guid WalletId, Guid ReservationTxId, DateTimeOffset At);

// WalletLowBalance moved to NotificationEvents.cs (plan 03-04 contract owner).
// Duplicate record definition removed here in 03-03 (Rule 3 auto-fix: blocking build failure).

public record WalletToppedUp(Guid WalletId, Guid? AgencyId, decimal Amount, string Currency, string PaymentIntentId, DateTimeOffset At);
