using MassTransit;
using Microsoft.Extensions.Logging;
using TBE.Contracts.Commands;
using TBE.PaymentService.Application.Stripe;

namespace TBE.PaymentService.Application.Consumers;

/// <summary>
/// Consumes <see cref="CancelAuthorizationCommand"/> (saga compensation for an
/// authorized-but-not-captured PaymentIntent).
/// </summary>
public sealed class CancelAuthorizationConsumer(
    IStripePaymentGateway gateway,
    ILogger<CancelAuthorizationConsumer> log)
    : IConsumer<CancelAuthorizationCommand>
{
    public async Task Consume(ConsumeContext<CancelAuthorizationCommand> ctx)
    {
        var msg = ctx.Message;
        try
        {
            await gateway.CancelAsync(msg.BookingId, msg.PaymentIntentId, ctx.CancellationToken);
            log.LogInformation("authorization cancelled booking={BookingId} pi={PaymentIntentId}",
                msg.BookingId, msg.PaymentIntentId);
        }
        catch (PaymentGatewayException ex)
        {
            // Log + swallow — compensation path; saga will already be in Failed state.
            log.LogWarning("cancel-auth gateway error booking={BookingId} pi={PaymentIntentId} cause={Cause}",
                msg.BookingId, msg.PaymentIntentId, ex.Message);
        }
    }
}
