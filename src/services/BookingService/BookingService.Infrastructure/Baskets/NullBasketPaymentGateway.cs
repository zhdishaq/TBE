using Microsoft.Extensions.Logging;
using TBE.BookingService.Application.Baskets;

namespace TBE.BookingService.Infrastructure.Baskets;

/// <summary>
/// Default DI binding for <see cref="IBasketPaymentGateway"/>. Deliberately a no-op
/// fail-closed adapter so local dev and unit tests run without a live Stripe/PaymentService
/// integration. Production replaces this with a bus-command adapter that forwards
/// Authorize / CapturePartial / Void to PaymentService (PCI SAQ-A isolation per PAY-08).
/// <para>
/// All methods log a warning and throw — this is intentional: if something in BookingService
/// actually tries to take payment, the absence of a real adapter must be visible in CI and
/// local-dev logs rather than silently "succeeding".
/// </para>
/// </summary>
public sealed class NullBasketPaymentGateway : IBasketPaymentGateway
{
    private readonly ILogger<NullBasketPaymentGateway> _log;

    public NullBasketPaymentGateway(ILogger<NullBasketPaymentGateway> log)
    {
        _log = log;
    }

    public Task<BasketAuthorizeResult> AuthorizeBasketAsync(
        Guid basketId, decimal amount, string currency, string idempotencyKey, CancellationToken ct)
    {
        _log.LogWarning(
            "NullBasketPaymentGateway.AuthorizeBasketAsync invoked for basket {BasketId} — production wiring missing",
            basketId);
        throw new InvalidOperationException(
            "IBasketPaymentGateway is not wired. Register a PaymentService-backed adapter before taking payment.");
    }

    public Task<BasketCaptureResult> CapturePartialAsync(
        string paymentIntentId, long amountToCaptureMinorUnits, bool finalCapture, string idempotencyKey, CancellationToken ct)
    {
        _log.LogWarning(
            "NullBasketPaymentGateway.CapturePartialAsync invoked for pi {PaymentIntentId} — production wiring missing",
            paymentIntentId);
        throw new InvalidOperationException(
            "IBasketPaymentGateway is not wired. Register a PaymentService-backed adapter before capturing payment.");
    }

    public Task VoidAsync(string paymentIntentId, string idempotencyKey, CancellationToken ct)
    {
        _log.LogWarning(
            "NullBasketPaymentGateway.VoidAsync invoked for pi {PaymentIntentId} — production wiring missing",
            paymentIntentId);
        throw new InvalidOperationException(
            "IBasketPaymentGateway is not wired. Register a PaymentService-backed adapter before voiding payment.");
    }
}
