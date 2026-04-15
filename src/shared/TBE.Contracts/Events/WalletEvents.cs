namespace TBE.Contracts.Events;

public record WalletReserved(Guid BookingId, Guid WalletId, Guid ReservationTxId, decimal Amount, DateTimeOffset At);

public record WalletReservationFailed(Guid BookingId, Guid WalletId, string Cause, decimal AttemptedAmount, decimal AvailableBalance);

public record WalletCommitted(Guid BookingId, Guid WalletId, Guid ReservationTxId, DateTimeOffset At);

public record WalletReleased(Guid BookingId, Guid WalletId, Guid ReservationTxId, DateTimeOffset At);

public record WalletLowBalance(Guid WalletId, decimal Balance, decimal Threshold, DateTimeOffset At);

public record WalletToppedUp(Guid WalletId, Guid? AgencyId, decimal Amount, string Currency, string PaymentIntentId, DateTimeOffset At);
