using FluentAssertions;
using MassTransit;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;
using TBE.BookingService.Application.Baskets;
using TBE.BookingService.Infrastructure;
using TBE.BookingService.Infrastructure.Baskets;
using TBE.Contracts.Events;
using Xunit;

namespace TBE.BookingService.Tests;

/// <summary>
/// Plan 04-04 Task 1 — behaviour tests for <see cref="BasketPaymentOrchestrator"/>.
/// Exercises the four canonical outcomes against ONE combined Stripe PaymentIntent
/// per CONTEXT D-08 / D-09 / D-10:
/// <list type="number">
///   <item>Success — FlightTicketed then HotelBookingConfirmed → partial capture then final capture.</item>
///   <item>Partial failure (D-09) — FlightTicketed then HotelBookingFailed → release remainder with AmountToCapture=0.</item>
///   <item>Hard failure — BookingFailed before TicketIssued → void.</item>
///   <item>Replay idempotency — duplicate FlightTicketed must NOT trigger a second capture.</item>
/// </list>
/// </summary>
public class BasketPaymentOrchestratorTests
{
    private static readonly Guid BasketId = Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa");
    private static readonly Guid FlightBookingId = Guid.Parse("bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbbb");
    private static readonly Guid HotelBookingId = Guid.Parse("cccccccc-cccc-cccc-cccc-cccccccccccc");
    private const string PiId = "pi_test_combined_basket";

    private static DbContextOptions<BookingDbContext> NewDbOptions() =>
        new DbContextOptionsBuilder<BookingDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;

    private static Basket SeedBasket(BookingDbContext db, bool withPi = true, bool flightCaptured = false)
    {
        var now = DateTime.UtcNow;
        var basket = new Basket
        {
            BasketId = BasketId,
            UserId = "user-owner-abc",
            FlightBookingId = FlightBookingId,
            HotelBookingId = HotelBookingId,
            StripePaymentIntentId = withPi ? PiId : null,
            Status = withPi ? "PaymentAuthorized" : "Initiated",
            TotalAmount = 706.00m,
            FlightSubtotal = 250.00m,
            HotelSubtotal = 456.00m,
            ChargedAmount = flightCaptured ? 250.00m : 0m,
            RefundedAmount = 0m,
            FlightCaptured = flightCaptured,
            HotelCaptured = false,
            Currency = "GBP",
            GuestEmail = "alice@example.com",
            GuestFullName = "Alice Example",
            CreatedUtc = now,
            UpdatedUtc = now,
        };
        db.Baskets.Add(basket);
        db.SaveChanges();
        return basket;
    }

