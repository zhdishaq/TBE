using MassTransit;
using Microsoft.Extensions.Logging;
using TBE.Contracts.Commands;
using TBE.Contracts.Events;
using TBE.PaymentService.Application.Stripe;

namespace TBE.PaymentService.Application.Consumers;

/// <summary>
/// Consumes <see cref="CapturePaymentCommand"/> from the saga's Capturing state and
/// delegates to <see cref="IStripePaymentGateway.CaptureAsync"/>. The actual Stripe
/// <c>.CaptureAsync</c> call lives only in <c>StripePaymentGateway.Capture</c> (Pitfall 1).
/// </summary>
public sealed class CapturePaymentConsumer(
    IStripePaymentGateway gateway,
    ILogger<CapturePaymentConsumer> log)
    : IConsumer<CapturePaymentCommand>
{
    public async Task Consume(ConsumeContext<CapturePaymentCommand> ctx)
    {
        var msg = ctx.Message;
        try
        {
            var result = await gateway.CaptureAsync(
                msg.BookingId, msg.PaymentIntentId, msg.AmountCents, ctx.CancellationToken);

            await ctx.Publish(new PaymentCaptured(msg.BookingId, result.PaymentIntentId, DateTimeOffset.UtcNow));
        }
        catch (PaymentGatewayException ex)
        {
            log.LogWarning("capture failed booking={BookingId} pi={PaymentIntentId}",
                msg.BookingId, msg.PaymentIntentId);
            await ctx.Publish(new PaymentCaptureFailed(msg.BookingId, ex.Message));
        }
    }
}
