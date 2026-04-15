using MassTransit;
using Microsoft.Extensions.Logging;
using TBE.Contracts.Events;

namespace TBE.PaymentService.Application.Consumers;

/// <summary>
/// W3 boundary: SOLE publisher of saga-consumed payment events
/// (<c>PaymentAuthorized</c>, <c>PaymentCaptured</c>, <c>PaymentAuthorizationFailed</c>)
/// in response to a Stripe signal. Filters out top-up envelopes (those are handled
/// by <c>StripeTopUpConsumer</c>).
/// </summary>
public sealed class StripeWebhookConsumer(
    ILogger<StripeWebhookConsumer> log)
    : IConsumer<StripeWebhookReceived>
{
    public async Task Consume(ConsumeContext<StripeWebhookReceived> ctx)
    {
        var e = ctx.Message;

        // Top-up envelopes belong to StripeTopUpConsumer.
        if (e.WalletId is not null)
        {
            return;
        }

        if (e.BookingId is not { } bookingId)
        {
            log.LogInformation("webhook envelope has no booking_id; event {EventId} type {EventType} ignored",
                e.EventId, e.EventType);
            return;
        }

        switch (e.EventType)
        {
            case "payment_intent.amount_capturable_updated":
                await ctx.Publish(new PaymentAuthorized(bookingId, e.PaymentIntentId ?? string.Empty, e.At));
                break;
            case "payment_intent.succeeded":
                await ctx.Publish(new PaymentCaptured(bookingId, e.PaymentIntentId ?? string.Empty, e.At));
                break;
            case "payment_intent.payment_failed":
                await ctx.Publish(new PaymentAuthorizationFailed(bookingId, "stripe payment_intent.payment_failed"));
                break;
            case "payment_intent.canceled":
                log.LogInformation("stripe payment_intent.canceled booking={BookingId} pi={PaymentIntentId}",
                    bookingId, e.PaymentIntentId);
                break;
            default:
                log.LogInformation("unhandled stripe event {EventType} for booking {BookingId}",
                    e.EventType, bookingId);
                break;
        }
    }
}
