using MassTransit;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using TBE.Contracts.Events;
using TBE.CrmService.Application.Projections;

namespace TBE.CrmService.Infrastructure.Consumers;

/// <summary>
/// Plan 06-04 Task 1 — populate Pnr / TicketNumber on the booking
/// projection and seed an <see cref="UpcomingTripRow"/> when the travel
/// date is already known on the projection (itinerary enrichment happens
/// separately in Phase 7).
/// </summary>
public sealed class TicketIssuedConsumer : IConsumer<TicketIssued>
{
    private readonly CrmDbContext _db;
    private readonly ILogger<TicketIssuedConsumer> _log;

    public TicketIssuedConsumer(CrmDbContext db, ILogger<TicketIssuedConsumer> log)
    {
        _db = db;
        _log = log;
    }

    public async Task Consume(ConsumeContext<TicketIssued> ctx)
    {
        var m = ctx.Message;
        var ct = ctx.CancellationToken;

        var b = await _db.BookingProjections.FirstOrDefaultAsync(x => x.Id == m.BookingId, ct);
        if (b is null)
        {
            // BookingConfirmed hasn't landed yet (out-of-order delivery).
            // Seed a skeletal row so the TicketNumber isn't lost; the
            // BookingConfirmed consumer will fill the other fields.
            b = new BookingProjection
            {
                Id = m.BookingId,
                TicketNumber = m.TicketNumber,
                Status = "Confirmed",
                ConfirmedAt = m.At.UtcDateTime,
                Channel = "b2c",
                Currency = "GBP",
            };
            _db.BookingProjections.Add(b);
        }
        else
        {
            b.TicketNumber = m.TicketNumber;
        }

        // Only surface as an upcoming trip if we already know the travel
        // date AND it is in the future. Past travel dates stay out of
        // UpcomingTrips per CRM-05.
        if (b.TravelDate is DateTime travelDate && travelDate.Date >= DateTime.UtcNow.Date)
        {
            var existing = await _db.UpcomingTrips.FirstOrDefaultAsync(u => u.BookingId == b.Id, ct);
            if (existing is null)
            {
                _db.UpcomingTrips.Add(new UpcomingTripRow
                {
                    BookingId = b.Id,
                    CustomerId = b.CustomerId,
                    AgencyId = b.AgencyId,
                    BookingReference = b.BookingReference,
                    Pnr = b.Pnr,
                    Status = b.Status,
                    TravelDate = travelDate,
                    GrossAmount = b.GrossAmount,
                    Currency = b.Currency,
                    OriginIata = b.OriginIata,
                    DestinationIata = b.DestinationIata,
                });
            }
        }

        await _db.SaveChangesAsync(ct);
        _log.LogInformation("crm ticket issued booking={BookingId} ticket={TicketNumber}", m.BookingId, m.TicketNumber);
    }
}