    // -----------------------------------------------------------------------
    // Full success: partial capture (flight) then final capture (hotel)
    // -----------------------------------------------------------------------
    [Fact]
    public async Task Orchestrator_partial_captures_flight_then_finalizes_on_hotel_success()
    {
        var dbOptions = NewDbOptions();
        using (var seed = new BookingDbContext(dbOptions))
        {
            seed.Database.EnsureCreated();
            SeedBasket(seed);
        }

        var payments = Substitute.For<IBasketPaymentGateway>();
        payments.CapturePartialAsync(
                Arg.Any<string>(), Arg.Any<long>(), Arg.Any<bool>(), Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(new BasketCaptureResult(PiId, 25000, "succeeded"));

        // Use a direct orchestrator invocation rather than the MT harness so we can
        // assert the exact order + idempotency-key arguments without timing races.
        await using var db = new BookingDbContext(dbOptions);
        var sut = new BasketPaymentOrchestrator(db, payments, NullLogger<BasketPaymentOrchestrator>.Instance);

        await sut.Consume(MakeContext(new TicketIssued(FlightBookingId, "TK123", DateTimeOffset.UtcNow)));
        await sut.Consume(MakeContext(new HotelBookingConfirmed(
            HotelBookingId, Guid.NewGuid(), "HB-1", "SUP-ABC", "The Grand", "1 Example St",
            new DateOnly(2026, 5, 1), new DateOnly(2026, 5, 4), 1, 2, 0,
            456.00m, "GBP", "alice@example.com", "Alice Example", DateTimeOffset.UtcNow)));

        Received.InOrder(() =>
        {
            payments.CapturePartialAsync(PiId, 25000, false, $"basket-{BasketId}-capture-flight", Arg.Any<CancellationToken>());
            payments.CapturePartialAsync(PiId, 45600, true, $"basket-{BasketId}-capture-hotel", Arg.Any<CancellationToken>());
        });
        await payments.DidNotReceive().AuthorizeBasketAsync(
            Arg.Any<Guid>(), Arg.Any<decimal>(), Arg.Any<string>(), Arg.Any<string>(), Arg.Any<CancellationToken>());

        var persisted = await db.Baskets.AsNoTracking().FirstAsync(b => b.BasketId == BasketId);
        persisted.FlightCaptured.Should().BeTrue();
        persisted.HotelCaptured.Should().BeTrue();
        persisted.Status.Should().Be("Confirmed");
        persisted.ChargedAmount.Should().Be(706.00m);
    }

    // -----------------------------------------------------------------------
    // D-09 — flight captured then hotel failed → release remainder with final_capture=true, amount=0
    // -----------------------------------------------------------------------
    [Fact]
    public async Task Orchestrator_releases_remainder_on_partial_failure()
    {
        var dbOptions = NewDbOptions();
        using (var seed = new BookingDbContext(dbOptions))
        {
            seed.Database.EnsureCreated();
            SeedBasket(seed);
        }

        var payments = Substitute.For<IBasketPaymentGateway>();
        payments.CapturePartialAsync(
                Arg.Any<string>(), Arg.Any<long>(), Arg.Any<bool>(), Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(new BasketCaptureResult(PiId, 0, "succeeded"));

        await using var db = new BookingDbContext(dbOptions);
        var sut = new BasketPaymentOrchestrator(db, payments, NullLogger<BasketPaymentOrchestrator>.Instance);

        await sut.Consume(MakeContext(new TicketIssued(FlightBookingId, "TK123", DateTimeOffset.UtcNow)));
        await sut.Consume(MakeContext(new HotelBookingFailed(HotelBookingId, "inventory gone", DateTimeOffset.UtcNow)));

        Received.InOrder(() =>
        {
            payments.CapturePartialAsync(PiId, 25000, false, $"basket-{BasketId}-capture-flight", Arg.Any<CancellationToken>());
            payments.CapturePartialAsync(PiId, 0, true, $"basket-{BasketId}-finalize-partial", Arg.Any<CancellationToken>());
        });
        await payments.DidNotReceive().VoidAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<CancellationToken>());

        var persisted = await db.Baskets.AsNoTracking().FirstAsync(b => b.BasketId == BasketId);
        persisted.Status.Should().Be("PartiallyConfirmed");
        persisted.ChargedAmount.Should().Be(250.00m);
        persisted.RefundedAmount.Should().Be(456.00m);
    }

    // -----------------------------------------------------------------------
    // Hard failure before any capture → void the single PI
    // -----------------------------------------------------------------------
    [Fact]
    public async Task Orchestrator_voids_on_hard_failure_before_any_capture()
    {
        var dbOptions = NewDbOptions();
        using (var seed = new BookingDbContext(dbOptions))
        {
            seed.Database.EnsureCreated();
            SeedBasket(seed);
        }

        var payments = Substitute.For<IBasketPaymentGateway>();

        await using var db = new BookingDbContext(dbOptions);
        var sut = new BasketPaymentOrchestrator(db, payments, NullLogger<BasketPaymentOrchestrator>.Instance);

        await sut.Consume(MakeContext(new BookingFailed(
            FlightBookingId, Guid.NewGuid(), "GDS rejected", "price-reconfirm", DateTimeOffset.UtcNow)));

        await payments.Received(1).VoidAsync(PiId, $"basket-{BasketId}-void", Arg.Any<CancellationToken>());
        await payments.DidNotReceive().CapturePartialAsync(
            Arg.Any<string>(), Arg.Any<long>(), Arg.Any<bool>(), Arg.Any<string>(), Arg.Any<CancellationToken>());

        var persisted = await db.Baskets.AsNoTracking().FirstAsync(b => b.BasketId == BasketId);
        persisted.Status.Should().Be("Failed");
        persisted.ChargedAmount.Should().Be(0m);
    }

    // -----------------------------------------------------------------------
    // Replay idempotency — duplicate TicketIssued must NOT double-capture
    // -----------------------------------------------------------------------
    [Fact]
    public async Task Orchestrator_idempotent_on_webhook_replay()
    {
        var dbOptions = NewDbOptions();
        using (var seed = new BookingDbContext(dbOptions))
        {
            seed.Database.EnsureCreated();
            SeedBasket(seed);
        }

        var payments = Substitute.For<IBasketPaymentGateway>();
        payments.CapturePartialAsync(
                Arg.Any<string>(), Arg.Any<long>(), Arg.Any<bool>(), Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(new BasketCaptureResult(PiId, 25000, "succeeded"));

        await using var db = new BookingDbContext(dbOptions);
        var sut = new BasketPaymentOrchestrator(db, payments, NullLogger<BasketPaymentOrchestrator>.Instance);

        // Same MessageId (== EventId in the inbox) — orchestrator MUST dedupe.
        var messageId = Guid.NewGuid();
        var evt = new TicketIssued(FlightBookingId, "TK123", DateTimeOffset.UtcNow);

        await sut.Consume(MakeContext(evt, messageId: messageId));
        await sut.Consume(MakeContext(evt, messageId: messageId));

        await payments.Received(1).CapturePartialAsync(
            PiId, 25000, false, $"basket-{BasketId}-capture-flight", Arg.Any<CancellationToken>());

        var persisted = await db.Baskets.AsNoTracking().FirstAsync(b => b.BasketId == BasketId);
        persisted.FlightCaptured.Should().BeTrue();
        persisted.ChargedAmount.Should().Be(250.00m);
    }

    // -----------------------------------------------------------------------
    // helpers
    // -----------------------------------------------------------------------
    private static ConsumeContext<T> MakeContext<T>(T msg, Guid? messageId = null) where T : class
    {
        var ctx = Substitute.For<ConsumeContext<T>>();
        ctx.Message.Returns(msg);
        ctx.MessageId.Returns(messageId ?? Guid.NewGuid());
        ctx.CancellationToken.Returns(CancellationToken.None);
        // ctx.Publish<> needs to be a no-op for the orchestrator's terminal events.
        ctx.Publish(Arg.Any<object>(), Arg.Any<CancellationToken>()).Returns(Task.CompletedTask);
        return ctx;
    }
}
