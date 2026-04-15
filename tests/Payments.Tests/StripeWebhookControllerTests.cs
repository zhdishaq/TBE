using System.Security.Cryptography;
using System.Text;
using FluentAssertions;
using MassTransit;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using NSubstitute;
using TBE.Contracts.Events;
using TBE.PaymentService.API.Controllers;
using TBE.PaymentService.Application.Stripe;
using TBE.PaymentService.Infrastructure;
using Xunit;

namespace Payments.Tests;

public class StripeWebhookControllerTests
{
    private const string Secret = "whsec_test_0123456789abcdef";

    private static IOptionsMonitor<StripeOptions> Opts()
    {
        var monitor = Substitute.For<IOptionsMonitor<StripeOptions>>();
        monitor.CurrentValue.Returns(new StripeOptions { WebhookSecret = Secret });
        return monitor;
    }

    private static PaymentDbContext NewDb()
    {
        var options = new DbContextOptionsBuilder<PaymentDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .ConfigureWarnings(w => w.Ignore(Microsoft.EntityFrameworkCore.Diagnostics.InMemoryEventId.TransactionIgnoredWarning))
            .Options;
        return new PaymentDbContext(options);
    }

    private static string SignPayload(string payload, long timestamp)
    {
        var signed = $"{timestamp}.{payload}";
        using var hmac = new HMACSHA256(Encoding.UTF8.GetBytes(Secret));
        var hash = hmac.ComputeHash(Encoding.UTF8.GetBytes(signed));
        var hex = Convert.ToHexString(hash).ToLowerInvariant();
        return $"t={timestamp},v1={hex}";
    }

    private static StripeWebhookController BuildController(
        PaymentDbContext db, IPublishEndpoint pub, ILoggerSpy<StripeWebhookController>? log = null)
    {
        var actualLog = (Microsoft.Extensions.Logging.ILogger<StripeWebhookController>?)log
            ?? NullLogger<StripeWebhookController>.Instance;
        var ctrl = new StripeWebhookController(Opts(), db, pub, actualLog);
        ctrl.ControllerContext = new Microsoft.AspNetCore.Mvc.ControllerContext
        {
            HttpContext = new DefaultHttpContext()
        };
        return ctrl;
    }

    private static void SetRequestBody(StripeWebhookController ctrl, string json, string signature)
    {
        ctrl.ControllerContext.HttpContext.Request.Body = new MemoryStream(Encoding.UTF8.GetBytes(json));
        ctrl.ControllerContext.HttpContext.Request.Headers["Stripe-Signature"] = signature;
    }

    private static string BuildPaymentIntentSucceededJson(string eventId, Guid bookingId, string pi)
        => $$"""
        {
          "id": "{{eventId}}",
          "object": "event",
          "api_version": "2020-08-27",
          "type": "payment_intent.succeeded",
          "data": {
            "object": {
              "id": "{{pi}}",
              "object": "payment_intent",
              "metadata": { "booking_id": "{{bookingId}}" }
            }
          }
        }
        """;

    [Fact(DisplayName = "PAY02: webhook with invalid signature returns 400")]
    [Trait("Category", "Unit")]
    public async Task PAY02_webhook_with_invalid_signature_returns_400()
    {
        await using var db = NewDb();
        var pub = Substitute.For<IPublishEndpoint>();
        var ctrl = BuildController(db, pub);

        SetRequestBody(ctrl, "{\"id\":\"evt_x\"}", "t=1234,v1=deadbeef");
        var result = await ctrl.HandleAsync(CancellationToken.None);

        result.Should().BeOfType<Microsoft.AspNetCore.Mvc.BadRequestResult>();
        await pub.DidNotReceiveWithAnyArgs().Publish<StripeWebhookReceived>(default!, default);
    }

