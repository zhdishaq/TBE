using System.Security.Claims;
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
/// Plan 05-02 Task 2 — controller contract tests for <see cref="AgentBookingsController"/>.
/// Exercises:
///   - D-35 agent-readonly write gate (POST must 403)
///   - D-37 admin-only markup-override (POST with override + non-admin must 403)
///   - T-05-02-01 / T-05-02-08 server-side agency_id stamping from JWT claim
///   - BookingInitiated.Channel == "b2b" server-stamp + AgentBookingDetailsCaptured
///     carrying the frozen agency pricing (D-36 / D-41) + customer snapshot (B2B-04)
///   - D-34 agency-wide list: filter by agency_id ONLY (never additionally by sub)
///   - Pitfall 26 missing agency_id claim hard-fails 401
///   - IDOR guard on GET /agent/bookings/{id}: cross-tenant 403
/// </summary>
public class AgentBookingsControllerTests
{
    private static readonly Guid AgencyIdA = Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaa1");
    private static readonly Guid AgencyIdB = Guid.Parse("bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbb2");
    private const string AgentSub = "agent-sub-xyz";
    private const string OtherAgentSub = "agent-sub-other";

    private static BookingDbContext NewDb() =>
        new(new DbContextOptionsBuilder<BookingDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options);

    private static AgentBookingsController NewController(
        BookingDbContext db,
        IPublishEndpoint publishEndpoint,
        Guid? agencyId,
        string? sub,
        params string[] roles)
    {
        var claims = new List<Claim>();
        if (!string.IsNullOrEmpty(sub))
        {
            claims.Add(new Claim(ClaimTypes.NameIdentifier, sub));
            claims.Add(new Claim("sub", sub));
        }
        if (agencyId is not null)
            claims.Add(new Claim("agency_id", agencyId.Value.ToString()));
        foreach (var r in roles)
            claims.Add(new Claim("roles", r));

        var identity = claims.Count == 0
            ? new ClaimsIdentity()
            : new ClaimsIdentity(claims, authenticationType: "jwt");
        var user = new ClaimsPrincipal(identity);

        return new AgentBookingsController(db, publishEndpoint, NullLogger<AgentBookingsController>.Instance)
        {
            ControllerContext = new ControllerContext
            {
                HttpContext = new DefaultHttpContext { User = user },
            },
        };
    }

    private static CreateAgentBookingRequest ValidRequest(decimal? overrideAmount = null) => new(
        ProductType: "flight",
        OfferId: "offer-123",
        AgencyNetFare: 200m,
        AgencyMarkupAmount: 50m,
        AgencyGrossAmount: 250m,
        AgencyCommissionAmount: 50m,
        AgencyMarkupOverride: overrideAmount,
        Currency: "GBP",
        WalletId: Guid.NewGuid(),
        CustomerName: "Jane Customer",
        CustomerEmail: "jane@example.com",
        CustomerPhone: "+441234567890");

    // ------------------------------------------------------------------
    // POST /agent/bookings
    // ------------------------------------------------------------------

