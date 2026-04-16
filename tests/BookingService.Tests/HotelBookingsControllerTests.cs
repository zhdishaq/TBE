using System.Net;
using System.Security.Claims;
using System.Text;
using FluentAssertions;
using MassTransit;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;
using TBE.BookingService.API.Controllers;
using TBE.BookingService.Application.Saga;
using TBE.BookingService.Infrastructure;
using TBE.Contracts.Events;
using Xunit;

namespace TBE.BookingService.Tests;

/// <summary>
/// Plan 04-03 HOTB-01..05 — controller-level contract tests for the B2C hotel
/// booking surface. Uses InMemory DbContext + NSubstitute IPublishEndpoint.
/// Voucher streaming test uses a stubbed <see cref="HttpMessageHandler"/> so
/// the pass-through path is exercised without a real NotificationService.
/// </summary>
public class HotelBookingsControllerTests
{
    private const string OwnerUserId = "user-owner-abc";
    private const string OtherUserId = "user-other-xyz";
    private static readonly Guid BookingId = Guid.Parse("22222222-2222-2222-2222-222222222222");

    private static BookingDbContext NewDb() =>
        new(new DbContextOptionsBuilder<BookingDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options);

    private static HotelBookingSagaState SeedBooking(
        BookingDbContext db,
        string userId,
        string status = "Pending",
        string? supplierRef = null)
    {
        var state = new HotelBookingSagaState
        {
            CorrelationId = BookingId,
            UserId = userId,
            BookingReference = "HB-260416-ABCDEF01",
            SupplierRef = supplierRef,
            PropertyName = "The Grand Sample",
            AddressLine = "1 Example Street, London",
            CheckInDate = new DateOnly(2026, 5, 1),
            CheckOutDate = new DateOnly(2026, 5, 4),
            Rooms = 1,
            Adults = 2,
            Children = 0,
            TotalAmount = 456.00m,
            Currency = "GBP",
            GuestEmail = "alice@example.com",
            GuestFullName = "Alice Example",
            Status = status,
            InitiatedAtUtc = DateTime.UtcNow,
            ConfirmedAtUtc = status == "Confirmed" ? DateTime.UtcNow : null,
        };
        db.HotelBookingSagaStates.Add(state);
        db.SaveChanges();
        return state;
    }

