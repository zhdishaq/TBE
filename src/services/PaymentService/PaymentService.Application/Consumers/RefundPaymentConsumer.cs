using MassTransit;
using Microsoft.Extensions.Logging;
using TBE.Contracts.Commands;
using TBE.Contracts.Events;
using TBE.PaymentService.Application.Stripe;

namespace TBE.PaymentService.Application.Consumers;

/// <summary>
/// Consumes <see cref="RefundPaymentCommand"/> (saga compensation for a captured PaymentIntent).
/// Publishes <see cref="PaymentRefundIssued"/> on success.
/// </summary>
public sealed class RefundPaymentConsumer(
    IStripePaymentGateway gateway,
    ILogger<RefundPaymentConsumer> log)
    : IConsumer<RefundPaymentCommand>
{
    public async Task Consume(ConsumeContext<RefundPaymentCommand> ctx)
    {
        var msg = ctx.Message;
        try
        {
            var refundId = await gateway.RefundAsync(
                msg.BookingId, msg.PaymentIntentId, msg.AmountCents, ctx.CancellationToken);

            await ctx.Publish(new PaymentRefundIssued(msg.BookingId, refundId, DateTimeOffset.UtcNow));
        }
        catch (PaymentGatewayException ex)
        {
            log.LogWarning("refund gateway error booking={BookingId} pi={PaymentIntentId} cause={Cause}",
                msg.BookingId, msg.PaymentIntentId, ex.Message);
            throw;
        }
    }
}
