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
/// Plan 04-01 Task 1 — covers CONTEXT D-15 (QuestPDF receipt from persisted
/// saga state) and threat T-04-01-01 (IDOR on GET /bookings/{id}/receipt.pdf).
/// </summary>
public class ReceiptsControllerTests
{
    private const string OwnerUserId = "user-owner-abc";
    private const string OtherUserId = "user-other-xyz";
    private static readonly Guid BookingId = Guid.Parse("11111111-1111-1111-1111-111111111111");

    [Fact]
    public async Task GetReceipt_returns_PDF_for_owner()
    {
        await using var db = NewDb();
        SeedBooking(db, OwnerUserId);
        var pdf = Substitute.For<IBookingReceiptPdfGenerator>();
        pdf.GenerateAsync(Arg.Any<BookingSagaState>(), Arg.Any<CancellationToken>())
            .Returns(new byte[] { 0x25, 0x50, 0x44, 0x46 }); // %PDF

        var controller = NewController(db, pdf, userId: OwnerUserId, isBackoffice: false);

        var result = await controller.GetReceiptAsync(BookingId, CancellationToken.None);

        var file = result.Should().BeOfType<FileContentResult>().Subject;
        file.ContentType.Should().Be("application/pdf");
        file.FileDownloadName.Should().Contain("receipt-TBE-260416-ABCDEF01");
        file.FileContents.Should().NotBeEmpty();
    }

    [Fact]
    public async Task GetReceipt_returns_403_for_other_user()
    {
        await using var db = NewDb();
        SeedBooking(db, OwnerUserId);
        var pdf = Substitute.For<IBookingReceiptPdfGenerator>();

        var controller = NewController(db, pdf, userId: OtherUserId, isBackoffice: false);

        var result = await controller.GetReceiptAsync(BookingId, CancellationToken.None);

        result.Should().BeOfType<ForbidResult>();
        await pdf.DidNotReceive().GenerateAsync(Arg.Any<BookingSagaState>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task GetReceipt_returns_200_for_backoffice_even_if_not_owner()
    {
        await using var db = NewDb();
        SeedBooking(db, OwnerUserId);
        var pdf = Substitute.For<IBookingReceiptPdfGenerator>();
        pdf.GenerateAsync(Arg.Any<BookingSagaState>(), Arg.Any<CancellationToken>())
            .Returns(new byte[] { 0x25, 0x50, 0x44, 0x46 });

        var controller = NewController(db, pdf, userId: OtherUserId, isBackoffice: true);

        var result = await controller.GetReceiptAsync(BookingId, CancellationToken.None);

        result.Should().BeOfType<FileContentResult>();
    }

    [Fact]
    public async Task GetReceipt_returns_404_for_unknown_id()
    {
        await using var db = NewDb();
        var pdf = Substitute.For<IBookingReceiptPdfGenerator>();

        var controller = NewController(db, pdf, userId: OwnerUserId, isBackoffice: false);

        var result = await controller.GetReceiptAsync(Guid.NewGuid(), CancellationToken.None);

        result.Should().BeOfType<NotFoundResult>();
    }

    // ---- helpers ---------------------------------------------------------

    private static BookingDbContext NewDb()
    {
        var options = new DbContextOptionsBuilder<BookingDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        return new BookingDbContext(options);
    }

    private static void SeedBooking(BookingDbContext db, string userId)
    {
        db.BookingSagaStates.Add(new BookingSagaState
        {
            CorrelationId = BookingId,
            UserId = userId,
            BookingReference = "TBE-260416-ABCDEF01",
            ProductType = "flight",
            Channel = "b2c",
            Currency = "GBP",
            PaymentMethod = "card",
            TotalAmount = 150m,
            BaseFareAmount = 100m,
            SurchargeAmount = 30m,
            TaxAmount = 20m,
            GdsPnr = "PNR123",
            TicketNumber = "125-1234567890",
            InitiatedAtUtc = DateTime.UtcNow,
            TicketingDeadlineUtc = DateTime.UtcNow.AddHours(24),
        });
        db.SaveChanges();
    }

    private static ReceiptsController NewController(
        BookingDbContext db,
        IBookingReceiptPdfGenerator pdf,
        string userId,
        bool isBackoffice)
    {
        var claims = new List<Claim>
        {
            new(ClaimTypes.NameIdentifier, userId),
            new("sub", userId),
        };
        if (isBackoffice) claims.Add(new Claim(ClaimTypes.Role, "backoffice-staff"));

        var identity = new ClaimsIdentity(claims, authenticationType: "jwt");
        var user = new ClaimsPrincipal(identity);

        var controller = new ReceiptsController(db, pdf, NullLogger<ReceiptsController>.Instance)
        {
            ControllerContext = new ControllerContext
            {
                HttpContext = new DefaultHttpContext { User = user },
            },
        };
        return controller;
    }
}
