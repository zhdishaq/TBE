using System.Security.Claims;
using System.Text.Json;
using MassTransit;
using MassTransit.Testing;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using TBE.BookingService.API.Controllers;
using TBE.BookingService.Application;
using TBE.BookingService.Application.Saga;
using TBE.BookingService.Infrastructure;
using TBE.Contracts.Enums;
using TBE.Contracts.Events;
using Xunit;

namespace TBE.BackofficeService.Tests;

/// <summary>
/// BO-02 — Manual booking entry (phone / walk-in sales). Plan 06-02 Task 1.
///
/// <para>
/// A manual booking is a staff-entered booking with
/// <see cref="Channel.Manual"/> = 2 that BYPASSES the saga entirely —
/// no <c>BookingInitiated</c> event is published, no GDS adapter is
/// called, no Stripe authorization runs. <c>ManualBookingCommand</c>
/// inserts a <see cref="BookingSagaState"/> row with the terminal
/// Confirmed status and writes one <c>BookingEvents</c> row of
/// <c>EventType='ManualBookingCreated'</c>.
/// </para>
///
/// <para>
/// Per Pitfall 28 the request DTO never contains Channel or Status —
/// those are stamped server-side. Duplicate <c>SupplierReference</c>
/// within 24h returns 409 problem+json.
/// </para>
///
/// <para>
/// EF InMemory is used for the test surface. The BookingService
/// <c>BookingDbContext</c> owns the <c>BookingSagaState</c> DbSet and
/// the BookingEvents writer runs against a writer-only DbContext; both
/// are swapped to InMemory here for scope-safety with
/// <c>validateScopes: false</c> (no HTTP request pipeline).
/// </para>
/// </summary>
public sealed class ManualBookingEntryTests
{
    private static ControllerContext Ctx(string preferredUsername, params string[] roles)
    {
        var claims = roles.Select(r => new Claim(ClaimTypes.Role, r)).ToList();
        claims.Add(new Claim("preferred_username", preferredUsername));
        return new ControllerContext
        {
            HttpContext = new DefaultHttpContext
            {
                User = new ClaimsPrincipal(new ClaimsIdentity(claims, "test")),
            },
        };
    }

    private static async Task<(ManualBookingsController controller, BookingDbContext db, BookingEventsDbContext events, ITestHarness harness)>
        BuildAsync()
    {
        var provider = new ServiceCollection()
            .AddDbContext<BookingDbContext>(o => o.UseInMemoryDatabase($"booking-{Guid.NewGuid()}"))
            .AddDbContext<BookingEventsDbContext>(o => o.UseInMemoryDatabase($"events-{Guid.NewGuid()}"))
            .AddSingleton(NullLoggerFactory.Instance)
            .AddLogging()
            .AddScoped<IBookingEventsWriter, BookingEventsWriter>()
            .AddScoped<ManualBookingCommand>()
            .AddMassTransitTestHarness()
            .BuildServiceProvider(validateScopes: false);

        var harness = provider.GetRequiredService<ITestHarness>();
        await harness.Start();

        var scope = provider.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<BookingDbContext>();
        var events = scope.ServiceProvider.GetRequiredService<BookingEventsDbContext>();
        var command = scope.ServiceProvider.GetRequiredService<ManualBookingCommand>();
        var controller = new ManualBookingsController(
            command, NullLogger<ManualBookingsController>.Instance);
        return (controller, db, events, harness);
    }

    private static ManualBookingsController.CreateManualBookingRequest MinimalRequest(
        string? supplierRef = "AMD-12345",
        string bookingRef = "TBE-MAN-0001") =>
        new(
            BookingReference: bookingRef,
            Pnr: bookingRef,
            ProductType: "Flight",
            BaseFareAmount: 800m,
            SurchargeAmount: 50m,
            TaxAmount: 150m,
            Currency: "GBP",
            CustomerId: null,
            CustomerName: "Alice Walkin",
            CustomerEmail: "alice@example.com",
            CustomerPhone: "+44 7700 900000",
            AgencyId: null,
            ItineraryJson: """{"passengers":[{"type":"Adult","firstName":"Alice","lastName":"Walkin"}],"segments":[{"origin":"LHR","destination":"JFK","carrier":"BA"}]}""",
            SupplierReference: supplierRef,
            Notes: "Booked by ops-cs on call");

    [Fact]
    [Trait("Category", "Phase06")]
    public async Task ops_cs_can_create_manual_booking_returns_201_channel_manual_status_confirmed()
    {
        var (controller, db, events, harness) = await BuildAsync();
        try
        {
            controller.ControllerContext = Ctx("ops-cs-1", "ops-cs");
            var result = await controller.Create(MinimalRequest(), CancellationToken.None);

            var created = Assert.IsType<CreatedResult>(result);
            var body = created.Value!;
            var bookingId = (Guid)body.GetType().GetProperty("BookingId")!.GetValue(body)!;
            Assert.NotEqual(Guid.Empty, bookingId);

            var row = await db.BookingSagaStates.SingleAsync(b => b.CorrelationId == bookingId);
            Assert.Equal(Channel.Manual, row.Channel);
            Assert.Equal((int)Channel.Manual, (int)row.Channel);
            Assert.Equal("TBE-MAN-0001", row.BookingReference);
            Assert.Equal(800m, row.BaseFareAmount);
            Assert.Equal(50m, row.SurchargeAmount);
            Assert.Equal(150m, row.TaxAmount);
            Assert.Equal(1000m, row.TotalAmount);
            Assert.Equal("GBP", row.Currency);
            Assert.Equal("Alice Walkin", row.CustomerName);
            Assert.Equal("AMD-12345", row.SupplierReference);
            Assert.Contains("\"origin\":\"LHR\"", row.ItineraryJson);
        }
        finally
        {
            await harness.Stop();
        }
    }