    [Fact]
    public async Task CreateAsync_with_agent_readonly_role_returns_403()
    {
        await using var db = NewDb();
        var publish = Substitute.For<IPublishEndpoint>();
        var controller = NewController(db, publish, AgencyIdA, AgentSub, roles: "agent-readonly");

        var result = await controller.CreateAsync(ValidRequest(), CancellationToken.None);

        result.Should().BeOfType<ForbidResult>();
        await publish.DidNotReceive().Publish(Arg.Any<BookingInitiated>(), Arg.Any<CancellationToken>());
        await publish.DidNotReceive().Publish(Arg.Any<AgentBookingDetailsCaptured>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task CreateAsync_with_markup_override_as_non_admin_returns_403()
    {
        await using var db = NewDb();
        var publish = Substitute.For<IPublishEndpoint>();
        var controller = NewController(db, publish, AgencyIdA, AgentSub, roles: "agent");

        var result = await controller.CreateAsync(ValidRequest(overrideAmount: 75m), CancellationToken.None);

        result.Should().BeOfType<ForbidResult>();
        await publish.DidNotReceive().Publish(Arg.Any<BookingInitiated>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task CreateAsync_with_markup_override_as_agent_admin_is_accepted()
    {
        await using var db = NewDb();
        var publish = Substitute.For<IPublishEndpoint>();
        var controller = NewController(db, publish, AgencyIdA, AgentSub, roles: "agent-admin");

        var result = await controller.CreateAsync(ValidRequest(overrideAmount: 75m), CancellationToken.None);

        result.Should().BeOfType<AcceptedAtActionResult>();
        await publish.Received(1).Publish(
            Arg.Is<AgentBookingDetailsCaptured>(m => m.AgencyId == AgencyIdA && m.AgencyMarkupOverride == 75m),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task CreateAsync_publishes_BookingInitiated_with_Channel_b2b_and_agency_from_claim_not_body()
    {
        await using var db = NewDb();
        var publish = Substitute.For<IPublishEndpoint>();
        var controller = NewController(db, publish, AgencyIdA, AgentSub, roles: "agent");

        var result = await controller.CreateAsync(ValidRequest(), CancellationToken.None);

        result.Should().BeOfType<AcceptedAtActionResult>();
        await publish.Received(1).Publish(
            Arg.Is<BookingInitiated>(m => m.Channel == "b2b" && m.UserId == AgentSub && m.PaymentMethod == "wallet"),
            Arg.Any<CancellationToken>());
        await publish.Received(1).Publish(
            Arg.Is<AgentBookingDetailsCaptured>(m =>
                m.AgencyId == AgencyIdA
                && m.AgencyNetFare == 200m
                && m.AgencyGrossAmount == 250m
                && m.AgencyMarkupAmount == 50m
                && m.CustomerEmail == "jane@example.com"),
            Arg.Any<CancellationToken>());

        // T-05-02-01: the CreateAgentBookingRequest DTO must NOT expose an AgencyId property —
        // we verify this at the type level so the controller can never be "fixed" to read it.
        typeof(CreateAgentBookingRequest).GetProperty("AgencyId").Should().BeNull(
            "AgencyId must be server-stamped from the JWT claim, never accepted from the body");
        typeof(CreateAgentBookingRequest).GetProperty("Channel").Should().BeNull(
            "Channel must be server-stamped to b2b, never accepted from the body");
    }

    [Fact]
    public async Task CreateAsync_without_agency_id_claim_returns_401()
    {
        await using var db = NewDb();
        var publish = Substitute.For<IPublishEndpoint>();
        var controller = NewController(db, publish, agencyId: null, sub: AgentSub, roles: "agent");

        var result = await controller.CreateAsync(ValidRequest(), CancellationToken.None);

        result.Should().BeOfType<UnauthorizedObjectResult>();
        await publish.DidNotReceive().Publish(Arg.Any<BookingInitiated>(), Arg.Any<CancellationToken>());
    }

    // ------------------------------------------------------------------
    // GET /agent/bookings/me
    // ------------------------------------------------------------------

    [Fact]
    public async Task ListForAgencyAsync_scopes_by_agency_id_only_not_sub()
    {
        await using var db = NewDb();
        // Two sagas under the same agency but different agent sub; D-34 requires the
        // listing to return BOTH rows regardless of which agent called.
        db.BookingSagaStates.Add(new BookingSagaState
        {
            CorrelationId = Guid.NewGuid(),
            AgencyId = AgencyIdA,
            UserId = AgentSub,
            ChannelText = "b2b",
            BookingReference = "TBE-260520-AAA",
            ProductType = "flight",
            Currency = "GBP",
            PaymentMethod = "wallet",
            InitiatedAtUtc = DateTime.UtcNow.AddMinutes(-5),
        });
        db.BookingSagaStates.Add(new BookingSagaState
        {
            CorrelationId = Guid.NewGuid(),
            AgencyId = AgencyIdA,
            UserId = OtherAgentSub,
            ChannelText = "b2b",
            BookingReference = "TBE-260520-BBB",
            ProductType = "flight",
            Currency = "GBP",
            PaymentMethod = "wallet",
            InitiatedAtUtc = DateTime.UtcNow,
        });
        // Different agency — must not be returned (cross-tenant isolation).
        db.BookingSagaStates.Add(new BookingSagaState
        {
            CorrelationId = Guid.NewGuid(),
            AgencyId = AgencyIdB,
            UserId = "agent-other-tenant",
            ChannelText = "b2b",
            BookingReference = "TBE-260520-ZZZ",
            ProductType = "flight",
            Currency = "GBP",
            PaymentMethod = "wallet",
            InitiatedAtUtc = DateTime.UtcNow,
        });
        await db.SaveChangesAsync();

        var publish = Substitute.For<IPublishEndpoint>();
        var controller = NewController(db, publish, AgencyIdA, AgentSub, roles: "agent");

        var result = await controller.ListForAgencyAsync(page: 1, size: 20, client: null, pnr: null, CancellationToken.None);

        var ok = result.Should().BeOfType<OkObjectResult>().Subject;
        var body = ok.Value!;
        var itemsProp = body.GetType().GetProperty("items")!;
        var items = (System.Collections.IEnumerable)itemsProp.GetValue(body)!;
        var list = items.Cast<object>().ToList();
        list.Should().HaveCount(2, "D-34 — agency-wide list returns all bookings under the claim's agency regardless of agent sub");
    }

    [Fact]
    public async Task ListForAgencyAsync_without_agency_id_claim_returns_401()
    {
        await using var db = NewDb();
        var publish = Substitute.For<IPublishEndpoint>();
        var controller = NewController(db, publish, agencyId: null, sub: AgentSub, roles: "agent");

        var result = await controller.ListForAgencyAsync(page: 1, size: 20, client: null, pnr: null, CancellationToken.None);

        result.Should().BeOfType<UnauthorizedObjectResult>();
    }

    // ------------------------------------------------------------------
    // GET /agent/bookings/{id} — IDOR guard
    // ------------------------------------------------------------------

    [Fact]
    public async Task GetByIdAsync_returns_403_when_booking_belongs_to_different_agency()
    {
        await using var db = NewDb();
        var crossTenantBookingId = Guid.NewGuid();
        db.BookingSagaStates.Add(new BookingSagaState
        {
            CorrelationId = crossTenantBookingId,
            AgencyId = AgencyIdB,                     // belongs to tenant B
            UserId = "agent-other-tenant",
            ChannelText = "b2b",
            BookingReference = "TBE-260520-XTEN",
            ProductType = "flight",
            Currency = "GBP",
            PaymentMethod = "wallet",
            InitiatedAtUtc = DateTime.UtcNow,
        });
        await db.SaveChangesAsync();

        var publish = Substitute.For<IPublishEndpoint>();
        // Caller belongs to tenant A.
        var controller = NewController(db, publish, AgencyIdA, AgentSub, roles: "agent");

        var result = await controller.GetByIdAsync(crossTenantBookingId, CancellationToken.None);

        result.Should().BeOfType<ForbidResult>();
    }

    // ------------------------------------------------------------------
    // Plan 05-04 Task 1 — POST /agent/bookings/{id}/void  (B2B-10)
    // ------------------------------------------------------------------

    [Fact]
    public async Task VoidAsync_returns_403_when_caller_is_not_agent_admin()
    {
        await using var db = NewDb();
        var bookingId = Guid.NewGuid();
        db.BookingSagaStates.Add(new BookingSagaState
        {
            CorrelationId = bookingId,
            AgencyId = AgencyIdA,
            UserId = AgentSub,
            ChannelText = "b2b",
            BookingReference = "TBE-260520-VOID",
            ProductType = "flight",
            Currency = "GBP",
            PaymentMethod = "wallet",
            GdsPnr = "ABC123",
            TicketNumber = null,
            InitiatedAtUtc = DateTime.UtcNow,
        });
        await db.SaveChangesAsync();

        var publish = Substitute.For<IPublishEndpoint>();
        var controller = NewController(db, publish, AgencyIdA, AgentSub, roles: "agent");

        var result = await controller.VoidAsync(bookingId, CancellationToken.None);

        result.Should().BeOfType<ForbidResult>();
        await publish.DidNotReceive().Publish(Arg.Any<TBE.Contracts.Events.VoidRequested>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task VoidAsync_returns_404_when_booking_belongs_to_different_agency()
    {
        // Pitfall 10 — cross-tenant void must 404, NEVER 403 (never leak existence).
        await using var db = NewDb();
        var crossTenantBookingId = Guid.NewGuid();
        db.BookingSagaStates.Add(new BookingSagaState
        {
            CorrelationId = crossTenantBookingId,
            AgencyId = AgencyIdB,
            UserId = "agent-other-tenant",
            ChannelText = "b2b",
            BookingReference = "TBE-260520-XTEN",
            ProductType = "flight",
            Currency = "GBP",
            PaymentMethod = "wallet",
            GdsPnr = "ABC123",
            InitiatedAtUtc = DateTime.UtcNow,
        });
        await db.SaveChangesAsync();

        var publish = Substitute.For<IPublishEndpoint>();
        var controller = NewController(db, publish, AgencyIdA, AgentSub, roles: "agent-admin");

        var result = await controller.VoidAsync(crossTenantBookingId, CancellationToken.None);

        result.Should().BeOfType<NotFoundResult>(
            "Pitfall 10 — cross-tenant voids must 404 (never leak existence)");
        await publish.DidNotReceive().Publish(Arg.Any<TBE.Contracts.Events.VoidRequested>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task VoidAsync_returns_404_when_booking_does_not_exist()
    {
        await using var db = NewDb();
        var publish = Substitute.For<IPublishEndpoint>();
        var controller = NewController(db, publish, AgencyIdA, AgentSub, roles: "agent-admin");

        var result = await controller.VoidAsync(Guid.NewGuid(), CancellationToken.None);

        result.Should().BeOfType<NotFoundResult>();
    }

    [Fact]
    public async Task VoidAsync_returns_409_problem_json_when_booking_already_ticketed_post_ticket()
    {
        // D-39 — post-ticket voids are refused with 409 Conflict + RFC 7807
        // problem+json type "/errors/post-ticket-cancel-unsupported".
        await using var db = NewDb();
        var bookingId = Guid.NewGuid();
        db.BookingSagaStates.Add(new BookingSagaState
        {
            CorrelationId = bookingId,
            AgencyId = AgencyIdA,
            UserId = AgentSub,
            ChannelText = "b2b",
            BookingReference = "TBE-260520-TICKD",
            ProductType = "flight",
            Currency = "GBP",
            PaymentMethod = "wallet",
            GdsPnr = "ABC123",
            TicketNumber = "TKT0001234567",              // POST-TICKET → 409
            InitiatedAtUtc = DateTime.UtcNow.AddHours(-1),
        });
        await db.SaveChangesAsync();

        var publish = Substitute.For<IPublishEndpoint>();
        var controller = NewController(db, publish, AgencyIdA, AgentSub, roles: "agent-admin");

        var result = await controller.VoidAsync(bookingId, CancellationToken.None);

        // ObjectResult with status 409 carrying ProblemDetails + content-type application/problem+json.
        var obj = result.Should().BeOfType<ObjectResult>().Subject;
        obj.StatusCode.Should().Be(StatusCodes.Status409Conflict);
        obj.ContentTypes.Should().Contain("application/problem+json");
        var problem = obj.Value.Should().BeOfType<Microsoft.AspNetCore.Mvc.ProblemDetails>().Subject;
        problem.Type.Should().Contain("post-ticket-cancel-unsupported");

        await publish.DidNotReceive().Publish(Arg.Any<TBE.Contracts.Events.VoidRequested>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task VoidAsync_pre_ticket_publishes_VoidRequested_and_returns_202()
    {
        // B2B-10 happy path: pre-ticket (no TicketNumber) admin-initiated void.
        await using var db = NewDb();
        var bookingId = Guid.NewGuid();
        db.BookingSagaStates.Add(new BookingSagaState
        {
            CorrelationId = bookingId,
            AgencyId = AgencyIdA,
            UserId = AgentSub,
            ChannelText = "b2b",
            BookingReference = "TBE-260520-VRQ",
            ProductType = "flight",
            Currency = "GBP",
            PaymentMethod = "wallet",
            GdsPnr = "ABC123",
            TicketNumber = null,                         // PRE-TICKET
            InitiatedAtUtc = DateTime.UtcNow,
        });
        await db.SaveChangesAsync();

        var publish = Substitute.For<IPublishEndpoint>();
        var controller = NewController(db, publish, AgencyIdA, AgentSub, roles: "agent-admin");

        var result = await controller.VoidAsync(bookingId, CancellationToken.None);

        result.Should().BeOfType<AcceptedAtActionResult>();
        await publish.Received(1).Publish(
            Arg.Is<TBE.Contracts.Events.VoidRequested>(m =>
                m.BookingId == bookingId
                && m.RequestedByUserId == AgentSub),
            Arg.Any<CancellationToken>());
    }

    // ------------------------------------------------------------------
    // Plan 05-04 Task 1 — GET /agent/bookings/me filter + sort extensions
    // ------------------------------------------------------------------

    [Fact]
    public async Task ListForAgencyAsync_filters_by_client_name_contains_case_insensitive()
    {
        await using var db = NewDb();
        db.BookingSagaStates.Add(new BookingSagaState
        {
            CorrelationId = Guid.NewGuid(),
            AgencyId = AgencyIdA,
            UserId = AgentSub,
            ChannelText = "b2b",
            BookingReference = "TBE-260520-SMITH",
            ProductType = "flight",
            Currency = "GBP",
            PaymentMethod = "wallet",
            CustomerName = "John Smith",
            InitiatedAtUtc = DateTime.UtcNow.AddMinutes(-3),
        });
        db.BookingSagaStates.Add(new BookingSagaState
        {
            CorrelationId = Guid.NewGuid(),
            AgencyId = AgencyIdA,
            UserId = AgentSub,
            ChannelText = "b2b",
            BookingReference = "TBE-260520-DOE",
            ProductType = "flight",
            Currency = "GBP",
            PaymentMethod = "wallet",
            CustomerName = "Jane Doe",
            InitiatedAtUtc = DateTime.UtcNow,
        });
        await db.SaveChangesAsync();

        var publish = Substitute.For<IPublishEndpoint>();
        var controller = NewController(db, publish, AgencyIdA, AgentSub, roles: "agent");

        var result = await controller.ListForAgencyAsync(
            page: 1, size: 20, client: "smith", pnr: null, ct: CancellationToken.None);

        var items = ExtractItems(result);
        items.Should().HaveCount(1, "client=smith filters to the matching client name");
    }

    [Fact]
    public async Task ListForAgencyAsync_filters_by_pnr_equals()
    {
        await using var db = NewDb();
        db.BookingSagaStates.Add(new BookingSagaState
        {
            CorrelationId = Guid.NewGuid(),
            AgencyId = AgencyIdA, UserId = AgentSub, ChannelText = "b2b",
            BookingReference = "TBE-X1", ProductType = "flight",
            Currency = "GBP", PaymentMethod = "wallet",
            GdsPnr = "ABC123",
            InitiatedAtUtc = DateTime.UtcNow,
        });
        db.BookingSagaStates.Add(new BookingSagaState
        {
            CorrelationId = Guid.NewGuid(),
            AgencyId = AgencyIdA, UserId = AgentSub, ChannelText = "b2b",
            BookingReference = "TBE-X2", ProductType = "flight",
            Currency = "GBP", PaymentMethod = "wallet",
            GdsPnr = "ZZZ999",
            InitiatedAtUtc = DateTime.UtcNow,
        });
        await db.SaveChangesAsync();

        var publish = Substitute.For<IPublishEndpoint>();
        var controller = NewController(db, publish, AgencyIdA, AgentSub, roles: "agent");

        var result = await controller.ListForAgencyAsync(
            page: 1, size: 20, client: null, pnr: "ABC123", ct: CancellationToken.None);

        var items = ExtractItems(result);
        items.Should().HaveCount(1);
    }

    [Fact]
    public async Task ListForAgencyAsync_clamps_page_size_between_20_and_100()
    {
        await using var db = NewDb();
        var publish = Substitute.For<IPublishEndpoint>();
        var controller = NewController(db, publish, AgencyIdA, AgentSub, roles: "agent");

        // Oversized page size (200) should clamp to 100. Undersized (5) clamps
        // to 20. Both must resolve without error and respect the documented bounds.
        var tooBig = await controller.ListForAgencyAsync(page: 1, size: 200, client: null, pnr: null, ct: CancellationToken.None);
        var tooSmall = await controller.ListForAgencyAsync(page: 1, size: 5, client: null, pnr: null, ct: CancellationToken.None);

        ExtractSize(tooBig).Should().Be(100);
        ExtractSize(tooSmall).Should().Be(20);
    }

    private static System.Collections.Generic.List<object> ExtractItems(IActionResult result)
    {
        var ok = result.Should().BeOfType<OkObjectResult>().Subject;
        var body = ok.Value!;
        var itemsProp = body.GetType().GetProperty("items")!;
        var items = (System.Collections.IEnumerable)itemsProp.GetValue(body)!;
        return items.Cast<object>().ToList();
    }

    private static int ExtractSize(IActionResult result)
    {
        var ok = result.Should().BeOfType<OkObjectResult>().Subject;
        var body = ok.Value!;
        var sizeProp = body.GetType().GetProperty("size")!;
        return (int)sizeProp.GetValue(body)!;
    }
}
