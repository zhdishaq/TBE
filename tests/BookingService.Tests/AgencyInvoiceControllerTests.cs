using System.Security.Claims;
using FluentAssertions;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;
using TBE.BookingService.API.Controllers;
using TBE.BookingService.Application.Pdf;
using TBE.BookingService.Application.Saga;
using TBE.BookingService.Infrastructure;
using Xunit;

namespace TBE.BookingService.Tests;

/// <summary>
/// Plan 05-04 Task 2 (B2B-08) — InvoicesController IDOR / auth gates.
///
/// <para>
/// Replaces RED placeholder in
/// <c>tests/Notifications.Tests/AgencyInvoiceControllerTests.cs</c>. The
/// placeholder returned <b>403</b> on cross-tenant access, but the plan's
/// Pitfall 10 mandates <b>404</b> (never 403) to avoid leaking existence of a
/// foreign-agency booking. These tests hold the plan's contract as
/// authoritative.
/// </para>
///
/// <para>
/// <b>Covered gates:</b>
/// <list type="bullet">
///   <item>Pitfall 28 — missing agency_id claim → 401 Unauthorized (fail-closed).</item>
///   <item>Pitfall 10 — unknown booking → 404 NotFound.</item>
///   <item>Pitfall 10 — cross-tenant (booking.AgencyId != caller's agency_id) → 404 NotFound (never 403).</item>
///   <item>Happy path — owner agency → application/pdf FileContentResult.</item>
/// </list>
/// </para>
/// </summary>
public class AgencyInvoiceControllerTests
{
    private static readonly Guid BookingId = Guid.Parse("11111111-1111-1111-1111-111111111111");
    private static readonly Guid OwnerAgencyId = Guid.Parse("44444444-4444-4444-4444-444444444444");
    private static readonly Guid OtherAgencyId = Guid.Parse("55555555-5555-5555-5555-555555555555");

    [Fact]
    public async Task GetInvoice_returns_PDF_for_owner_agency()
    {
        await using var db = NewDb();
        SeedBooking(db, OwnerAgencyId);
        var pdf = Substitute.For<IAgencyInvoicePdfGenerator>();
        pdf.GenerateAsync(Arg.Any<BookingSagaState>(), Arg.Any<CancellationToken>())
            .Returns(new byte[] { 0x25, 0x50, 0x44, 0x46 }); // %PDF

        var controller = NewController(db, pdf, agencyIdClaim: OwnerAgencyId.ToString());

        var result = await controller.GetInvoiceAsync(BookingId, CancellationToken.None);

        var file = result.Should().BeOfType<FileContentResult>().Subject;
        file.ContentType.Should().Be("application/pdf");
        file.FileDownloadName.Should().Contain("invoice-TBE-260416-ABCDEF01");
        file.FileContents.Should().NotBeEmpty();
    }

    /// <summary>Pitfall 10 — cross-tenant caller gets 404 (NOT 403). A 403 would leak booking existence.</summary>
    [Fact]
    public async Task GetInvoice_returns_404_when_booking_belongs_to_different_agency()
    {
        await using var db = NewDb();
        SeedBooking(db, OwnerAgencyId);
        var pdf = Substitute.For<IAgencyInvoicePdfGenerator>();

        var controller = NewController(db, pdf, agencyIdClaim: OtherAgencyId.ToString());

        var result = await controller.GetInvoiceAsync(BookingId, CancellationToken.None);

        result.Should().BeOfType<NotFoundResult>(
            "Pitfall 10 — cross-tenant access MUST return 404 NotFound, never 403 Forbidden");
        await pdf.DidNotReceive().GenerateAsync(Arg.Any<BookingSagaState>(), Arg.Any<CancellationToken>());
    }

