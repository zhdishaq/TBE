using MassTransit;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using TBE.Contracts.Events;
using TBE.CrmService.Application.Projections;

namespace TBE.CrmService.Infrastructure.Consumers;

/// <summary>
/// Plan 06-04 Task 1 — mark the booking projection Cancelled, cascade
/// status onto the matching <see cref="UpcomingTripRow"/>. LifetimeGross
/// is intentionally NOT decremented — per the consumer doctrine in
/// 06-04-PLAN "keep lifetime gross as committed revenue; cancellation
/// doesn't reduce it".
/// </summary>
public sealed class BookingCancelledConsumer : IConsumer<BookingCancelled>
{
    private readonly CrmDbContext _db;
    private readonly ILogger<BookingCancelledConsumer> _log;

    public BookingCancelledConsumer(CrmDbContext db, ILogger<BookingCancelledConsumer> log)
    {
        _db = db;
        _log = log;
    }

    public async Task Consume(ConsumeContext<BookingCancelled> ctx)
    {
        var m = ctx.Message;
        var ct = ctx.CancellationToken;

        var b = await _db.BookingProjections.FirstOrDefaultAsync(x => x.Id == m.BookingId, ct);
        if (b is not null)
        {
            b.Status = "Cancelled";
            b.CancelledAt = m.At.UtcDateTime;
        }

        var trip = await _db.UpcomingTrips.FirstOrDefaultAsync(u => u.BookingId == m.BookingId, ct);
        if (trip is not null)
        {
            trip.Status = "Cancelled";
        }

        await _db.SaveChangesAsync(ct);
        _log.LogInformation("crm projection cancel booking={BookingId}", m.BookingId);
    }
}
