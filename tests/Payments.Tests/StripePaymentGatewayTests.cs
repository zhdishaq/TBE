using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using NSubstitute;
using Stripe;
using TBE.PaymentService.Application.Stripe;
using Xunit;

namespace Payments.Tests;

/// <summary>
/// Verifies deterministic idempotency keys (D-13) and PCI-safe request shape for the
/// Stripe gateway adapter. Uses the Stripe.net request-options interceptor surface via
/// a substituted <see cref="PaymentIntentService"/> / <see cref="RefundService"/>.
/// </summary>
public class StripePaymentGatewayTests
{
    private static IOptionsMonitor<StripeOptions> Opts()
    {
        var monitor = Substitute.For<IOptionsMonitor<StripeOptions>>();
        monitor.CurrentValue.Returns(new StripeOptions
        {
            ApiKey = "sk_test_dummy",
            WebhookSecret = "whsec_dummy",
            DefaultCurrency = "GBP"
        });
        return monitor;
    }

    [Fact(DisplayName = "PAY01: Authorize builds manual-capture intent with booking idempotency key")]
    [Trait("Category", "Unit")]
    public async Task PAY01_Authorize_builds_manual_capture_intent()
    {
        var bookingId = Guid.NewGuid();
        PaymentIntentCreateOptions? captured = null;
        RequestOptions? capturedReq = null;
        var intents = Substitute.For<PaymentIntentService>();
        intents.CreateAsync(Arg.Any<PaymentIntentCreateOptions>(), Arg.Any<RequestOptions>(), Arg.Any<CancellationToken>())
               .Returns(call =>
               {
                   captured = call.ArgAt<PaymentIntentCreateOptions>(0);
                   capturedReq = call.ArgAt<RequestOptions>(1);
                   return Task.FromResult(new PaymentIntent { Id = "pi_test", Status = "requires_capture" });
               });

        var gw = new StripePaymentGateway(Opts(), NullLogger<StripePaymentGateway>.Instance, intents, null);
        var result = await gw.AuthorizeAsync(bookingId, 500_00m, "gbp", "cus_1", "pm_1", CancellationToken.None);

        result.PaymentIntentId.Should().Be("pi_test");
        captured!.CaptureMethod.Should().Be("manual");
        captured.Amount.Should().Be(50000);
        captured.Metadata.Should().ContainKey("booking_id").WhoseValue.Should().Be(bookingId.ToString());
        capturedReq!.IdempotencyKey.Should().Be($"booking-{bookingId}-authorize");
    }

    [Fact(DisplayName = "PAY02: Capture uses deterministic idempotency key")]
    [Trait("Category", "Unit")]
    public async Task PAY02_Capture_uses_deterministic_idempotency_key()
    {
        var bookingId = Guid.NewGuid();
        RequestOptions? capturedReq = null;
        var intents = Substitute.For<PaymentIntentService>();
        intents.CaptureAsync(
                Arg.Any<string>(),
                Arg.Any<PaymentIntentCaptureOptions>(),
                Arg.Any<RequestOptions>(),
                Arg.Any<CancellationToken>())
            .Returns(call =>
            {
                capturedReq = call.ArgAt<RequestOptions>(2);
                return Task.FromResult(new PaymentIntent { Id = "pi_x", Status = "succeeded" });
            });

        var gw = new StripePaymentGateway(Opts(), NullLogger<StripePaymentGateway>.Instance, intents, null);
        await gw.CaptureAsync(bookingId, "pi_x", 500_00m, CancellationToken.None);

        capturedReq!.IdempotencyKey.Should().Be($"booking-{bookingId}-capture");
    }

    [Fact(DisplayName = "PAY07: Refund uses deterministic idempotency key")]
    [Trait("Category", "Unit")]
    public async Task PAY07_Refund_uses_deterministic_idempotency_key()
    {
        var bookingId = Guid.NewGuid();
        RequestOptions? capturedReq = null;
        var refunds = Substitute.For<RefundService>();
        refunds.CreateAsync(Arg.Any<RefundCreateOptions>(), Arg.Any<RequestOptions>(), Arg.Any<CancellationToken>())
               .Returns(call =>
               {
                   capturedReq = call.ArgAt<RequestOptions>(1);
                   return Task.FromResult(new Refund { Id = "re_123", Status = "succeeded" });
               });

        var gw = new StripePaymentGateway(Opts(), NullLogger<StripePaymentGateway>.Instance, null, refunds);
        var id = await gw.RefundAsync(bookingId, "pi_x", 100_00m, CancellationToken.None);

        id.Should().Be("re_123");
        capturedReq!.IdempotencyKey.Should().Be($"booking-{bookingId}-refund");
    }

    [Fact(DisplayName = "PAY04: TopUp sets wallet metadata and wallet idempotency key")]
    [Trait("Category", "Unit")]
    public async Task PAY04_TopUp_sets_wallet_metadata()
    {
        var walletId = Guid.NewGuid();
        var agencyId = Guid.NewGuid();
        PaymentIntentCreateOptions? captured = null;
        RequestOptions? capturedReq = null;
        var intents = Substitute.For<PaymentIntentService>();
        intents.CreateAsync(Arg.Any<PaymentIntentCreateOptions>(), Arg.Any<RequestOptions>(), Arg.Any<CancellationToken>())
               .Returns(call =>
               {
                   captured = call.ArgAt<PaymentIntentCreateOptions>(0);
                   capturedReq = call.ArgAt<RequestOptions>(1);
                   return Task.FromResult(new PaymentIntent { Id = "pi_top", Status = "succeeded" });
               });

        var gw = new StripePaymentGateway(Opts(), NullLogger<StripePaymentGateway>.Instance, intents, null);
        var r = await gw.CreateWalletTopUpAsync(walletId, agencyId, 1000_00m, "gbp", "cus_1", "pm_1", CancellationToken.None);

        r.PaymentIntentId.Should().Be("pi_top");
        captured!.CaptureMethod.Should().Be("automatic");
        captured.Metadata.Should().ContainKey("wallet_id").WhoseValue.Should().Be(walletId.ToString());
        captured.Metadata.Should().ContainKey("agency_id").WhoseValue.Should().Be(agencyId.ToString());
        captured.Metadata.Should().ContainKey("topup_amount").WhoseValue.Should().Be("100000");
        capturedReq!.IdempotencyKey.Should().Be($"wallet-{walletId}-topup-authorize");
    }
}
