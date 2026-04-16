using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using TBE.BookingService.Application.Pdf;
using TBE.BookingService.Infrastructure;

namespace TBE.BookingService.API.Controllers;

/// <summary>
/// Plan 04-01 / CONTEXT D-15 — B2C receipt PDF endpoint. Streams a QuestPDF
/// byte array to authenticated callers, enforcing ownership (T-04-01-01):
/// only the booking's owner (by JWT <c>sub</c> / NameIdentifier) or a
/// backoffice-staff caller may download another user's receipt.
///
/// Mirrors the auth + ownership pattern from <see cref="BookingsController"/>.
/// </summary>
[ApiController]
[Route("bookings")]
[Authorize]
public class ReceiptsController(
    BookingDbContext db,
    IBookingReceiptPdfGenerator pdfGen,
    ILogger<ReceiptsController> logger) : ControllerBase
{
    private const string BackofficeRole = "backoffice-staff";

    [HttpGet("{id:guid}/receipt.pdf")]
    public async Task<IActionResult> GetReceiptAsync(Guid id, CancellationToken ct)
    {
        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier)
            ?? User.FindFirstValue("sub")
            ?? string.Empty;

        var booking = await db.BookingSagaStates
            .AsNoTracking()
            .FirstOrDefaultAsync(s => s.CorrelationId == id, ct);

        if (booking is null)
        {
            logger.LogInformation("Receipt request for unknown booking {BookingId}", id);
            return NotFound();
        }

        if (booking.UserId != userId && !User.IsInRole(BackofficeRole))
        {
            logger.LogWarning(
                "Receipt access denied (IDOR guard) booking={BookingId} requester={User} owner={Owner}",
                id, userId, booking.UserId);
            return Forbid();
        }

        var bytes = await pdfGen.GenerateAsync(booking, ct);
        return File(bytes, "application/pdf", $"receipt-{booking.BookingReference}.pdf");
    }
}
