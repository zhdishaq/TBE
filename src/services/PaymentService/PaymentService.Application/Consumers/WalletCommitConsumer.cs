using MassTransit;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using TBE.Contracts.Commands;
using TBE.Contracts.Events;
using TBE.PaymentService.Application.Wallet;

namespace TBE.PaymentService.Application.Consumers;

public sealed class WalletCommitConsumer(
    IWalletRepository wallet,
    IOptionsMonitor<WalletOptions> opts,
    ILogger<WalletCommitConsumer> log)
    : IConsumer<WalletCommitCommand>
{
    public async Task Consume(ConsumeContext<WalletCommitCommand> ctx)
    {
        var msg = ctx.Message;
        await wallet.CommitAsync(msg.WalletId, msg.BookingId, msg.ReservationTxId, ctx.CancellationToken);
        await ctx.Publish(new WalletCommitted(msg.BookingId, msg.WalletId, msg.ReservationTxId, DateTimeOffset.UtcNow));

        var balance = await wallet.GetBalanceAsync(msg.WalletId, ctx.CancellationToken);
        if (balance < opts.CurrentValue.LowBalanceThreshold)
        {
            await ctx.Publish(new WalletLowBalance(msg.WalletId, balance, opts.CurrentValue.LowBalanceThreshold, DateTimeOffset.UtcNow));
        }
        log.LogInformation("wallet commit ok wallet={WalletId} booking={BookingId}", msg.WalletId, msg.BookingId);
    }
}
