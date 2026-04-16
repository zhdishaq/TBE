namespace TBE.PaymentService.Application.Stripe;

/// <summary>
/// Only abstraction through which non-PaymentService code ever touches Stripe (PCI SAQ-A isolation per PAY-08).
/// Implementation lives in <c>PaymentService.Infrastructure</c> so that <c>Stripe.net</c> is NEVER
/// referenced from application-level command consumers.
/// </summary>
public interface IStripePaymentGateway
{
    /// <summary>
    /// Creates a PaymentIntent with <c>capture_method=manual</c> and idempotency key
    /// <c>booking-{bookingId}-authorize</c> per D-13.
    /// </summary>
    Task<AuthorizeResult> AuthorizeAsync(
        Guid bookingId,
        decimal amountCents,
        string currency,
        string stripeCustomerId,
        string paymentMethodId,
        CancellationToken ct);

    /// <summary>
    /// Captures a previously-authorized PaymentIntent. Idempotency key <c>booking-{bookingId}-capture</c>.
    /// </summary>
    Task<CaptureResult> CaptureAsync(
        Guid bookingId,
        string paymentIntentId,
        decimal amountCents,
        CancellationToken ct);

    /// <summary>
    /// Plan 04-04 / D-10 — sequential partial capture against a single combined basket
    /// PaymentIntent. Caller supplies the <paramref name="amountToCaptureMinorUnits"/>
    /// (Stripe.net expects minor units) and chooses whether this capture closes the PI
    /// via <paramref name="finalCapture"/>. Deterministic idempotency keys live in the
    /// BookingService basket orchestrator (e.g. <c>basket-{id}-capture-flight</c>,
    /// <c>basket-{id}-capture-hotel</c>, <c>basket-{id}-finalize-partial</c>).
    /// </summary>
    Task<CaptureResult> CapturePartialAsync(
        string paymentIntentId,
        long amountToCaptureMinorUnits,
        bool finalCapture,
        string idempotencyKey,
        CancellationToken ct);

    /// <summary>
    /// Plan 04-04 / D-09 hard-failure path — cancels a basket PaymentIntent BEFORE any
    /// capture happened so the customer sees zero charges on their statement. Caller-supplied
    /// idempotency key (e.g. <c>basket-{id}-void</c>) for MassTransit retry safety.
    /// </summary>
    Task VoidAsync(string paymentIntentId, string idempotencyKey, CancellationToken ct);

    /// <summary>
    /// Cancels an authorization (saga compensation path).
    /// </summary>
    Task CancelAsync(Guid bookingId, string paymentIntentId, CancellationToken ct);

    /// <summary>
    /// Issues a refund against a captured PaymentIntent. Returns the Stripe refund id.
    /// </summary>
    Task<string> RefundAsync(Guid bookingId, string paymentIntentId, decimal amountCents, CancellationToken ct);

    /// <summary>
    /// PAY-04 wallet top-up: immediate-capture PaymentIntent with wallet metadata.
    /// Not part of the saga; outcome is observed via the Stripe webhook.
    /// </summary>
    Task<AuthorizeResult> CreateWalletTopUpAsync(
        Guid walletId,
        Guid agencyId,
        decimal amountCents,
        string currency,
        string stripeCustomerId,
        string paymentMethodId,
        CancellationToken ct);
}

public record AuthorizeResult(string PaymentIntentId, string Status);

public record CaptureResult(string PaymentIntentId, string Status);

/// <summary>
/// Raised by <see cref="IStripePaymentGateway"/> when an underlying Stripe API call fails.
/// Never carries card-holder data.
/// </summary>
public sealed class PaymentGatewayException : Exception
{
    public string? StripeCode { get; }

    public PaymentGatewayException(string message, string? stripeCode = null, Exception? inner = null)
        : base(message, inner)
    {
        StripeCode = stripeCode;
    }
}
