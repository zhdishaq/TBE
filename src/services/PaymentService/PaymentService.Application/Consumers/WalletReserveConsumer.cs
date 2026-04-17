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
            // Plan 05-02 Task 2 — contract now carries CorrelationId + AgencyId +
            // IdempotencyKey per D-40 / T-05-02-04. WalletId is retained
            // alongside AgencyId (Phase-3 compat with IWalletRepository.ReserveAsync
            // which is keyed by wallet id). Future Plan 05-03 wires agent-admin
            // top-up against the same AgencyId; resolver stays in this consumer.
            var txId = await wallet.ReserveAsync(msg.WalletId, msg.BookingId, msg.Amount, msg.Currency, ctx.CancellationToken);
            var balance = await wallet.GetBalanceAsync(msg.WalletId, ctx.CancellationToken);
            await ctx.Publish(new WalletReserved(msg.CorrelationId, msg.BookingId, txId, balance));

            if (balance < opts.CurrentValue.LowBalanceThreshold)
            {
                await ctx.Publish(new WalletLowBalance(msg.WalletId, balance, opts.CurrentValue.LowBalanceThreshold, DateTimeOffset.UtcNow));
            }
        }
        catch (InsufficientWalletBalanceException ex)
        {
            log.LogInformation("wallet reserve rejected wallet={WalletId} attempted={Amount} available={Available}",
                msg.WalletId, ex.AttemptedAmount, ex.AvailableBalance);
            await ctx.Publish(new WalletReserveFailed(msg.CorrelationId, msg.BookingId, "insufficient_funds"));
        }
    }
}
