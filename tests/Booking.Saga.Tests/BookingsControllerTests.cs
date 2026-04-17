using System.Security.Claims;
using FluentAssertions;
using MassTransit;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;
using TBE.BookingService.API.Controllers;
using TBE.BookingService.Application.Saga;
using TBE.BookingService.Infrastructure;
using TBE.Contracts.Events;
using Xunit;

namespace Booking.Saga.Tests;

/// <summary>
/// Unit tests for BookingsController — focus on auth boundary, DTO PII exclusion,
/// and the outbox publish contract. Uses InMemory DbContext + NSubstitute IPublishEndpoint.
/// </summary>
[Trait("Category", "Unit")]
public class BookingsControllerTests
{
    private static BookingDbContext NewDb(out IPublishEndpoint publish)
    {
        var options = new DbContextOptionsBuilder<BookingDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        publish = Substitute.For<IPublishEndpoint>();
        return new BookingDbContext(options);
    }

    private static BookingsController Build(BookingDbContext db, IPublishEndpoint publish, string? userId, string? role = null)
    {
        var logger = NullLogger<BookingsController>.Instance;
        var ctrl = new BookingsController(db, publish, logger);
        var claims = new List<Claim>();
        if (userId is not null) claims.Add(new Claim(ClaimTypes.NameIdentifier, userId));
        if (role is not null) claims.Add(new Claim(ClaimTypes.Role, role));
        var identity = new ClaimsIdentity(claims, "TestAuth");
        var user = new ClaimsPrincipal(identity);
        ctrl.ControllerContext = new ControllerContext
        {
            HttpContext = new DefaultHttpContext { User = user },
        };
        return ctrl;
    }

    [Fact(DisplayName = "FLTB01: POST /bookings publishes BookingInitiated event")]
    public async Task FLTB01_post_bookings_publishes_BookingInitiated_event()
    {
        var db = NewDb(out var publish);
        var ctrl = Build(db, publish, "user-1");

        var req = new CreateBookingRequest("flight", "b2c", 100m, "USD", "card", null);
        var result = await ctrl.PostAsync(req, CancellationToken.None);

        result.Should().BeOfType<AcceptedAtActionResult>();
        await publish.Received(1).Publish(Arg.Any<BookingInitiated>(), Arg.Any<CancellationToken>());
    }

    [Fact(DisplayName = "FLTB03: POST /bookings with negative amount returns 400")]
    public async Task FLTB03_post_bookings_with_negative_amount_returns_400()
    {
        var db = NewDb(out var publish);
        var ctrl = Build(db, publish, "user-1");

        var req = new CreateBookingRequest("flight", "b2c", -1m, "USD", "card", null);
        var result = await ctrl.PostAsync(req, CancellationToken.None);

        result.Should().BeOfType<BadRequestObjectResult>();
        await publish.DidNotReceive().Publish(Arg.Any<BookingInitiated>(), Arg.Any<CancellationToken>());
    }

    [Fact(DisplayName = "FLTB09: GET /bookings/{id} for other user returns 403")]
    public async Task FLTB09_get_booking_for_other_user_returns_403()
    {
        var db = NewDb(out var publish);
        var bookingId = Guid.NewGuid();
        db.BookingSagaStates.Add(new BookingSagaState
        {
            CorrelationId = bookingId,
            BookingReference = "TBE-260416-AAA",
            ProductType = "flight",
            ChannelText = "b2c",
            UserId = "user-B",
            Currency = "USD",
            PaymentMethod = "card",
            TotalAmount = 100m,
            InitiatedAtUtc = DateTime.UtcNow,
            TicketingDeadlineUtc = DateTime.UtcNow.AddHours(24),
        });
        await db.SaveChangesAsync();

        var ctrl = Build(db, publish, userId: "user-A");

        var result = await ctrl.GetByIdAsync(bookingId, CancellationToken.None);

        result.Should().BeOfType<ForbidResult>();
    }

    [Fact(DisplayName = "COMP01/02/D-20: BookingDtoPublic does not expose passport or payment fields")]
    public void COMP01_booking_dto_does_not_expose_passport_or_payment_fields()
    {
        var names = typeof(BookingDtoPublic).GetProperties().Select(p => p.Name).ToArray();

        names.Should().NotContain("Passport");
        names.Should().NotContain("PassportNumber");
        names.Should().NotContain("DateOfBirth");
        names.Should().NotContain("CardNumber");
        names.Should().NotContain("Cvv");
        names.Should().NotContain("UserId"); // no user identifier leakage either
    }
}
