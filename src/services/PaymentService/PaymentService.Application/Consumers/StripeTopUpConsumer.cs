using MassTransit;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using TBE.Contracts.Events;
using TBE.PaymentService.Application.Stripe;
using TBE.PaymentService.Application.Wallet;

namespace TBE.PaymentService.Application.Consumers;

/// <summary>
/// W3 boundary: SOLE writer of <c>TopUp</c> ledger entries in response to a Stripe signal.
/// Filters <see cref="StripeWebhookReceived"/> to envelopes where <c>WalletId</c> is set AND
/// <c>EventType</c> ∈ {charge.succeeded, payment_intent.succeeded}, then writes a TopUp row
/// with idempotency key <c>wallet-{walletId}-topup-{paymentIntentId}</c>.
/// </summary>
public sealed class StripeTopUpConsumer(
    IWalletRepository wallet,
    IOptionsMonitor<StripeOptions> stripeOpts,
    ILogger<StripeTopUpConsumer> log)
    : IConsumer<StripeWebhookReceived>
{
    private static readonly HashSet<string> TopUpEventTypes = new(StringComparer.Ordinal)
    {
        "charge.succeeded",
        "payment_intent.succeeded"
    };

    public async Task Consume(ConsumeContext<StripeWebhookReceived> ctx)
    {
        var e = ctx.Message;
        if (e.WalletId is not { } walletId) return;                     // saga-path envelope
        if (!TopUpEventTypes.Contains(e.EventType)) return;             // not a success event

        if (e.TopUpAmount is not { } amountCents || amountCents <= 0)
        {
            log.LogWarning("top-up envelope for wallet {WalletId} missing topup_amount; event {EventId}",
                walletId, e.EventId);
            return;
        }
        if (string.IsNullOrWhiteSpace(e.PaymentIntentId))
        {
            log.LogWarning("top-up envelope for wallet {WalletId} missing PaymentIntentId; event {EventId}",
                walletId, e.EventId);
            return;
        }

        var idemKey = $"wallet-{walletId}-topup-{e.PaymentIntentId}";
        var currency = stripeOpts.CurrentValue.DefaultCurrency;
        var amount = amountCents / 100m;

        try
        {
            var txId = await wallet.TopUpAsync(walletId, amount, currency, idemKey, ctx.CancellationToken);
            await ctx.Publish(new WalletToppedUp(walletId, e.AgencyId, amount, currency, e.PaymentIntentId!, DateTimeOffset.UtcNow));
            log.LogInformation("wallet {WalletId} topped up {Amount} {Currency} tx={TxId}", walletId, amount, currency, txId);
        }
        catch (DuplicateWalletTopUpException)
        {
            log.LogInformation("wallet top-up replay suppressed idemKey={IdemKey}", idemKey);
        }
        catch (Exception ex) when (IsUniqueViolation(ex))
        {
            log.LogInformation("wallet top-up replay suppressed idemKey={IdemKey}", idemKey);
        }
    }

    private static bool IsUniqueViolation(Exception ex)
        => ex is SqlException sql && (sql.Number is 2601 or 2627)
           || ex.InnerException is SqlException inner && (inner.Number is 2601 or 2627);
}
