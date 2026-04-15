using MassTransit;
using Microsoft.Extensions.Logging;
using TBE.Contracts.Commands;
using TBE.Contracts.Events;
using TBE.PaymentService.Application.Stripe;

namespace TBE.PaymentService.Application.Consumers;

/// <summary>
/// Consumes <see cref="AuthorizePaymentCommand"/> from the saga, delegates to Stripe,
/// and publishes <c>PaymentAuthorized</c> on success or <c>PaymentAuthorizationFailed</c>
/// on gateway exception.
/// </summary>
public sealed class AuthorizePaymentConsumer(
    IStripePaymentGateway gateway,
    ILogger<AuthorizePaymentConsumer> log)
    : IConsumer<AuthorizePaymentCommand>
{
    public async Task Consume(ConsumeContext<AuthorizePaymentCommand> ctx)
    {
        var msg = ctx.Message;
        try
        {
            var result = await gateway.AuthorizeAsync(
                msg.BookingId, msg.AmountCents, msg.Currency,
                msg.StripeCustomerId, msg.PaymentMethodId, ctx.CancellationToken);

            await ctx.Publish(new PaymentAuthorized(msg.BookingId, result.PaymentIntentId, DateTimeOffset.UtcNow));
        }
        catch (PaymentGatewayException ex)
        {
            log.LogWarning("authorize failed booking={BookingId}", msg.BookingId);
            await ctx.Publish(new PaymentAuthorizationFailed(msg.BookingId, ex.Message));
        }
    }
}
