namespace TBE.Contracts.Events;

/// <summary>
/// Published when a booking is initiated by any channel (B2C or B2B).
/// Starts the booking saga in Phase 3.
/// </summary>
public record BookingInitiated(
    Guid BookingId,
    string ProductType,   // "flight" | "hotel" | "car"
    string Channel,       // "b2c" | "b2b"
    string UserId,
    DateTimeOffset InitiatedAt);

/// <summary>
/// Published when a booking is fully confirmed (PNR created + payment captured + ticket issued).
/// Triggers confirmation email in Notification Service.
/// </summary>
public record BookingConfirmed(
    Guid BookingId,
    string SupplierRef,
    string Channel,
    DateTimeOffset ConfirmedAt);

/// <summary>
/// Published when a booking fails at any saga step after compensation is complete.
/// Triggers failure email and backoffice dead-letter queue entry.
/// </summary>
public record BookingFailed(
    Guid BookingId,
    string Reason,
    string FailedAt,
    DateTimeOffset OccurredAt);

/// <summary>
/// Published by Payment Service when Stripe payment is processed.
/// Advances booking saga to the ticket-issuing step.
/// </summary>
public record PaymentProcessed(
    Guid BookingId,
    string PaymentIntentId,
    decimal Amount,
    string Currency,
    DateTimeOffset ProcessedAt);
