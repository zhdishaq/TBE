using MassTransit;
using Microsoft.Extensions.Logging;
using TBE.Contracts.Commands;
using TBE.Contracts.Events;
using TBE.PaymentService.Application.Wallet;

namespace TBE.PaymentService.Application.Consumers;

public sealed class WalletReleaseConsumer(
    IWalletRepository wallet,
    ILogger<WalletReleaseConsumer> log)
    : IConsumer<WalletReleaseCommand>
{
    public async Task Consume(ConsumeContext<WalletReleaseCommand> ctx)
    {
        var msg = ctx.Message;
        await wallet.ReleaseAsync(msg.WalletId, msg.BookingId, msg.ReservationTxId, ctx.CancellationToken);
        await ctx.Publish(new WalletReleased(msg.BookingId, msg.WalletId, msg.ReservationTxId, DateTimeOffset.UtcNow));
        log.LogInformation("wallet release ok wallet={WalletId} booking={BookingId}", msg.WalletId, msg.BookingId);
    }
}
