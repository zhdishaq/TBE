using FluentAssertions;
using MassTransit;
using MassTransit.Testing;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using NSubstitute;
using NSubstitute.ExceptionExtensions;
using TBE.Contracts.Events;
using TBE.PaymentService.Application.Consumers;
using TBE.PaymentService.Application.Stripe;
using TBE.PaymentService.Application.Wallet;
using Xunit;

namespace Payments.Tests;

/// <summary>
/// Behavior tests for <see cref="StripeTopUpConsumer"/> — the SOLE writer of TopUp ledger
/// entries driven by Stripe webhook envelopes. Uses MassTransit's in-memory test harness
/// so the consumer runs inside a real <see cref="ConsumeContext{T}"/>, while the wallet
/// repository is substituted via NSubstitute.
/// </summary>
[Trait("Category", "Unit")]
public class StripeTopUpConsumerTests
{
    private static IOptionsMonitor<StripeOptions> Opts()
    {
        var m = Substitute.For<IOptionsMonitor<StripeOptions>>();
        m.CurrentValue.Returns(new StripeOptions { DefaultCurrency = "GBP" });
        return m;
    }

    private static async Task<(InMemoryTestHarness harness, IWalletRepository wallet)> StartHarnessAsync()
    {
        var wallet = Substitute.For<IWalletRepository>();
        var harness = new InMemoryTestHarness();
        harness.Consumer(() => new StripeTopUpConsumer(wallet, Opts(), NullLogger<StripeTopUpConsumer>.Instance));
        await harness.Start();
        return (harness, wallet);
    }

    [Fact(DisplayName = "PAY04: top-up writes ledger entry with deterministic idempotency key")]
    public async Task PAY04_top_up_writes_ledger_entry_with_deterministic_idempotency_key()
    {
        var (harness, wallet) = await StartHarnessAsync();
        try
        {
            var walletId = Guid.NewGuid();
            var agencyId = Guid.NewGuid();
            wallet.TopUpAsync(walletId, 100m, "GBP",
                    $"wallet-{walletId}-topup-pi_top",
                    Arg.Any<CancellationToken>())
                .Returns(Guid.NewGuid());

            await harness.InputQueueSendEndpoint.Send(new StripeWebhookReceived(
                "evt_1", "payment_intent.succeeded", "pi_top", null, walletId, 10000m, agencyId,
                DateTimeOffset.UtcNow));

            (await harness.Consumed.Any<StripeWebhookReceived>()).Should().BeTrue();
            await wallet.Received(1).TopUpAsync(walletId, 100m, "GBP",
                $"wallet-{walletId}-topup-pi_top", Arg.Any<CancellationToken>());
            (await harness.Published.Any<WalletToppedUp>()).Should().BeTrue();
        }
        finally { await harness.Stop(); }
    }

    [Fact(DisplayName = "PAY04: replayed top-up webhook is idempotent (DuplicateWalletTopUpException swallowed)")]
    public async Task PAY04_replayed_top_up_webhook_is_idempotent()
    {
        var (harness, wallet) = await StartHarnessAsync();
        try
        {
            var walletId = Guid.NewGuid();
            var agencyId = Guid.NewGuid();
            wallet.TopUpAsync(walletId, 50m, "GBP", Arg.Any<string>(), Arg.Any<CancellationToken>())
                  .Throws(new DuplicateWalletTopUpException(Guid.NewGuid(), new InvalidOperationException("dup")));

            await harness.InputQueueSendEndpoint.Send(new StripeWebhookReceived(
                "evt_replay", "charge.succeeded", "pi_replay", null, walletId, 5000m, agencyId,
                DateTimeOffset.UtcNow));

            (await harness.Consumed.Any<StripeWebhookReceived>()).Should().BeTrue();
            // no WalletToppedUp published on duplicate (consumer doesn't double-publish)
            (await harness.Published.Any<WalletToppedUp>()).Should().BeFalse();
        }
        finally { await harness.Stop(); }
    }

    [Fact(DisplayName = "PAY04: envelope without WalletId (saga-path) is ignored")]
    public async Task PAY04_non_topup_envelope_is_ignored()
    {
        var (harness, wallet) = await StartHarnessAsync();
        try
        {
            var bookingId = Guid.NewGuid();
            await harness.InputQueueSendEndpoint.Send(new StripeWebhookReceived(
                "evt_saga", "payment_intent.succeeded", "pi_saga", bookingId, null, null, null,
                DateTimeOffset.UtcNow));

            (await harness.Consumed.Any<StripeWebhookReceived>()).Should().BeTrue();
            await wallet.DidNotReceiveWithAnyArgs().TopUpAsync(
                default, default, default!, default!, default);
            (await harness.Published.Any<WalletToppedUp>()).Should().BeFalse();
        }
        finally { await harness.Stop(); }
    }

    [Fact(DisplayName = "PAY04: non-success event type is ignored even with WalletId")]
    public async Task PAY04_non_topup_event_type_is_ignored()
    {
        var (harness, wallet) = await StartHarnessAsync();
        try
        {
            var walletId = Guid.NewGuid();
            await harness.InputQueueSendEndpoint.Send(new StripeWebhookReceived(
                "evt_failed", "payment_intent.payment_failed", "pi_fail", null, walletId, 5000m, null,
                DateTimeOffset.UtcNow));

            (await harness.Consumed.Any<StripeWebhookReceived>()).Should().BeTrue();
            await wallet.DidNotReceiveWithAnyArgs().TopUpAsync(
                default, default, default!, default!, default);
        }
        finally { await harness.Stop(); }
    }

    [Fact(DisplayName = "PAY04: envelope missing TopUpAmount logs warning and skips ledger write")]
    public async Task PAY04_missing_topup_amount_logs_warning_and_skips()
    {
        var (harness, wallet) = await StartHarnessAsync();
        try
        {
            var walletId = Guid.NewGuid();
            await harness.InputQueueSendEndpoint.Send(new StripeWebhookReceived(
                "evt_noamt", "charge.succeeded", "pi_noamt", null, walletId, null, null,
                DateTimeOffset.UtcNow));

            (await harness.Consumed.Any<StripeWebhookReceived>()).Should().BeTrue();
            await wallet.DidNotReceiveWithAnyArgs().TopUpAsync(
                default, default, default!, default!, default);
        }
        finally { await harness.Stop(); }
    }
}