    [Fact]
    [Trait("Category", "Phase06")]
    public async Task manual_booking_writes_one_BookingEvents_row_with_ManualBookingCreated()
    {
        var (controller, db, events, harness) = await BuildAsync();
        try
        {
            controller.ControllerContext = Ctx("ops-cs-1", "ops-cs");
            var result = await controller.Create(MinimalRequest(), CancellationToken.None);
            var bookingId = (Guid)((CreatedResult)result).Value!.GetType()
                .GetProperty("BookingId")!.GetValue(((CreatedResult)result).Value)!;

            var eventRows = await events.Events.Where(e => e.BookingId == bookingId).ToListAsync();
            var evt = Assert.Single(eventRows);
            Assert.Equal("ManualBookingCreated", evt.EventType);
            Assert.Equal("ops-cs-1", evt.Actor);

            using var doc = JsonDocument.Parse(evt.Snapshot);
            Assert.Equal("Manual", doc.RootElement.GetProperty("Channel").GetString());
            Assert.Equal("Confirmed", doc.RootElement.GetProperty("Status").GetString());
        }
        finally
        {
            await harness.Stop();
        }
    }

    [Fact]
    [Trait("Category", "Phase06")]
    public async Task manual_booking_does_not_publish_BookingInitiated_saga_start_event()
    {
        var (controller, db, events, harness) = await BuildAsync();
        try
        {
            controller.ControllerContext = Ctx("ops-cs-1", "ops-cs");
            await controller.Create(MinimalRequest(), CancellationToken.None);

            Assert.False(await harness.Published.Any<BookingInitiated>());
        }
        finally
        {
            await harness.Stop();
        }
    }

    [Fact]
    [Trait("Category", "Phase06")]
    public async Task duplicate_supplier_reference_within_24h_returns_409_problem()
    {
        var (controller, db, events, harness) = await BuildAsync();
        try
        {
            controller.ControllerContext = Ctx("ops-cs-1", "ops-cs");
            await controller.Create(MinimalRequest(supplierRef: "DUP-001", bookingRef: "TBE-MAN-A"), CancellationToken.None);

            var second = await controller.Create(
                MinimalRequest(supplierRef: "DUP-001", bookingRef: "TBE-MAN-B"),
                CancellationToken.None);

            var problem = Assert.IsType<ObjectResult>(second);
            Assert.Equal(StatusCodes.Status409Conflict, problem.StatusCode);
            var details = Assert.IsAssignableFrom<ProblemDetails>(problem.Value);
            Assert.Equal("/errors/duplicate-supplier-reference", details.Type);
        }
        finally
        {
            await harness.Stop();
        }
    }

    [Fact]
    [Trait("Category", "Phase06")]
    public async Task missing_preferred_username_returns_401()
    {
        var (controller, db, events, harness) = await BuildAsync();
        try
        {
            // No preferred_username claim — actor cannot be resolved.
            controller.ControllerContext = new ControllerContext
            {
                HttpContext = new DefaultHttpContext
                {
                    User = new ClaimsPrincipal(new ClaimsIdentity(
                        new[] { new Claim(ClaimTypes.Role, "ops-cs") },
                        "test")),
                },
            };

            var result = await controller.Create(MinimalRequest(), CancellationToken.None);
            var unauthorized = Assert.IsType<ObjectResult>(result);
            Assert.Equal(StatusCodes.Status401Unauthorized, unauthorized.StatusCode);
        }
        finally
        {
            await harness.Stop();
        }
    }

    [Fact]
    [Trait("Category", "Phase06")]
    public async Task negative_fare_amount_returns_400_problem()
    {
        var (controller, db, events, harness) = await BuildAsync();
        try
        {
            controller.ControllerContext = Ctx("ops-cs-1", "ops-cs");
            var bad = MinimalRequest() with { BaseFareAmount = -1m };
            var result = await controller.Create(bad, CancellationToken.None);

            var problem = Assert.IsType<ObjectResult>(result);
            Assert.Equal(StatusCodes.Status400BadRequest, problem.StatusCode);
            var details = Assert.IsAssignableFrom<ProblemDetails>(problem.Value);
            Assert.Equal("/errors/manual-booking-invalid-amount", details.Type);
        }
        finally
        {
            await harness.Stop();
        }
    }

    [Fact]
    [Trait("Category", "Phase06")]
    public async Task empty_itinerary_json_returns_400_problem()
    {
        var (controller, db, events, harness) = await BuildAsync();
        try
        {
            controller.ControllerContext = Ctx("ops-cs-1", "ops-cs");
            var bad = MinimalRequest() with { ItineraryJson = "" };
            var result = await controller.Create(bad, CancellationToken.None);

            var problem = Assert.IsType<ObjectResult>(result);
            Assert.Equal(StatusCodes.Status400BadRequest, problem.StatusCode);
            var details = Assert.IsAssignableFrom<ProblemDetails>(problem.Value);
            Assert.Equal("/errors/manual-booking-invalid-itinerary", details.Type);
        }
        finally
        {
            await harness.Stop();
        }
    }
}
