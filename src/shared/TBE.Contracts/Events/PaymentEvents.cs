namespace TBE.Contracts.Events;

/// <summary>
/// Published by PaymentService after a successful Stripe Refund call (saga compensation).
/// PaymentAuthorized / PaymentCaptured / PaymentAuthorizationFailed live in SagaEvents.cs.
/// </summary>
public record PaymentRefundIssued(Guid BookingId, string RefundId, DateTimeOffset At);

/// <summary>
/// Single webhook-ingress envelope (W3 boundary).
/// ONLY <c>StripeWebhookController</c> publishes this; consumers
/// (<c>StripeWebhookConsumer</c>, <c>StripeTopUpConsumer</c>) are the sole
/// publishers/writers of downstream saga + wallet effects.
/// </summary>
/// <param name="EventId">Stripe event.Id — unique ingress de-duplication key.</param>
/// <param name="EventType">Stripe event.Type (e.g. "payment_intent.succeeded").</param>
/// <param name="PaymentIntentId">PaymentIntent.Id when the event payload is a PaymentIntent.</param>
/// <param name="BookingId">Extracted from Stripe metadata["booking_id"] when present (saga path).</param>
/// <param name="WalletId">Extracted from Stripe metadata["wallet_id"] when present (top-up path).</param>
/// <param name="TopUpAmount">Extracted from metadata["topup_amount"] when present (minor-unit cents).</param>
/// <param name="AgencyId">Extracted from metadata["agency_id"] when present.</param>
/// <param name="At">Ingress timestamp.</param>
public record StripeWebhookReceived(
    string EventId,
    string EventType,
    string? PaymentIntentId,
    Guid? BookingId,
    Guid? WalletId,
    decimal? TopUpAmount,
    Guid? AgencyId,
    DateTimeOffset At);
