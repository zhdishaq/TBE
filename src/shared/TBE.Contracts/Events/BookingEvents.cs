namespace TBE.Contracts.Events;

/// <summary>
/// Published when a booking is initiated by any channel (B2C or B2B).
/// </summary>
public record BookingInitiated(
    Guid BookingId,
    string ProductType,
    string Channel,
    string UserId,
    DateTimeOffset InitiatedAt);

/// <summary>
/// Published when a booking is fully confirmed.
/// </summary>
public record BookingConfirmed(
    Guid BookingId,
    string SupplierRef,
    string Channel,
    DateTimeOffset ConfirmedAt);

/// <summary>
/// Published when a booking fails.
/// </summary>
public record BookingFailed(
    Guid BookingId,
    string Reason,
    string FailedAt,
    DateTimeOffset OccurredAt);

/// <summary>
/// Published by Payment Service when Stripe payment is processed.
/// </summary>
public record PaymentProcessed(
    Guid BookingId,
    string PaymentIntentId,
    decimal Amount,
    string Currency,
    DateTimeOffset ProcessedAt);
