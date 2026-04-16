using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Stripe;

namespace TBE.PaymentService.Application.Stripe;

/// <summary>
/// Concrete Stripe.net adapter. The ONLY class in the codebase allowed to
/// reference <c>Stripe.net</c> PaymentIntent + Refund services directly
/// (PAY-08 PCI SAQ-A isolation). Deterministic idempotency keys per D-13:
/// <code>
///   Authorize → booking-{bookingId}-authorize
///   Capture   → booking-{bookingId}-capture
///   Cancel    → booking-{bookingId}-cancel
///   Refund    → booking-{bookingId}-refund
///   TopUp     → wallet-{walletId}-topup-authorize
/// </code>
/// Never logs Stripe objects, card data, idempotency-key values, or API keys
/// per Pitfall 7.
/// </summary>
public sealed class StripePaymentGateway(
    IOptionsMonitor<StripeOptions> opts,
    ILogger<StripePaymentGateway> log,
    PaymentIntentService? intentService = null,
    RefundService? refundService = null)
    : IStripePaymentGateway
{
    private readonly PaymentIntentService _intents = intentService ?? new PaymentIntentService();
    private readonly RefundService _refunds = refundService ?? new RefundService();

    private void EnsureApiKey()
    {
        var key = opts.CurrentValue.ApiKey;
        if (!string.IsNullOrEmpty(key))
        {
            StripeConfiguration.ApiKey = key;
        }
    }

    public async Task<AuthorizeResult> AuthorizeAsync(
        Guid bookingId,
        decimal amountCents,
        string currency,
        string stripeCustomerId,
        string paymentMethodId,
        CancellationToken ct)
    {
        EnsureApiKey();
        var createOptions = new PaymentIntentCreateOptions
        {
            Amount = (long)amountCents,
            Currency = currency,
            CaptureMethod = "manual",
            Customer = stripeCustomerId,
            PaymentMethod = paymentMethodId,
            Confirm = true,
            Metadata = new Dictionary<string, string>
            {
                ["booking_id"] = bookingId.ToString()
            }
        };
        var requestOptions = new RequestOptions
        {
            IdempotencyKey = $"booking-{bookingId}-authorize"
        };
        try
        {
            var pi = await _intents.CreateAsync(createOptions, requestOptions, ct);
            log.LogInformation("stripe authorize ok booking={BookingId} pi={PaymentIntentId} status={Status}",
                bookingId, pi.Id, pi.Status);
            return new AuthorizeResult(pi.Id, pi.Status);
        }
        catch (StripeException ex)
        {
            log.LogWarning("stripe authorize failed booking={BookingId} code={Code}", bookingId, ex.StripeError?.Code);
            throw new PaymentGatewayException("authorize failed", ex.StripeError?.Code, ex);
        }
    }

    public async Task<CaptureResult> CaptureAsync(
        Guid bookingId,
        string paymentIntentId,
        decimal amountCents,
        CancellationToken ct)
    {
        EnsureApiKey();
        var captureOptions = new PaymentIntentCaptureOptions
        {
            AmountToCapture = (long)amountCents
        };
        var requestOptions = new RequestOptions
        {
            IdempotencyKey = $"booking-{bookingId}-capture"
        };
        try
        {
            // Pitfall 1: this is the ONLY CaptureAsync call in the codebase.
            var pi = await _intents.CaptureAsync(paymentIntentId, captureOptions, requestOptions, ct);
            log.LogInformation("stripe capture ok booking={BookingId} pi={PaymentIntentId} status={Status}",
                bookingId, pi.Id, pi.Status);
            return new CaptureResult(pi.Id, pi.Status);
        }
        catch (StripeException ex)
        {
            log.LogWarning("stripe capture failed booking={BookingId} pi={PaymentIntentId} code={Code}",
                bookingId, paymentIntentId, ex.StripeError?.Code);
            throw new PaymentGatewayException("capture failed", ex.StripeError?.Code, ex);
        }
    }

    public async Task<CaptureResult> CapturePartialAsync(
        string paymentIntentId,
        long amountToCaptureMinorUnits,
        bool finalCapture,
        string idempotencyKey,
        CancellationToken ct)
    {
        EnsureApiKey();
        // D-10: the canonical Stripe.net knobs for sequential partial capture against
        // a single combined PaymentIntent are AmountToCapture + FinalCapture.
        var captureOptions = new PaymentIntentCaptureOptions
        {
            AmountToCapture = amountToCaptureMinorUnits,
            FinalCapture = finalCapture
        };
        var requestOptions = new RequestOptions
        {
            IdempotencyKey = idempotencyKey
        };
        try
        {
            var pi = await _intents.CaptureAsync(paymentIntentId, captureOptions, requestOptions, ct);
            log.LogInformation(
                "stripe basket capture-partial ok pi={PaymentIntentId} final={FinalCapture} status={Status}",
                pi.Id, finalCapture, pi.Status);
            return new CaptureResult(pi.Id, pi.Status);
        }
        catch (StripeException ex)
        {
            log.LogWarning(
                "stripe basket capture-partial failed pi={PaymentIntentId} code={Code}",
                paymentIntentId, ex.StripeError?.Code);
            throw new PaymentGatewayException("capture-partial failed", ex.StripeError?.Code, ex);
        }
    }

    public async Task VoidAsync(string paymentIntentId, string idempotencyKey, CancellationToken ct)
    {
        EnsureApiKey();
        var requestOptions = new RequestOptions
        {
            IdempotencyKey = idempotencyKey
        };
        try
        {
            var pi = await _intents.CancelAsync(paymentIntentId, null, requestOptions, ct);
            log.LogInformation(
                "stripe basket void ok pi={PaymentIntentId} status={Status}",
                pi.Id, pi.Status);
        }
        catch (StripeException ex)
        {
            log.LogWarning(
                "stripe basket void failed pi={PaymentIntentId} code={Code}",
                paymentIntentId, ex.StripeError?.Code);
            throw new PaymentGatewayException("void failed", ex.StripeError?.Code, ex);
        }
    }

    public async Task CancelAsync(Guid bookingId, string paymentIntentId, CancellationToken ct)
    {
        EnsureApiKey();
        var requestOptions = new RequestOptions
        {
            IdempotencyKey = $"booking-{bookingId}-cancel"
        };
        try
        {
            var pi = await _intents.CancelAsync(paymentIntentId, null, requestOptions, ct);
            log.LogInformation("stripe cancel ok booking={BookingId} pi={PaymentIntentId} status={Status}",
                bookingId, pi.Id, pi.Status);
        }
        catch (StripeException ex)
        {
            log.LogWarning("stripe cancel failed booking={BookingId} pi={PaymentIntentId} code={Code}",
                bookingId, paymentIntentId, ex.StripeError?.Code);
            throw new PaymentGatewayException("cancel failed", ex.StripeError?.Code, ex);
        }
    }

    public async Task<string> RefundAsync(Guid bookingId, string paymentIntentId, decimal amountCents, CancellationToken ct)
    {
        EnsureApiKey();
        var refundOptions = new RefundCreateOptions
        {
            PaymentIntent = paymentIntentId,
            Amount = (long)amountCents
        };
        var requestOptions = new RequestOptions
        {
            IdempotencyKey = $"booking-{bookingId}-refund"
        };
        try
        {
            var refund = await _refunds.CreateAsync(refundOptions, requestOptions, ct);
            log.LogInformation("stripe refund ok booking={BookingId} pi={PaymentIntentId} refund={RefundId} status={Status}",
                bookingId, paymentIntentId, refund.Id, refund.Status);
            return refund.Id;
        }
        catch (StripeException ex)
        {
            log.LogWarning("stripe refund failed booking={BookingId} pi={PaymentIntentId} code={Code}",
                bookingId, paymentIntentId, ex.StripeError?.Code);
            throw new PaymentGatewayException("refund failed", ex.StripeError?.Code, ex);
        }
    }

    public async Task<AuthorizeResult> CreateWalletTopUpAsync(
        Guid walletId,
        Guid agencyId,
        decimal amountCents,
        string currency,
        string stripeCustomerId,
        string paymentMethodId,
        CancellationToken ct)
    {
        EnsureApiKey();
        var createOptions = new PaymentIntentCreateOptions
        {
            Amount = (long)amountCents,
            Currency = currency,
            CaptureMethod = "automatic",
            Customer = stripeCustomerId,
            PaymentMethod = paymentMethodId,
            Confirm = true,
            Metadata = new Dictionary<string, string>
            {
                ["wallet_id"] = walletId.ToString(),
                ["topup_amount"] = ((long)amountCents).ToString(),
                ["agency_id"] = agencyId.ToString()
            }
        };
        var requestOptions = new RequestOptions
        {
            IdempotencyKey = $"wallet-{walletId}-topup-authorize"
        };
        try
        {
            var pi = await _intents.CreateAsync(createOptions, requestOptions, ct);
            log.LogInformation("stripe wallet top-up created wallet={WalletId} pi={PaymentIntentId} status={Status}",
                walletId, pi.Id, pi.Status);
            return new AuthorizeResult(pi.Id, pi.Status);
        }
        catch (StripeException ex)
        {
            log.LogWarning("stripe wallet top-up failed wallet={WalletId} code={Code}", walletId, ex.StripeError?.Code);
            throw new PaymentGatewayException("wallet top-up failed", ex.StripeError?.Code, ex);
        }
    }
}
