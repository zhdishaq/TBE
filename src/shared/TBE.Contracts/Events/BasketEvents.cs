namespace TBE.Contracts.Events;

/// <summary>
/// Published when a B2C customer starts a combined Trip Builder basket (flight + hotel
/// today; optional car forward-compat). Starts the BasketPaymentOrchestrator.
/// The <c>TotalAmount</c> is SERVER-COMPUTED (T-04-04-01) from the line-item offers; the
/// request body's amount is never trusted. CONTEXT D-08 — one combined PaymentIntent
/// backs the basket; this event carries ONE Amount, not per-leg subtotals.
/// </summary>
public record BasketInitiated(
    Guid BasketId,
    string UserId,
    Guid? FlightOfferId,
    Guid? HotelOfferId,
    Guid? CarOfferId,
    decimal TotalAmount,
    string Currency,
    DateTimeOffset At);

/// <summary>
/// Published by BasketsController once a single combined Stripe PaymentIntent has been
/// created and authorized (capture_method=manual). CONTEXT D-08: ONE PaymentIntent per
/// basket — the field below is a SINGLE <c>PaymentIntentId</c>, NOT per-leg pairs.
/// Any future "FlightPaymentIntentId" / "HotelPaymentIntentId" split is forbidden.
/// </summary>
public record BasketPaymentAuthorized(
    Guid BasketId,
    string PaymentIntentId,
    decimal AuthorizedAmount,
    string Currency,
    DateTimeOffset At);

/// <summary>
/// Terminal success event — both flight and hotel legs confirmed and their respective
/// portions of the single combined PaymentIntent have been captured sequentially per
/// CONTEXT D-10 (flight partial → hotel final capture). <c>EventId</c> is the NOTF-06
/// idempotency key per D-19 — NotificationService keys the basket-confirmation email
/// off <c>(EventId, EmailType.BasketConfirmation)</c>.
/// </summary>
public record BasketConfirmed(
    Guid BasketId,
    Guid EventId,
    string FlightBookingReference,
    string HotelSupplierRef,
    string GuestEmail,
    string GuestFullName,
    decimal TotalAmount,
    string Currency,
    DateTimeOffset At);

/// <summary>
/// Terminal partial-success event — one leg succeeded, the other failed after the first
/// leg was already partially-captured. CONTEXT D-09 release-remainder semantics: the
/// successful leg's subtotal is the customer's actual statement charge; the failed leg's
/// authorization was released (never captured), so <c>RefundedAmount</c> records what was
/// held-then-released, not a refund of a captured charge. <c>EventId</c> is the NOTF-06
/// idempotency key for the single partial-success email (D-09 — one email, one statement
/// entry).
/// </summary>
public record BasketPartiallyConfirmed(
    Guid BasketId,
    Guid EventId,
    string SucceededComponent,
    string FailedComponent,
    string? FlightBookingReference,
    string? HotelSupplierRef,
    string GuestEmail,
    string GuestFullName,
    decimal ChargedAmount,
    decimal RefundedAmount,
    string Currency,
    string Cause,
    DateTimeOffset At);

/// <summary>
/// Terminal failure event — basket could not be confirmed before any capture happened.
/// The orchestrator voided the single PaymentIntent so no charges appear on the customer's
/// statement.
/// </summary>
public record BasketFailed(
    Guid BasketId,
    string Cause,
    DateTimeOffset At);
