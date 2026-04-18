using System.Security.Claims;
using FluentAssertions;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using TBE.BookingService.API.Controllers;
using TBE.BookingService.Application.Saga;
using TBE.BookingService.Infrastructure;
using TBE.Contracts.Enums;
using Xunit;

namespace TBE.BookingService.Tests;

/// <summary>
/// Plan 05-04 Task 1 — <see cref="AgencyDashboardController"/> returns a single
/// summary DTO for the caller's agency covering TTL alert counts + recent bookings.
///
/// Enforces:
/// - D-34 agency-wide filter — counts and recent-bookings scoped by <c>agency_id</c>
///   claim ONLY, never additionally by <c>sub</c>.
/// - Pitfall 28 — missing <c>agency_id</c> claim hard-fails 401 (fail-closed).
/// - T-05-04-10 cross-tenant isolation — agency A must never see agency B's counts.
/// - Recent bookings are capped at 5 rows (plan's "top 5 by CreatedAt desc").
/// </summary>
public class AgencyDashboardControllerTests
{
    private static readonly Guid AgencyIdA = Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaa1");
    private static readonly Guid AgencyIdB = Guid.Parse("bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbb2");
    private const string AgentSubA = "agent-sub-a";

    private static BookingDbContext NewDb() =>
        new(new DbContextOptionsBuilder<BookingDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options);

    private static AgencyDashboardController NewController(
        BookingDbContext db,
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

        return new AgencyDashboardController(db, NullLogger<AgencyDashboardController>.Instance)
        {
            ControllerContext = new ControllerContext
            {
                HttpContext = new DefaultHttpContext { User = user },
            },
        };
    }

    private static BookingSagaState MakeSaga(Guid agencyId, string reference, DateTime? ticketingDeadline = null, string? state = null)
        => new()
        {
            CorrelationId = Guid.NewGuid(),
            AgencyId = agencyId,
            Channel = Channel.B2B,
            ChannelText = "b2b",
            BookingReference = reference,
            ProductType = "flight",
            Currency = "GBP",
            PaymentMethod = "wallet",
            GdsPnr = "PNR" + reference[^3..],
            TicketingDeadlineUtc = ticketingDeadline ?? DateTime.UtcNow.AddDays(7),
            InitiatedAtUtc = DateTime.UtcNow,
            UserId = "agent-x",
            CustomerName = "Jane Customer",
            AgencyGrossAmount = 250m,
            TotalAmount = 250m,
        };

    [Fact]
    public async Task GetSummaryAsync_without_agency_id_claim_returns_401()
    {
        await using var db = NewDb();
        var controller = NewController(db, agencyId: null, sub: AgentSubA, roles: "agent");

        var result = await controller.GetSummaryAsync(CancellationToken.None);

        result.Should().BeOfType<UnauthorizedObjectResult>();
    }

    [Fact]
    public async Task GetSummaryAsync_scopes_by_agency_id_only_not_sub_D34()
    {
        await using var db = NewDb();
        // Agency A — one booking with TTL < 2h (urgent)
        db.BookingSagaStates.Add(MakeSaga(AgencyIdA, "TBE-A-001", DateTime.UtcNow.AddMinutes(90)));
        // Agency A — one booking with TTL < 24h (warn)
        db.BookingSagaStates.Add(MakeSaga(AgencyIdA, "TBE-A-002", DateTime.UtcNow.AddHours(20)));
        // Agency B — booking with TTL < 2h must NOT appear in Agency A's counts
        db.BookingSagaStates.Add(MakeSaga(AgencyIdB, "TBE-B-001", DateTime.UtcNow.AddMinutes(60)));
        await db.SaveChangesAsync();

        var controller = NewController(db, AgencyIdA, AgentSubA, roles: "agent");
        var result = await controller.GetSummaryAsync(CancellationToken.None);

        var ok = result.Should().BeOfType<OkObjectResult>().Subject;
        var dto = ok.Value!;
        var urgent = (int)dto.GetType().GetProperty("UrgentTtlCount")!.GetValue(dto)!;
        var warn = (int)dto.GetType().GetProperty("Warning24hTtlCount")!.GetValue(dto)!;

        urgent.Should().Be(1, "only Agency A's urgent booking counts");
        warn.Should().Be(1, "only Agency A's warning booking counts");
    }

    [Fact]
    public async Task GetSummaryAsync_recent_bookings_capped_at_5()
    {
        await using var db = NewDb();
        for (int i = 0; i < 7; i++)
        {
            var s = MakeSaga(AgencyIdA, $"TBE-A-{i:000}");
            s.InitiatedAtUtc = DateTime.UtcNow.AddMinutes(-i);
            db.BookingSagaStates.Add(s);
        }
        await db.SaveChangesAsync();

        var controller = NewController(db, AgencyIdA, AgentSubA, roles: "agent");
        var result = await controller.GetSummaryAsync(CancellationToken.None);

        var ok = result.Should().BeOfType<OkObjectResult>().Subject;
        var dto = ok.Value!;
        var recent = (System.Collections.IEnumerable)dto.GetType().GetProperty("RecentBookings")!.GetValue(dto)!;
        recent.Cast<object>().Should().HaveCount(5);
    }
}
