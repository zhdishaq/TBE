using System.Security.Claims;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using TBE.BackofficeService.Application.Controllers;
using TBE.BackofficeService.Application.Entities;
using TBE.BackofficeService.Infrastructure;
using Xunit;

namespace TBE.BackofficeService.Tests;

/// <summary>
/// BO-01 — unified booking list must return bookings across B2C, B2B,
/// and Manual channels with RBAC allowing all 4 ops-* roles read access.
/// Backoffice staff are not agency-scoped (cross-tenant read is the
/// intended behaviour). Filter by channel / status / free-text query.
/// VALIDATION.md Task 6-01-06.
///
/// <para>
/// EF InMemory is used for the test surface: in production the
/// BackofficeService reads <c>BookingSagaState</c> via a cross-schema
/// DbSet (BookingReadModel) on BackofficeDbContext. The InMemory provider
/// ignores schema names, so the test seeds BookingReadModel rows directly
/// and asserts the controller surfaces them with the correct channel /
/// status / agency decoupling.
/// </para>
/// </summary>
public sealed class UnifiedBookingListTests
{
    private static ControllerContext Ctx(params string[] roles)
    {
        var claims = roles.Select(r => new Claim(ClaimTypes.Role, r)).ToList();
        claims.Add(new Claim("preferred_username", "ops-test"));
        return new ControllerContext
        {
            HttpContext = new DefaultHttpContext
            {
                User = new ClaimsPrincipal(new ClaimsIdentity(claims, "test")),
            },
        };
    }

    private static BookingsController Build(out BackofficeDbContext db)
    {
        // validateScopes: false because this test resolves the scoped
        // BackofficeDbContext directly from the root provider (no HTTP
        // request pipeline). The dependency graph is trivially flat
        // (controller + DbContext + logger) so scope safety is moot.
        var provider = new ServiceCollection()
            .AddDbContext<BackofficeDbContext>(o => o.UseInMemoryDatabase($"bookings-{Guid.NewGuid()}"))
            .BuildServiceProvider(validateScopes: false);

        db = provider.GetRequiredService<BackofficeDbContext>();
        return new BookingsController(db, NullLogger<BookingsController>.Instance);
    }

    private static void Seed3Channels(BackofficeDbContext db)
    {
        db.BookingReadModel.Add(new BookingReadRow
        {
            CorrelationId = Guid.NewGuid(),
            BookingReference = "TBE-B2C-1",
            ChannelKind = 0, // B2C
            CurrentState = 7,
            GdsPnr = "PNR123",
            CustomerName = "Alice Traveller",
            CustomerEmail = "alice@example.com",
            TotalAmount = 450.00m,
            Currency = "GBP",
            InitiatedAtUtc = DateTime.UtcNow.AddDays(-3),
        });
        db.BookingReadModel.Add(new BookingReadRow
        {
            CorrelationId = Guid.NewGuid(),
            BookingReference = "TBE-B2B-1",
            ChannelKind = 1, // B2B
            CurrentState = 7,
            GdsPnr = "PNR456",
            AgencyId = Guid.NewGuid(),
            CustomerName = "Bob OnBehalf",
            CustomerEmail = "bob@example.com",
            TotalAmount = 1200.00m,
            Currency = "GBP",
            InitiatedAtUtc = DateTime.UtcNow.AddDays(-2),
        });
        db.BookingReadModel.Add(new BookingReadRow
        {
            CorrelationId = Guid.NewGuid(),
            BookingReference = "TBE-MAN-1",
            ChannelKind = 2, // Manual (Plan 06-02 reserves 2; column accepts it today)
            CurrentState = 7,
            GdsPnr = "PNR789",
            AgencyId = Guid.NewGuid(),
            CustomerName = "Carol Offline",
            CustomerEmail = "carol@example.com",
            TotalAmount = 2400.00m,
            Currency = "GBP",
            InitiatedAtUtc = DateTime.UtcNow.AddDays(-1),
        });
        db.SaveChanges();
    }

    [Fact]
    [Trait("Category", "Phase06")]
    public async Task Returns_B2C_B2B_Manual_channels_with_role_based_filter_BO01()
    {
        var controller = Build(out var db);
        Seed3Channels(db);
        controller.ControllerContext = Ctx("ops-read");

        var result = await controller.List(new BookingsController.ListQuery(), CancellationToken.None);
        var ok = Assert.IsType<OkObjectResult>(result);
        var body = Assert.IsType<BookingsController.BookingListResponse>(ok.Value);
        Assert.Equal(3, body.Rows.Count);
        Assert.Equal(3, body.TotalCount);
        Assert.Contains(body.Rows, r => r.Channel == "B2C");
        Assert.Contains(body.Rows, r => r.Channel == "B2B");
        Assert.Contains(body.Rows, r => r.Channel == "Manual");
    }

