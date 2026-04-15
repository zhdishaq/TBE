using MassTransit;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using TBE.Contracts.Commands;
using TBE.Contracts.Events;
using TBE.PaymentService.Application.Wallet;

namespace TBE.PaymentService.Application.Consumers;

public sealed class WalletReserveConsumer(
    IWalletRepository wallet,
    IOptionsMonitor<WalletOptions> opts,
    ILogger<WalletReserveConsumer> log)
    : IConsumer<WalletReserveCommand>
{
    public async Task Consume(ConsumeContext<WalletReserveCommand> ctx)
    {
        var msg = ctx.Message;
        try
        {
            var txId = await wallet.ReserveAsync(msg.WalletId, msg.BookingId, msg.Amount, msg.Currency, ctx.CancellationToken);
            await ctx.Publish(new WalletReserved(msg.BookingId, msg.WalletId, txId, msg.Amount, DateTimeOffset.UtcNow));

            var balance = await wallet.GetBalanceAsync(msg.WalletId, ctx.CancellationToken);
            if (balance < opts.CurrentValue.LowBalanceThreshold)
            {
                await ctx.Publish(new WalletLowBalance(msg.WalletId, balance, opts.CurrentValue.LowBalanceThreshold, DateTimeOffset.UtcNow));
            }
        }
        catch (InsufficientWalletBalanceException ex)
        {
            log.LogInformation("wallet reserve rejected wallet={WalletId} attempted={Amount} available={Available}",
                msg.WalletId, ex.AttemptedAmount, ex.AvailableBalance);
            await ctx.Publish(new WalletReservationFailed(
                msg.BookingId, msg.WalletId, "insufficient_balance", ex.AttemptedAmount, ex.AvailableBalance));
        }
    }
}
