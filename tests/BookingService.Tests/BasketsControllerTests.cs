using System.Security.Claims;
using FluentAssertions;
using MassTransit;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;
using TBE.BookingService.API.Controllers;
using TBE.BookingService.Application.Baskets;
using TBE.BookingService.Infrastructure;
using TBE.Contracts.Events;
using Xunit;

namespace TBE.BookingService.Tests;

/// <summary>
/// Plan 04-04 Task 1 — controller-level contract tests for BasketsController. Turns
/// the Wave 0 RED placeholders green. Uses EF Core InMemory + NSubstitute for the
/// publish endpoint and the IBasketPaymentGateway. The single-PI contract (D-08) is
/// enforced by asserting exactly ONE AuthorizeBasketAsync call with the literal
/// <c>basket-{id}-authorize</c> idempotency key.
/// </summary>
public class BasketsControllerTests
{
    private const string OwnerUserId = "user-owner-abc";
    private static readonly Guid FlightOfferId = Guid.Parse("44444444-4444-4444-4444-444444444444");
    private static readonly Guid HotelOfferId = Guid.Parse("55555555-5555-5555-5555-555555555555");

    private static BookingDbContext NewDb() =>
        new(new DbContextOptionsBuilder<BookingDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options);

    private static BasketsController NewController(
        BookingDbContext db,
        IPublishEndpoint publish,
        IBasketPaymentGateway payments,
        string? userId)
    {
        var claims = new List<Claim>();
        if (!string.IsNullOrEmpty(userId))
        {
            claims.Add(new Claim(ClaimTypes.NameIdentifier, userId));
            claims.Add(new Claim("sub", userId));
        }
        var identity = string.IsNullOrEmpty(userId)
            ? new ClaimsIdentity()
            : new ClaimsIdentity(claims, authenticationType: "jwt");
        var user = new ClaimsPrincipal(identity);

        return new BasketsController(db, publish, payments, NullLogger<BasketsController>.Instance)
        {
            ControllerContext = new ControllerContext
            {
                HttpContext = new DefaultHttpContext { User = user },
            },
        };
    }

    private static CreateBasketRequest ValidRequest() => new(
        FlightOfferId: FlightOfferId,
        HotelOfferId: HotelOfferId,
        CarOfferId: null,
        Currency: "GBP",
        FlightSubtotalHint: 250.00m,
        HotelSubtotalHint: 456.00m,
        Guest: new BasketGuestRequest("Alice Example", "alice@example.com", null));

    [Fact]
    public async Task PostBaskets_with_flight_and_hotel_publishes_BasketInitiated()
    {
        await using var db = NewDb();
        var publish = Substitute.For<IPublishEndpoint>();
        var payments = Substitute.For<IBasketPaymentGateway>();
        var controller = NewController(db, publish, payments, userId: OwnerUserId);

        var result = await controller.PostAsync(ValidRequest(), CancellationToken.None);

        var accepted = result.Should().BeOfType<AcceptedAtActionResult>().Subject;
        accepted.ActionName.Should().Be(nameof(BasketsController.GetStatusAsync));
        accepted.Value.Should().NotBeNull();

        await publish.Received(1).Publish(
            Arg.Is<BasketInitiated>(e =>
                e.UserId == OwnerUserId &&
                e.FlightOfferId == FlightOfferId &&
                e.HotelOfferId == HotelOfferId &&
                e.TotalAmount == 706.00m &&
                e.Currency == "GBP"),
            Arg.Any<CancellationToken>());

        db.Baskets.Should().HaveCount(1);
        var row = await db.Baskets.AsNoTracking().FirstAsync();
        row.UserId.Should().Be(OwnerUserId);
        row.Status.Should().Be("Initiated");
        row.TotalAmount.Should().Be(706.00m);
        row.FlightSubtotal.Should().Be(250.00m);
        row.HotelSubtotal.Should().Be(456.00m);
    }

    [Fact]
    public async Task PostBaskets_returns_401_for_unauthenticated()
    {
        await using var db = NewDb();
        var publish = Substitute.For<IPublishEndpoint>();
        var payments = Substitute.For<IBasketPaymentGateway>();
        var controller = NewController(db, publish, payments, userId: null);

        var result = await controller.PostAsync(ValidRequest(), CancellationToken.None);

        result.Should().BeOfType<UnauthorizedResult>();
        await publish.DidNotReceive().Publish(Arg.Any<BasketInitiated>(), Arg.Any<CancellationToken>());
        db.Baskets.Should().BeEmpty();
    }

