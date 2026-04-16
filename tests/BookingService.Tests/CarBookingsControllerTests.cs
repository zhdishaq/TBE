using System.Security.Claims;
using FluentAssertions;
using MassTransit;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;
using TBE.BookingService.API.Controllers;
using TBE.BookingService.Application.Cars;
using TBE.BookingService.Infrastructure;
using TBE.Contracts.Events;
using Xunit;

namespace TBE.BookingService.Tests;

/// <summary>
/// Plan 04-04 Task 3a — controller-level contract tests for <see cref="CarBookingsController"/>.
/// Uses EF Core InMemory + NSubstitute for the publish endpoint. Three canonical cases:
/// <list type="bullet">
///   <item>POST unauthenticated → 401.</item>
///   <item>POST happy path persists CarBooking and publishes CarBookingInitiated.</item>
///   <item>GET ownership check — cross-user access returns 403 (IDOR guard).</item>
/// </list>
/// </summary>
public class CarBookingsControllerTests
{
    private const string OwnerUserId = "user-owner-abc";
    private const string OtherUserId = "user-other-xyz";
    private static readonly Guid OfferId = Guid.Parse("77777777-7777-7777-7777-777777777777");

    private static BookingDbContext NewDb() =>
        new(new DbContextOptionsBuilder<BookingDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options);

    private static CarBookingsController NewController(
        BookingDbContext db,
        IPublishEndpoint publish,
        string? userId,
        bool isBackoffice = false)
    {
        var claims = new List<Claim>();
        if (!string.IsNullOrEmpty(userId))
        {
            claims.Add(new Claim(ClaimTypes.NameIdentifier, userId));
            claims.Add(new Claim("sub", userId));
        }
        if (isBackoffice)
        {
            claims.Add(new Claim(ClaimTypes.Role, "backoffice-staff"));
        }
        var identity = string.IsNullOrEmpty(userId)
            ? new ClaimsIdentity()
            : new ClaimsIdentity(claims, authenticationType: "jwt");
        var user = new ClaimsPrincipal(identity);

        return new CarBookingsController(db, publish, NullLogger<CarBookingsController>.Instance)
        {
            ControllerContext = new ControllerContext
            {
                HttpContext = new DefaultHttpContext { User = user },
            },
        };
    }

    private static CreateCarBookingRequest ValidRequest() => new(
        OfferId: OfferId,
        VendorName: "Avis",
        PickupLocation: "LHR Terminal 5",
        DropoffLocation: "LHR Terminal 5",
        PickupAtUtc: new DateTime(2026, 5, 1, 10, 0, 0, DateTimeKind.Utc),
        DropoffAtUtc: new DateTime(2026, 5, 4, 10, 0, 0, DateTimeKind.Utc),
        DriverAge: 30,
        Guest: new CarGuestRequest("Alice Example", "alice@example.com", null));

    [Fact]
    public async Task PostCarBookings_returns_401_for_unauthenticated()
    {
        await using var db = NewDb();
        var publish = Substitute.For<IPublishEndpoint>();
        var controller = NewController(db, publish, userId: null);

        var result = await controller.PostAsync(ValidRequest(), CancellationToken.None);

        result.Should().BeOfType<UnauthorizedResult>();
        await publish.DidNotReceive().Publish(Arg.Any<CarBookingInitiated>(), Arg.Any<CancellationToken>());
        db.CarBookings.Should().BeEmpty();
    }

    [Fact]
    public async Task PostCarBookings_happy_path_persists_and_publishes()
    {
        await using var db = NewDb();
        var publish = Substitute.For<IPublishEndpoint>();
        var controller = NewController(db, publish, userId: OwnerUserId);

        var result = await controller.PostAsync(ValidRequest(), CancellationToken.None);

        var accepted = result.Should().BeOfType<AcceptedAtActionResult>().Subject;
        accepted.ActionName.Should().Be(nameof(CarBookingsController.GetStatusAsync));
        accepted.Value.Should().NotBeNull();

        await publish.Received(1).Publish(
            Arg.Is<CarBookingInitiated>(e =>
                e.UserId == OwnerUserId &&
                e.OfferId == OfferId &&
                e.VendorName == "Avis" &&
                e.PickupLocation == "LHR Terminal 5" &&
                e.DropoffLocation == "LHR Terminal 5" &&
                e.DriverAge == 30 &&
                e.GuestEmail == "alice@example.com" &&
                e.GuestFullName == "Alice Example"),
            Arg.Any<CancellationToken>());

        db.CarBookings.Should().HaveCount(1);
        var row = await db.CarBookings.AsNoTracking().FirstAsync();
        row.UserId.Should().Be(OwnerUserId);
        row.Status.Should().Be("Pending");
        row.OfferId.Should().Be(OfferId);
        row.BookingReference.Should().StartWith("CB-");
        row.DriverAge.Should().Be(30);
        row.Currency.Should().Be("GBP");
    }

    [Fact]
    public async Task GetCarBooking_returns_403_for_cross_user_access()
    {
        await using var db = NewDb();
        var publish = Substitute.For<IPublishEndpoint>();

        var bookingId = Guid.NewGuid();
        var now = DateTime.UtcNow;
        db.CarBookings.Add(new CarBooking
        {
            BookingId = bookingId,
            UserId = OwnerUserId,           // persisted under owner
            OfferId = OfferId,
            BookingReference = "CB-260501-ABCDEF12",
            VendorName = "Avis",
            PickupLocation = "LHR",
            DropoffLocation = "LHR",
            PickupAtUtc = now.AddDays(14),
            DropoffAtUtc = now.AddDays(17),
            DriverAge = 30,
            TotalAmount = 189.00m,
            Currency = "GBP",
            GuestEmail = "alice@example.com",
            GuestFullName = "Alice Example",
            Status = "Pending",
            CreatedUtc = now,
            UpdatedUtc = now,
        });
        await db.SaveChangesAsync();

        // Requester authenticates as a DIFFERENT user → IDOR guard must 403.
        var controller = NewController(db, publish, userId: OtherUserId);

        var result = await controller.GetStatusAsync(bookingId, CancellationToken.None);

        result.Should().BeOfType<ForbidResult>();
    }

    [Fact]
    public async Task GetCarBooking_backoffice_role_bypasses_ownership()
    {
        await using var db = NewDb();
        var publish = Substitute.For<IPublishEndpoint>();

        var bookingId = Guid.NewGuid();
        var now = DateTime.UtcNow;
        db.CarBookings.Add(new CarBooking
        {
            BookingId = bookingId,
            UserId = OwnerUserId,
            OfferId = OfferId,
            BookingReference = "CB-260501-DEADBEEF",
            VendorName = "Avis",
            PickupLocation = "LHR",
            DropoffLocation = "LHR",
            PickupAtUtc = now.AddDays(14),
            DropoffAtUtc = now.AddDays(17),
            DriverAge = 30,
            TotalAmount = 189.00m,
            Currency = "GBP",
            GuestEmail = "alice@example.com",
            GuestFullName = "Alice Example",
            Status = "Pending",
            CreatedUtc = now,
            UpdatedUtc = now,
        });
        await db.SaveChangesAsync();

        // Backoffice staff can read any booking (ownership bypass).
        var controller = NewController(db, publish, userId: OtherUserId, isBackoffice: true);

        var result = await controller.GetStatusAsync(bookingId, CancellationToken.None);

        var ok = result.Should().BeOfType<OkObjectResult>().Subject;
        var dto = ok.Value.Should().BeOfType<CarBookingDtoPublic>().Subject;
        dto.Id.Should().Be(bookingId);
        dto.VendorName.Should().Be("Avis");
    }
}