    private sealed class StubHandler : HttpMessageHandler
    {
        public HttpResponseMessage Response = new(HttpStatusCode.OK);
        public HttpRequestMessage? LastRequest;
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            LastRequest = request;
            return Task.FromResult(Response);
        }
    }

    private static IHttpClientFactory FactoryReturning(HttpMessageHandler handler, string baseAddress = "http://notification-service-test:8080")
    {
        var factory = Substitute.For<IHttpClientFactory>();
        factory.CreateClient(Arg.Any<string>()).Returns(_ => new HttpClient(handler, disposeHandler: false)
        {
            BaseAddress = new Uri(baseAddress),
        });
        return factory;
    }

    private static HotelBookingsController NewController(
        BookingDbContext db,
        IPublishEndpoint publishEndpoint,
        string? userId,
        bool isBackoffice = false,
        IHttpClientFactory? httpFactory = null)
    {
        var claims = new List<Claim>();
        if (!string.IsNullOrEmpty(userId))
        {
            claims.Add(new Claim(ClaimTypes.NameIdentifier, userId));
            claims.Add(new Claim("sub", userId));
        }
        if (isBackoffice) claims.Add(new Claim(ClaimTypes.Role, "backoffice-staff"));

        var identity = string.IsNullOrEmpty(userId)
            ? new ClaimsIdentity()  // unauthenticated
            : new ClaimsIdentity(claims, authenticationType: "jwt");
        var user = new ClaimsPrincipal(identity);

        httpFactory ??= FactoryReturning(new StubHandler());

        var controller = new HotelBookingsController(
            db, publishEndpoint, httpFactory, NullLogger<HotelBookingsController>.Instance)
        {
            ControllerContext = new ControllerContext
            {
                HttpContext = new DefaultHttpContext { User = user },
            },
        };
        return controller;
    }

    private static CreateHotelBookingRequest ValidRequest() => new(
        OfferId: Guid.Parse("33333333-3333-3333-3333-333333333333"),
        CheckInDate: new DateOnly(2026, 5, 1),
        CheckOutDate: new DateOnly(2026, 5, 4),
        Rooms: 1,
        Adults: 2,
        Children: 0,
        Guest: new HotelGuestRequest("Alice Example", "alice@example.com", null));

    // ------------------------------------------------------------------
    // POST /hotel-bookings
    // ------------------------------------------------------------------

    [Fact]
    public async Task Post_without_sub_claim_returns_401()
    {
        // HotelBookingsController class-level [Authorize] is enforced by ASP.NET pipeline in
        // production, but the controller also falls back to Unauthorized() when no sub claim
        // resolves so a direct unit invocation exercises the path.
        await using var db = NewDb();
        var publish = Substitute.For<IPublishEndpoint>();
        var controller = NewController(db, publish, userId: null);

        var result = await controller.PostAsync(ValidRequest(), CancellationToken.None);

        result.Should().BeOfType<UnauthorizedResult>();
        await publish.DidNotReceive().Publish(Arg.Any<HotelBookingInitiated>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Post_happy_path_publishes_HotelBookingInitiated_and_returns_202()
    {
        await using var db = NewDb();
        var publish = Substitute.For<IPublishEndpoint>();
        var controller = NewController(db, publish, userId: OwnerUserId);

        var result = await controller.PostAsync(ValidRequest(), CancellationToken.None);

        var accepted = result.Should().BeOfType<AcceptedAtActionResult>().Subject;
        accepted.ActionName.Should().Be(nameof(HotelBookingsController.GetStatusAsync));
        accepted.Value.Should().NotBeNull();

        await publish.Received(1).Publish(
            Arg.Is<HotelBookingInitiated>(e =>
                e.UserId == OwnerUserId &&
                e.OfferId == Guid.Parse("33333333-3333-3333-3333-333333333333") &&
                e.Guest.Email == "alice@example.com"),
            Arg.Any<CancellationToken>());

        // Saga state row was persisted with Status=Pending.
        db.HotelBookingSagaStates.Should().HaveCount(1);
        var row = await db.HotelBookingSagaStates.AsNoTracking().FirstAsync();
        row.UserId.Should().Be(OwnerUserId);
        row.Status.Should().Be("Pending");
    }

    [Fact]
    public async Task Post_rejects_checkout_on_or_before_checkin_with_400()
    {
        await using var db = NewDb();
        var publish = Substitute.For<IPublishEndpoint>();
        var controller = NewController(db, publish, userId: OwnerUserId);

        var bad = ValidRequest() with
        {
            CheckInDate = new DateOnly(2026, 5, 4),
            CheckOutDate = new DateOnly(2026, 5, 1),
        };

        var result = await controller.PostAsync(bad, CancellationToken.None);

        result.Should().BeOfType<BadRequestObjectResult>();
        await publish.DidNotReceive().Publish(Arg.Any<HotelBookingInitiated>(), Arg.Any<CancellationToken>());
    }

    // ------------------------------------------------------------------
    // GET /hotel-bookings/{id}
    // ------------------------------------------------------------------

    [Fact]
    public async Task GetStatus_returns_403_for_other_user()
    {
        await using var db = NewDb();
        SeedBooking(db, OwnerUserId);
        var publish = Substitute.For<IPublishEndpoint>();
        var controller = NewController(db, publish, userId: OtherUserId);

        var result = await controller.GetStatusAsync(BookingId, CancellationToken.None);

        result.Should().BeOfType<ForbidResult>();
    }

    [Fact]
    public async Task GetStatus_returns_200_public_dto_for_owner()
    {
        await using var db = NewDb();
        SeedBooking(db, OwnerUserId, status: "Confirmed", supplierRef: "SUP-ABC-123");
        var publish = Substitute.For<IPublishEndpoint>();
        var controller = NewController(db, publish, userId: OwnerUserId);

        var result = await controller.GetStatusAsync(BookingId, CancellationToken.None);

        var ok = result.Should().BeOfType<OkObjectResult>().Subject;
        var dto = ok.Value.Should().BeOfType<HotelBookingDtoPublic>().Subject;

        dto.Id.Should().Be(BookingId);
        dto.Status.Should().Be("Confirmed");
        dto.SupplierRef.Should().Be("SUP-ABC-123");
        dto.PropertyName.Should().Be("The Grand Sample");
        dto.TotalAmount.Should().Be(456.00m);
        dto.Currency.Should().Be("GBP");
    }

    // ------------------------------------------------------------------
    // GET /hotel-bookings/{id}/voucher.pdf
    // ------------------------------------------------------------------

    [Fact]
    public async Task GetVoucher_returns_404_when_not_confirmed()
    {
        await using var db = NewDb();
        SeedBooking(db, OwnerUserId, status: "Pending");
        var publish = Substitute.For<IPublishEndpoint>();
        var handler = new StubHandler();
        var factory = FactoryReturning(handler);
        var controller = NewController(db, publish, userId: OwnerUserId, httpFactory: factory);

        var result = await controller.GetVoucherAsync(BookingId, CancellationToken.None);

        result.Should().BeOfType<NotFoundResult>();
        handler.LastRequest.Should().BeNull("controller must NOT call NotificationService until status=Confirmed");
    }

    [Fact]
    public async Task GetVoucher_streams_upstream_body_with_pdf_content_type()
    {
        await using var db = NewDb();
        SeedBooking(db, OwnerUserId, status: "Confirmed", supplierRef: "SUP-ABC-123");
        var publish = Substitute.For<IPublishEndpoint>();

        var pdfBytes = Encoding.ASCII.GetBytes("%PDF-1.4 sample body for streaming pass-through");
        var upstream = new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new ByteArrayContent(pdfBytes),
        };
        upstream.Content.Headers.ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue("application/pdf");
        var handler = new StubHandler { Response = upstream };
        var factory = FactoryReturning(handler);

        var controller = NewController(db, publish, userId: OwnerUserId, httpFactory: factory);

        // Capture Response.Body pass-through.
        using var body = new MemoryStream();
        controller.ControllerContext.HttpContext.Response.Body = body;

        var result = await controller.GetVoucherAsync(BookingId, CancellationToken.None);

        result.Should().BeOfType<EmptyResult>();
        controller.Response.ContentType.Should().Be("application/pdf");
        controller.Response.Headers["Content-Disposition"].ToString().Should().Contain("voucher-HB-260416-ABCDEF01.pdf");
        body.ToArray().Should().BeEquivalentTo(pdfBytes);
        handler.LastRequest.Should().NotBeNull();
        handler.LastRequest!.RequestUri!.PathAndQuery.Should().Contain($"/notifications/hotel-voucher/{BookingId}.pdf");
    }
}