    [Fact]
    public async Task GetBasketStatus_returns_current_state()
    {
        await using var db = NewDb();
        var publish = Substitute.For<IPublishEndpoint>();
        var payments = Substitute.For<IBasketPaymentGateway>();

        var basketId = Guid.NewGuid();
        var now = DateTime.UtcNow;
        db.Baskets.Add(new Basket
        {
            BasketId = basketId,
            UserId = OwnerUserId,
            FlightBookingId = Guid.NewGuid(),
            HotelBookingId = Guid.NewGuid(),
            StripePaymentIntentId = "pi_test_single_combined",
            Status = "PaymentAuthorized",
            TotalAmount = 706.00m,
            FlightSubtotal = 250.00m,
            HotelSubtotal = 456.00m,
            ChargedAmount = 0m,
            RefundedAmount = 0m,
            FlightCaptured = false,
            HotelCaptured = false,
            Currency = "GBP",
            GuestEmail = "alice@example.com",
            GuestFullName = "Alice Example",
            CreatedUtc = now,
            UpdatedUtc = now,
        });
        await db.SaveChangesAsync();

        var controller = NewController(db, publish, payments, userId: OwnerUserId);

        var result = await controller.GetStatusAsync(basketId, CancellationToken.None);

        var ok = result.Should().BeOfType<OkObjectResult>().Subject;
        var dto = ok.Value.Should().BeOfType<BasketDtoPublic>().Subject;

        dto.BasketId.Should().Be(basketId);
        dto.Status.Should().Be("PaymentAuthorized");
        dto.StripePaymentIntentId.Should().Be("pi_test_single_combined");
        dto.TotalAmount.Should().Be(706.00m);
        dto.FlightSubtotal.Should().Be(250.00m);
        dto.HotelSubtotal.Should().Be(456.00m);
        dto.Currency.Should().Be("GBP");
    }

    [Fact]
    public async Task PostPaymentIntent_returns_client_secret_with_idempotency_key()
    {
        await using var db = NewDb();
        var publish = Substitute.For<IPublishEndpoint>();
        var payments = Substitute.For<IBasketPaymentGateway>();

        var basketId = Guid.NewGuid();
        var now = DateTime.UtcNow;
        db.Baskets.Add(new Basket
        {
            BasketId = basketId,
            UserId = OwnerUserId,
            Status = "Initiated",
            TotalAmount = 706.00m,
            FlightSubtotal = 250.00m,
            HotelSubtotal = 456.00m,
            ChargedAmount = 0m,
            RefundedAmount = 0m,
            Currency = "GBP",
            GuestEmail = "alice@example.com",
            GuestFullName = "Alice Example",
            CreatedUtc = now,
            UpdatedUtc = now,
        });
        await db.SaveChangesAsync();

        payments.AuthorizeBasketAsync(
                Arg.Any<Guid>(), Arg.Any<decimal>(), Arg.Any<string>(), Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(new BasketAuthorizeResult("pi_abc123", "pi_abc123_secret_xyz", "requires_confirmation"));

        var controller = NewController(db, publish, payments, userId: OwnerUserId);

        var result = await controller.InitPaymentIntentAsync(basketId, CancellationToken.None);

        var ok = result.Should().BeOfType<OkObjectResult>().Subject;
        var body = ok.Value.Should().BeOfType<BasketPaymentIntentResponse>().Subject;
        body.ClientSecret.Should().Be("pi_abc123_secret_xyz");

        // D-08 — exactly ONE PaymentIntent authorize call with the single-PI key.
        await payments.Received(1).AuthorizeBasketAsync(
            basketId,
            706.00m,
            "GBP",
            $"basket-{basketId}-authorize",
            Arg.Any<CancellationToken>());

        // Explicitly verify no per-leg authorize keys were used (D-08 forbids this).
        await payments.DidNotReceive().AuthorizeBasketAsync(
            Arg.Any<Guid>(),
            Arg.Any<decimal>(),
            Arg.Any<string>(),
            Arg.Is<string>(k => k.Contains("authorize-flight") || k.Contains("authorize-hotel")),
            Arg.Any<CancellationToken>());

        await publish.Received(1).Publish(
            Arg.Is<BasketPaymentAuthorized>(e =>
                e.BasketId == basketId &&
                e.PaymentIntentId == "pi_abc123" &&
                e.AuthorizedAmount == 706.00m),
            Arg.Any<CancellationToken>());

        // Idempotency: a second call should NOT create a second Stripe PI.
        // (Controller reuses the persisted PaymentIntentId and returns the same gateway
        // result because the key is deterministic; under the real adapter Stripe dedupes.)
        await controller.InitPaymentIntentAsync(basketId, CancellationToken.None);
        await payments.Received(2).AuthorizeBasketAsync(
            basketId, 706.00m, "GBP", $"basket-{basketId}-authorize", Arg.Any<CancellationToken>());
        // But no additional Publish of BasketPaymentAuthorized should have happened.
        await publish.Received(1).Publish(Arg.Any<BasketPaymentAuthorized>(), Arg.Any<CancellationToken>());
    }
}