    /// <summary>Pitfall 10 — unknown id gets 404, same branch as cross-tenant (indistinguishable externally).</summary>
    [Fact]
    public async Task GetInvoice_returns_404_for_unknown_booking()
    {
        await using var db = NewDb();
        var pdf = Substitute.For<IAgencyInvoicePdfGenerator>();

        var controller = NewController(db, pdf, agencyIdClaim: OwnerAgencyId.ToString());

        var result = await controller.GetInvoiceAsync(Guid.NewGuid(), CancellationToken.None);

        result.Should().BeOfType<NotFoundResult>();
        await pdf.DidNotReceive().GenerateAsync(Arg.Any<BookingSagaState>(), Arg.Any<CancellationToken>());
    }

    /// <summary>Pitfall 28 — fail-closed 401 when agency_id claim is missing from the JWT.</summary>
    [Fact]
    public async Task GetInvoice_returns_401_when_agency_id_claim_missing()
    {
        await using var db = NewDb();
        SeedBooking(db, OwnerAgencyId);
        var pdf = Substitute.For<IAgencyInvoicePdfGenerator>();

        var controller = NewController(db, pdf, agencyIdClaim: null);

        var result = await controller.GetInvoiceAsync(BookingId, CancellationToken.None);

        result.Should().BeOfType<UnauthorizedObjectResult>(
            "Pitfall 28 — missing agency_id claim MUST fail closed with 401, never fall back");
        await pdf.DidNotReceive().GenerateAsync(Arg.Any<BookingSagaState>(), Arg.Any<CancellationToken>());
    }

    /// <summary>Pitfall 28 — malformed agency_id claim (not a Guid) also fails closed with 401.</summary>
    [Fact]
    public async Task GetInvoice_returns_401_when_agency_id_claim_is_malformed()
    {
        await using var db = NewDb();
        SeedBooking(db, OwnerAgencyId);
        var pdf = Substitute.For<IAgencyInvoicePdfGenerator>();

        var controller = NewController(db, pdf, agencyIdClaim: "not-a-guid");

        var result = await controller.GetInvoiceAsync(BookingId, CancellationToken.None);

        result.Should().BeOfType<UnauthorizedObjectResult>();
    }

    // ---- helpers ---------------------------------------------------------

    private static BookingDbContext NewDb()
    {
        var options = new DbContextOptionsBuilder<BookingDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        return new BookingDbContext(options);
    }

    private static void SeedBooking(BookingDbContext db, Guid agencyId)
    {
        db.BookingSagaStates.Add(new BookingSagaState
        {
            CorrelationId = BookingId,
            AgencyId = agencyId,
            UserId = "agent-bob",
            BookingReference = "TBE-260416-ABCDEF01",
            ProductType = "flight",
            ChannelText = "b2b",
            Currency = "GBP",
            PaymentMethod = "wallet",
            TotalAmount = 480m,
            AgencyGrossAmount = 480m,
            GdsPnr = "PNR777",
            TicketNumber = "125-9876543210",
            CustomerName = "Jane Doe",
            CustomerEmail = "jane@example.com",
            InitiatedAtUtc = DateTime.UtcNow,
            TicketingDeadlineUtc = DateTime.UtcNow.AddHours(24),
        });
        db.SaveChanges();
    }

    private static InvoicesController NewController(
        BookingDbContext db,
        IAgencyInvoicePdfGenerator pdf,
        string? agencyIdClaim)
    {
        var claims = new List<Claim>
        {
            new(ClaimTypes.NameIdentifier, "agent-bob"),
            new("sub", "agent-bob"),
        };
        if (!string.IsNullOrEmpty(agencyIdClaim))
            claims.Add(new Claim("agency_id", agencyIdClaim));

        var identity = new ClaimsIdentity(claims, authenticationType: "jwt");
        var user = new ClaimsPrincipal(identity);

        var controller = new InvoicesController(db, pdf, NullLogger<InvoicesController>.Instance)
        {
            ControllerContext = new ControllerContext
            {
                HttpContext = new DefaultHttpContext { User = user },
            },
        };
        return controller;
    }
}