    [Fact(DisplayName = "PAY02: replayed event.Id returns 200 without republishing")]
    [Trait("Category", "Unit")]
    public async Task PAY02_webhook_with_replayed_event_id_returns_200_without_republish()
    {
        var eventId = "evt_replay";
        var bookingId = Guid.NewGuid();
        await using var db = NewDb();
        db.StripeWebhookEvents.Add(new TBE.PaymentService.Infrastructure.Stripe.StripeWebhookEvent
        {
            EventId = eventId, EventType = "payment_intent.succeeded", ReceivedAtUtc = DateTime.UtcNow
        });
        await db.SaveChangesAsync();

        var pub = Substitute.For<IPublishEndpoint>();
        var ctrl = BuildController(db, pub);

        var json = BuildPaymentIntentSucceededJson(eventId, bookingId, "pi_x");
        var ts = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
        SetRequestBody(ctrl, json, SignPayload(json, ts));

        var result = await ctrl.HandleAsync(CancellationToken.None);

        result.Should().BeOfType<Microsoft.AspNetCore.Mvc.OkResult>();
        await pub.DidNotReceiveWithAnyArgs().Publish<StripeWebhookReceived>(default!, default);
    }

    [Fact(DisplayName = "PAY02: webhook publishes only StripeWebhookReceived envelope")]
    [Trait("Category", "Unit")]
    public async Task PAY02_webhook_publishes_only_StripeWebhookReceived()
    {
        var eventId = "evt_ok_" + Guid.NewGuid().ToString("N");
        var bookingId = Guid.NewGuid();
        await using var db = NewDb();
        var pub = Substitute.For<IPublishEndpoint>();
        var ctrl = BuildController(db, pub);

        var json = BuildPaymentIntentSucceededJson(eventId, bookingId, "pi_abc");
        var ts = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
        SetRequestBody(ctrl, json, SignPayload(json, ts));

        var result = await ctrl.HandleAsync(CancellationToken.None);

        result.Should().BeOfType<Microsoft.AspNetCore.Mvc.OkResult>();

        await pub.Received(1).Publish(
            Arg.Is<StripeWebhookReceived>(e =>
                e.EventId == eventId &&
                e.EventType == "payment_intent.succeeded" &&
                e.BookingId == bookingId &&
                e.PaymentIntentId == "pi_abc" &&
                e.WalletId == null),
            Arg.Any<CancellationToken>());

        // No other Publish<T> calls whatsoever.
        pub.ReceivedCalls().Where(c => c.GetMethodInfo().Name == "Publish")
            .Should().HaveCount(1);
    }

    [Fact(DisplayName = "PAY02: webhook raw body never appears in logger")]
    [Trait("Category", "Unit")]
    public async Task PAY02_webhook_raw_body_never_appears_in_logger()
    {
        var eventId = "evt_log_" + Guid.NewGuid().ToString("N");
        var bookingId = Guid.NewGuid();
        await using var db = NewDb();
        var pub = Substitute.For<IPublishEndpoint>();
        var logSpy = new ILoggerSpy<StripeWebhookController>();
        var ctrl = BuildController(db, pub, logSpy);

        var json = BuildPaymentIntentSucceededJson(eventId, bookingId, "pi_abc");
        var ts = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
        var sig = SignPayload(json, ts);
        SetRequestBody(ctrl, json, sig);

        await ctrl.HandleAsync(CancellationToken.None);

        logSpy.AllMessages.Should().NotContain(m => m.Contains("booking_id", StringComparison.OrdinalIgnoreCase));
        logSpy.AllMessages.Should().NotContain(m => m.Contains(sig, StringComparison.OrdinalIgnoreCase));
        logSpy.AllMessages.Should().NotContain(m => m.Contains("metadata", StringComparison.OrdinalIgnoreCase));
    }
}

/// <summary>Captures log messages for assertion.</summary>
public sealed class ILoggerSpy<T> : Microsoft.Extensions.Logging.ILogger<T>
{
    public List<string> AllMessages { get; } = new();

    public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;
    public bool IsEnabled(Microsoft.Extensions.Logging.LogLevel logLevel) => true;

    public void Log<TState>(
        Microsoft.Extensions.Logging.LogLevel logLevel,
        Microsoft.Extensions.Logging.EventId eventId,
        TState state,
        Exception? exception,
        Func<TState, Exception?, string> formatter)
    {
        AllMessages.Add(formatter(state, exception));
    }
}
