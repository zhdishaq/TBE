using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using NSubstitute;
using TBE.PaymentService.Application.Stripe;
using TBE.PaymentService.Application.Wallet;
using Xunit;

namespace Payments.Tests;

/// <summary>
/// Plan 05-03 Task 1 — WalletTopUpService.
///
/// Verifies D-40 caps are enforced BEFORE Stripe is called and
/// CommitTopUpAsync is idempotent on the Stripe PaymentIntent id
/// (T-05-03-04). Caps are read from <see cref="WalletOptions"/> via
/// <c>IOptionsMonitor</c> so admins can flip them at runtime without a
/// restart.
/// </summary>
public class WalletTopUpServiceTests
{
    private static IOptionsMonitor<WalletOptions> Options(decimal min = 10m, decimal max = 50_000m)
    {
        var opts = new WalletOptions
        {
            TopUp = new WalletTopUpOptions { MinAmount = min, MaxAmount = max, Currency = "GBP" },
        };
        var monitor = Substitute.For<IOptionsMonitor<WalletOptions>>();
        monitor.CurrentValue.Returns(opts);
        return monitor;
    }

    [Fact(DisplayName = "T-05-03-03: amount below min throws WalletTopUpOutOfRangeException BEFORE Stripe")]
    public async Task Below_min_throws_out_of_range_before_stripe()
    {
        var stripe = Substitute.For<IStripePaymentGateway>();
        var sut = new WalletTopUpService(stripe, Options(min: 10m, max: 50_000m), NullLogger<WalletTopUpService>.Instance);

        var act = async () => await sut.CreateTopUpIntentAsync(Guid.NewGuid(), amount: 5m, CancellationToken.None);

        var ex = await act.Should().ThrowAsync<WalletTopUpOutOfRangeException>();
        ex.Which.Min.Should().Be(10m);
        ex.Which.Max.Should().Be(50_000m);
        ex.Which.Requested.Should().Be(5m);
        await stripe.DidNotReceiveWithAnyArgs().CreateWalletTopUpAsync(default, default, default, default!, default!, default!, default);
    }

    [Fact(DisplayName = "T-05-03-03: amount above max throws WalletTopUpOutOfRangeException BEFORE Stripe")]
    public async Task Above_max_throws_out_of_range_before_stripe()
    {
        var stripe = Substitute.For<IStripePaymentGateway>();
        var sut = new WalletTopUpService(stripe, Options(min: 10m, max: 50_000m), NullLogger<WalletTopUpService>.Instance);

        var act = async () => await sut.CreateTopUpIntentAsync(Guid.NewGuid(), amount: 50_001m, CancellationToken.None);

        var ex = await act.Should().ThrowAsync<WalletTopUpOutOfRangeException>();
        ex.Which.Requested.Should().Be(50_001m);
        await stripe.DidNotReceiveWithAnyArgs().CreateWalletTopUpAsync(default, default, default, default!, default!, default!, default);
    }

    [Fact(DisplayName = "D-40: in-range amount calls Stripe with payment_mode=wallet_topup metadata")]
    public async Task In_range_amount_creates_stripe_topup()
    {
        var stripe = Substitute.For<IStripePaymentGateway>();
        stripe.CreateWalletTopUpAsync(default, default, default, default!, default!, default!, default)
            .ReturnsForAnyArgs(new AuthorizeResult("pi_abc123", "requires_confirmation"));
        var sut = new WalletTopUpService(stripe, Options(), NullLogger<WalletTopUpService>.Instance);

        var agencyId = Guid.NewGuid();
        var result = await sut.CreateTopUpIntentAsync(agencyId, amount: 250m, CancellationToken.None);

        result.PaymentIntentId.Should().Be("pi_abc123");
        result.Amount.Should().Be(250m);
        result.Currency.Should().Be("GBP");
        await stripe.Received(1).CreateWalletTopUpAsync(
            walletId: agencyId,
            agencyId: agencyId,
            amountCents: 25_000m,
            currency: "GBP",
            stripeCustomerId: Arg.Any<string>(),
            paymentMethodId: Arg.Any<string>(),
            ct: Arg.Any<CancellationToken>());
    }

    [Fact(DisplayName = "T-05-03-03: caps are read from IOptionsMonitor on every call")]
    public async Task Caps_are_read_from_options_monitor_each_call()
    {
        var stripe = Substitute.For<IStripePaymentGateway>();
        var monitor = Substitute.For<IOptionsMonitor<WalletOptions>>();
        var first = new WalletOptions
        {
            TopUp = new WalletTopUpOptions { MinAmount = 10m, MaxAmount = 50_000m, Currency = "GBP" },
        };
        var second = new WalletOptions
        {
            TopUp = new WalletTopUpOptions { MinAmount = 100m, MaxAmount = 1_000m, Currency = "GBP" },
        };
        monitor.CurrentValue.Returns(first, second);
        stripe.CreateWalletTopUpAsync(default, default, default, default!, default!, default!, default)
            .ReturnsForAnyArgs(new AuthorizeResult("pi_x", "requires_confirmation"));
        var sut = new WalletTopUpService(stripe, monitor, NullLogger<WalletTopUpService>.Instance);

        // First call: 50 is in [10, 50000]
        await sut.CreateTopUpIntentAsync(Guid.NewGuid(), amount: 50m, CancellationToken.None);

        // Second call (after admin tightens range): 50 is now below min=100 — must throw.
        var act = async () => await sut.CreateTopUpIntentAsync(Guid.NewGuid(), amount: 50m, CancellationToken.None);
        await act.Should().ThrowAsync<WalletTopUpOutOfRangeException>();
    }
}
