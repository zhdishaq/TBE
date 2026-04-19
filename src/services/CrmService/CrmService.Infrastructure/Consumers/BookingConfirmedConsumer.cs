using MassTransit;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using TBE.Contracts.Events;
using TBE.CrmService.Application.Projections;

namespace TBE.CrmService.Infrastructure.Consumers;

/// <summary>
/// Plan 06-04 Task 1 — upsert <see cref="BookingProjection"/> + bump
/// lifetime counters on <see cref="CustomerProjection"/> / <see cref="AgencyProjection"/>.
/// </summary>
/// <remarks>
/// Lives in <c>CrmService.Infrastructure</c> (not <c>CrmService.Application</c>)
/// because the consumer depends on <see cref="CrmDbContext"/>. This mirrors
/// BackofficeService where consumers + DbContext ship in the same project
/// to avoid an Application→Infrastructure cycle. Rule 3 fix during
/// 06-04 Task 1 execution.
///
/// Idempotency: MassTransit <c>InboxState</c> dedup on MessageId skips a
/// second invocation with the same envelope id — no custom dedup code.
/// This is the deviation from RESEARCH Pattern 5 documented in
/// 06-04-PLAN (we use MT outbox + inbox instead of hand-rolling a
/// <c>ProcessedMessages</c> table).
///
/// The in-repo <see cref="BookingConfirmed"/> (SagaEvents.cs) carries
/// only terminal-ticket fields (BookingRef + Pnr + TicketNumber +
/// PaymentIntentId) — not the pricing / customer / agency shape the
/// 06-04 plan drafted. Fields not on the event default to zero; the
/// per-field enrichment happens via later <see cref="WalletToppedUp"/>
/// + <c>AgentBookingDetailsCaptured</c> events that already carry the
/// pricing fan-out (Plan 05-02).
/// </remarks>
public sealed class BookingConfirmedConsumer : IConsumer<BookingConfirmed>
{
    private readonly CrmDbContext _db;
    private readonly ILogger<BookingConfirmedConsumer> _log;

    public BookingConfirmedConsumer(CrmDbContext db, ILogger<BookingConfirmedConsumer> log)
    {
        _db = db;
        _log = log;
    }

    public async Task Consume(ConsumeContext<BookingConfirmed> ctx)
    {
        var m = ctx.Message;
        var ct = ctx.CancellationToken;

        var b = await _db.BookingProjections.FirstOrDefaultAsync(x => x.Id == m.BookingId, ct);
        if (b is null)
        {
            b = new BookingProjection
            {
                Id = m.BookingId,
                BookingReference = m.BookingReference,
                Pnr = m.Pnr,
                TicketNumber = m.TicketNumber,
                Status = "Confirmed",
                ConfirmedAt = m.At.UtcDateTime,
                Channel = "b2c", // default; AgentBookingDetailsCaptured consumer can flip to "b2b"
                Currency = "GBP",
            };
            _db.BookingProjections.Add(b);
        }
        else
        {
            b.Status = "Confirmed";
            b.ConfirmedAt = m.At.UtcDateTime;
            b.BookingReference = m.BookingReference;
            b.Pnr = m.Pnr;
            b.TicketNumber = m.TicketNumber;
        }

        // Lifetime counters for linked customer / agency, if populated from
        // earlier events (AgentBookingDetailsCaptured writes CustomerId /
        // AgencyId / pricing).
        if (b.CustomerId is Guid cid)
        {
            var c = await _db.Customers.FirstOrDefaultAsync(x => x.Id == cid, ct);
            if (c is not null)
            {
                c.LifetimeBookingsCount++;
                c.LifetimeGross += b.GrossAmount;
                c.LastBookingAt = b.ConfirmedAt;
            }
        }

        if (b.AgencyId is Guid aid)
        {
            var a = await _db.Agencies.FirstOrDefaultAsync(x => x.Id == aid, ct);
            if (a is not null)
            {
                a.LifetimeBookingsCount++;
                a.LifetimeGross += b.GrossAmount;
                a.LifetimeCommission += b.CommissionAmount;
                a.LastBookingAt = b.ConfirmedAt;
            }
        }

        // UpcomingTrips materialisation deferred until TravelDate is known
        // (itinerary JSON parsing — enrichment reserved for Phase 7).

        await _db.SaveChangesAsync(ct);
        _log.LogInformation("crm projection upsert booking={BookingId} pnr={Pnr}", m.BookingId, m.Pnr);
    }
}
