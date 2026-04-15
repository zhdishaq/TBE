namespace TBE.Contracts.Events;

// BookingInitiated, BookingConfirmed, BookingFailed moved to SagaEvents.cs
// (richer shapes with pricing/channel metadata + EventId for idempotency).
// PaymentProcessed remains here as a Phase 2/3 cross-service event
// not owned by the BookingSaga state machine.

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
