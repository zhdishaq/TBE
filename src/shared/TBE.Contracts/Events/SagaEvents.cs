namespace TBE.Contracts.Events;

/// <summary>
/// Published when a booking is initiated by any channel (B2C or B2B). Starts the BookingSaga.
/// Matches the public POST /bookings request body plus server-assigned BookingId/BookingReference.
/// </summary>
public record BookingInitiated(
    Guid BookingId,
    string ProductType,   // "flight" | "hotel" | "car"
    string Channel,       // "b2c" | "b2b"
    string UserId,
    string BookingReference,
    decimal TotalAmount,
    string Currency,
    string PaymentMethod, // "card" | "wallet"
    Guid? WalletId,
    DateTimeOffset InitiatedAt);

/// <summary>
/// Plan 05-02 Task 2 — published by <c>AgentBookingsController</c> immediately after
/// <see cref="BookingInitiated"/> when the channel is B2B. Carries the server-stamped
/// <c>AgencyId</c> (D-33), the frozen agency pricing snapshot (D-36/D-41), the
/// per-booking markup override (D-37, admin-only — enforced at the controller),
/// and the customer-contact snapshot captured at on-behalf booking time (B2B-04).
/// Consumed by the <c>BookingSaga</c> via a <c>DuringAny</c> handler so it lands on
/// the saga state before <see cref="PnrCreated"/> fires and the B2B IfElse branch
/// evaluates.
/// </summary>
public record AgentBookingDetailsCaptured(
    Guid BookingId,
    Guid AgencyId,
    decimal AgencyNetFare,
    decimal AgencyMarkupAmount,
    decimal AgencyGrossAmount,
    decimal AgencyCommissionAmount,
    decimal? AgencyMarkupOverride,
    string? CustomerName,
    string? CustomerEmail,
    string? CustomerPhone,
    string? OfferId,
    DateTimeOffset At);

/// <summary>
/// Published once the PricingService re-confirms the live fare against the stored offer token.
/// </summary>
public record PriceReconfirmed(Guid BookingId, decimal ReconfirmedAmount, string Currency, DateTimeOffset At);

/// <summary>
/// Published by FlightConnectorService once a PNR is created in the GDS.
/// Carries the ticketing deadline used by the 03-03 TTL monitor.
/// </summary>
public record PnrCreated(Guid BookingId, string Pnr, DateTime TicketingDeadlineUtc, DateTimeOffset At);

/// <summary>
/// Published by PaymentService once Stripe has authorized (but not captured) the card.
/// </summary>
public record PaymentAuthorized(Guid BookingId, string PaymentIntentId, DateTimeOffset At);

/// <summary>
/// Published by FlightConnectorService once the ticket is issued against the PNR.
/// </summary>
public record TicketIssued(Guid BookingId, string TicketNumber, DateTimeOffset At);

/// <summary>
/// Published by PaymentService once the Stripe PaymentIntent has been captured.
/// </summary>
public record PaymentCaptured(Guid BookingId, string PaymentIntentId, DateTimeOffset At);

/// <summary>
/// Terminal success event. EventId is the notification idempotency key per D-19.
/// </summary>
public record BookingConfirmed(
    Guid BookingId,
    Guid EventId,
    string BookingReference,
    string Pnr,
    string TicketNumber,
    string PaymentIntentId,
    DateTimeOffset At);

/// <summary>
/// Terminal cancellation event (customer or compensation driven). EventId is the idempotency key.
/// </summary>
public record BookingCancelled(Guid BookingId, Guid EventId, string Cause, DateTimeOffset At);

/// <summary>
/// Terminal expiry event raised by the 03-03 TTL monitor when the ticketing deadline passes.
/// </summary>
public record BookingExpired(Guid BookingId, Guid EventId, DateTimeOffset At);

/// <summary>
/// Terminal failure event published by the saga after compensation completes. EventId is the idempotency key.
/// </summary>
public record BookingFailed(
    Guid BookingId,
    Guid EventId,
    string Cause,
    string LastSuccessfulStep,
    DateTimeOffset At);

/// <summary>
/// Saga failure callback: PricingService could not re-confirm the offer.
/// </summary>
public record PriceReconfirmationFailed(Guid BookingId, string Cause);

/// <summary>
/// Saga failure callback: GDS rejected the PNR creation.
/// </summary>
public record PnrCreationFailed(Guid BookingId, string Cause);

/// <summary>
/// Saga failure callback: Stripe declined authorization.
/// </summary>
public record PaymentAuthorizationFailed(Guid BookingId, string Cause);

/// <summary>
/// Saga failure callback: GDS rejected ticket issuance after PNR+authorization succeeded.
/// </summary>
public record TicketIssuanceFailed(Guid BookingId, string Cause);

/// <summary>
/// Saga failure callback: Stripe declined or errored on capture.
/// </summary>
public record PaymentCaptureFailed(Guid BookingId, string Cause);

/// <summary>
/// Scheduled hard-timeout event; fires when the saga's per-booking timer elapses.
/// </summary>
public record BookingTimeoutExpired(Guid BookingId);

/// <summary>
/// Dead-letter escalation raised by the saga after retries are exhausted (e.g. capture failure).
/// Consumed by <c>SagaDeadLetterSink</c> which persists a structured row for ops reconciliation.
/// CorrelationId equals the saga's CorrelationId (which equals BookingId).
/// </summary>
public record SagaDeadLetterRequested(Guid CorrelationId, string StepName, string Cause, DateTimeOffset OccurredAt);