    [Fact]
    [Trait("Category", "Phase06")]
    public async Task All_four_ops_roles_get_200_on_list()
    {
        foreach (var role in new[] { "ops-read", "ops-cs", "ops-finance", "ops-admin" })
        {
            var controller = Build(out var db);
            Seed3Channels(db);
            controller.ControllerContext = Ctx(role);
            var result = await controller.List(new BookingsController.ListQuery(), CancellationToken.None);
            Assert.IsType<OkObjectResult>(result);
        }
    }

    [Fact]
    [Trait("Category", "Phase06")]
    public async Task Channel_filter_narrows_to_B2B_only()
    {
        var controller = Build(out var db);
        Seed3Channels(db);
        controller.ControllerContext = Ctx("ops-read");

        var result = await controller.List(
            new BookingsController.ListQuery { Channel = "B2B" },
            CancellationToken.None);
        var ok = Assert.IsType<OkObjectResult>(result);
        var body = Assert.IsType<BookingsController.BookingListResponse>(ok.Value);
        Assert.Single(body.Rows);
        Assert.Equal("B2B", body.Rows[0].Channel);
    }

    [Fact]
    [Trait("Category", "Phase06")]
    public async Task Free_text_query_matches_PNR_substring()
    {
        var controller = Build(out var db);
        Seed3Channels(db);
        controller.ControllerContext = Ctx("ops-read");

        var result = await controller.List(
            new BookingsController.ListQuery { Q = "PNR456" },
            CancellationToken.None);
        var ok = Assert.IsType<OkObjectResult>(result);
        var body = Assert.IsType<BookingsController.BookingListResponse>(ok.Value);
        Assert.Single(body.Rows);
        Assert.Equal("PNR456", body.Rows[0].Pnr);
    }

    [Fact]
    [Trait("Category", "Phase06")]
    public async Task Free_text_query_matches_customer_email()
    {
        var controller = Build(out var db);
        Seed3Channels(db);
        controller.ControllerContext = Ctx("ops-read");

        var result = await controller.List(
            new BookingsController.ListQuery { Q = "carol@example.com" },
            CancellationToken.None);
        var ok = Assert.IsType<OkObjectResult>(result);
        var body = Assert.IsType<BookingsController.BookingListResponse>(ok.Value);
        Assert.Single(body.Rows);
        Assert.Equal("Carol Offline", body.Rows[0].CustomerName);
    }

    [Fact]
    [Trait("Category", "Phase06")]
    public async Task Pagination_respects_pageSize()
    {
        var controller = Build(out var db);
        Seed3Channels(db);
        controller.ControllerContext = Ctx("ops-read");

        var p1 = await controller.List(
            new BookingsController.ListQuery { PageSize = 2, Page = 1 },
            CancellationToken.None);
        var body1 = Assert.IsType<BookingsController.BookingListResponse>(
            Assert.IsType<OkObjectResult>(p1).Value);
        Assert.Equal(2, body1.Rows.Count);
        Assert.Equal(3, body1.TotalCount);

        var p2 = await controller.List(
            new BookingsController.ListQuery { PageSize = 2, Page = 2 },
            CancellationToken.None);
        var body2 = Assert.IsType<BookingsController.BookingListResponse>(
            Assert.IsType<OkObjectResult>(p2).Value);
        Assert.Single(body2.Rows);
    }

    [Fact]
    [Trait("Category", "Phase06")]
    public async Task Detail_returns_BookingEvents_timeline_and_cancellation_requests()
    {
        var controller = Build(out var db);
        Seed3Channels(db);
        var bookingId = db.BookingReadModel.First().CorrelationId;

        // Seed one CancellationRequest for the same booking.
        db.CancellationRequests.Add(new CancellationRequest
        {
            Id = Guid.NewGuid(),
            BookingId = bookingId,
            ReasonCode = "CustomerRequest",
            Reason = "Customer asked to cancel",
            RequestedBy = "ops-cs-1",
            RequestedAt = DateTime.UtcNow,
            ExpiresAt = DateTime.UtcNow.AddHours(72),
            Status = "PendingApproval",
        });
        db.SaveChanges();

        controller.ControllerContext = Ctx("ops-read");

        var result = await controller.Detail(bookingId, CancellationToken.None);
        var ok = Assert.IsType<OkObjectResult>(result);
        var body = Assert.IsType<BookingsController.BookingDetailResponse>(ok.Value);
        Assert.Equal(bookingId, body.BookingId);
        Assert.NotNull(body.BookingEvents);
        Assert.Single(body.CancellationRequests);
        Assert.Equal("PendingApproval", body.CancellationRequests[0].Status);
    }
}
