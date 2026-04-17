using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using TBE.PaymentService.Application.Stripe;

namespace TBE.PaymentService.Application.Wallet;

/// <summary>
/// Default <see cref="IWalletTopUpService"/> implementation. Enforces D-40 caps against
/// <see cref="IOptionsMonitor{T}.CurrentValue"/> (read on every call so runtime config
/// flips take effect immediately), then defers to <see cref="IStripePaymentGateway"/>
/// for the actual PaymentIntent creation. PCI data never touches this layer.
///
/// Agent-admin → <c>POST /api/wallet/top-up/intent</c> → this service → Stripe →
/// return <c>client_secret</c> → browser Stripe Elements → webhook → CommitTopUpAsync
/// (append-only ledger row). See Plan 05-03 Task 1 for the full flow.
/// </summary>
public sealed class WalletTopUpService : IWalletTopUpService
{
    private readonly IStripePaymentGateway _stripe;
    private readonly IOptionsMonitor<WalletOptions> _options;
    private readonly ILogger<WalletTopUpService> _log;
    private readonly IWalletRepository? _wallet;

    public WalletTopUpService(
        IStripePaymentGateway stripe,
        IOptionsMonitor<WalletOptions> options,
        ILogger<WalletTopUpService> log,
        IWalletRepository? wallet = null)
    {
        _stripe = stripe;
        _options = options;
        _log = log;
        _wallet = wallet;
    }

    public async Task<TopUpIntentResult> CreateTopUpIntentAsync(
        Guid agencyId,
        decimal amount,
        CancellationToken ct)
    {
        // D-40: caps are read on EVERY call, not cached, so admin config flips
        // (IOptionsMonitor CurrentValue) take effect without restart.
        var opts = _options.CurrentValue.TopUp;
        if (amount < opts.MinAmount || amount > opts.MaxAmount)
        {
            _log.LogWarning(
                "wallet top-up rejected out-of-range for agency {AgencyId}: requested {Requested} {Currency}, allowed [{Min}, {Max}]",
                agencyId, amount, opts.Currency, opts.MinAmount, opts.MaxAmount);
            throw new WalletTopUpOutOfRangeException(opts.MinAmount, opts.MaxAmount, amount, opts.Currency);
        }

        // 1:1 wallet/agency mapping — AgencyWallet.Id == AgencyId so the existing
        // WalletRepository (which is keyed on walletId) works without extra indirection.
        // Stripe.net expects minor units (pence for GBP).
        var amountCents = amount * 100m;
        var pi = await _stripe.CreateWalletTopUpAsync(
            walletId: agencyId,
            agencyId: agencyId,
            amountCents: amountCents,
            currency: opts.Currency,
            stripeCustomerId: string.Empty,
            paymentMethodId: string.Empty,
            ct: ct);

        _log.LogInformation(
            "wallet top-up PaymentIntent {Pi} created for agency {AgencyId} amount={Amount} {Currency}",
            pi.PaymentIntentId, agencyId, amount, opts.Currency);

        return new TopUpIntentResult(
            ClientSecret: pi.ClientSecret ?? string.Empty,
            PaymentIntentId: pi.PaymentIntentId,
            Amount: amount,
            Currency: opts.Currency);
    }

    public async Task CommitTopUpAsync(
        Guid agencyId,
        string stripePaymentIntentId,
        decimal amount,
        string currency,
        CancellationToken ct)
    {
        if (_wallet is null)
        {
            throw new InvalidOperationException(
                "WalletTopUpService requires an IWalletRepository to commit top-ups; check DI registration.");
        }

        // Pitfall 20: the Stripe PI id is the stable idempotency key. MassTransit
        // redelivery of the webhook MUST NOT double-credit the wallet.
        var idempotencyKey = $"stripe-topup-{stripePaymentIntentId}";
        try
        {
            await _wallet.TopUpAsync(agencyId, amount, currency, idempotencyKey, ct);
            _log.LogInformation(
                "wallet top-up committed for agency {AgencyId} PI={Pi} amount={Amount} {Currency}",
                agencyId, stripePaymentIntentId, amount, currency);
        }
        catch (DuplicateWalletTopUpException)
        {
            // Already credited by a prior webhook delivery — swallow.
            _log.LogInformation(
                "wallet top-up PI={Pi} already committed (idempotent replay)", stripePaymentIntentId);
        }
    }
}
