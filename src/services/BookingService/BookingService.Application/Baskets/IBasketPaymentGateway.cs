namespace TBE.BookingService.Application.Baskets;

/// <summary>
/// BookingService-local abstraction that encapsulates the SINGLE-PaymentIntent lifecycle
/// used by Trip Builder basket checkout per CONTEXT D-08 / D-10. The only surface the
/// <see cref="TBE.BookingService.API.Controllers.BasketsController"/> and
/// <see cref="BasketPaymentOrchestrator"/> ever touches for payments; keeps
/// <c>Stripe.net</c> out of the BookingService project (PAY-08 PCI SAQ-A isolation).
/// <para>
/// Deterministic idempotency keys (T-04-04-02):
/// </para>
/// <list type="bullet">
///   <item><c>basket-{basketId}-authorize</c> — single combined PI creation (D-08 forbids <c>-authorize-flight</c> / <c>-authorize-hotel</c> variants).</item>
///   <item><c>basket-{basketId}-capture-flight</c> — partial capture with <c>FinalCapture=false</c> for flight portion (D-10 stage 1).</item>
///   <item><c>basket-{basketId}-capture-hotel</c> — final capture with <c>FinalCapture=true</c> for hotel portion (D-10 stage 2, full-success path).</item>
///   <item><c>basket-{basketId}-finalize-partial</c> — <c>AmountToCapture=0, FinalCapture=true</c> to release the uncaptured hotel remainder on D-09 partial failure.</item>
///   <item><c>basket-{basketId}-void</c> — cancel the PI on hard failure before any capture.</item>
/// </list>
/// </summary>
public interface IBasketPaymentGateway
{
    /// <summary>
    /// Create the SINGLE combined PaymentIntent for a basket with <c>capture_method=manual</c>.
    /// </summary>
    Task<BasketAuthorizeResult> AuthorizeBasketAsync(
        Guid basketId,
        decimal amount,
        string currency,
        string idempotencyKey,
        CancellationToken ct);

    /// <summary>
    /// Partially (or finally) capture the PaymentIntent. On the flight stage the caller passes
    /// <c>finalCapture=false</c> so the remaining authorization stays alive; the hotel stage
    /// (or release-remainder path) passes <c>finalCapture=true</c>.
    /// </summary>
    Task<BasketCaptureResult> CapturePartialAsync(
        string paymentIntentId,
        long amountToCaptureMinorUnits,
        bool finalCapture,
        string idempotencyKey,
        CancellationToken ct);

    /// <summary>
    /// Cancel / void the PaymentIntent (hard-failure path before any capture happened).
    /// </summary>
    Task VoidAsync(
        string paymentIntentId,
        string idempotencyKey,
        CancellationToken ct);
}

public record BasketAuthorizeResult(string PaymentIntentId, string ClientSecret, string Status);

public record BasketCaptureResult(string PaymentIntentId, long AmountCaptured, string Status);
