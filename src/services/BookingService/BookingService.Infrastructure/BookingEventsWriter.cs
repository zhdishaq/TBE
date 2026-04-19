using System.Text.Json;
using Microsoft.Extensions.Logging;
using TBE.BookingService.Application;
using TBE.BookingService.Application.Saga;

// Namespace kept as BookingService.Application per Plan 06-01 file
// manifest. Physical project is Infrastructure because the writer
// depends on BookingEventsDbContext (DI boundary).
namespace TBE.BookingService.Application;

/// <summary>
/// Plan 06-01 Task 5 — single surface through which BookingSaga (and
/// Phase 6 staff controllers) append to <c>dbo.BookingEvents</c>.
///
/// See <see cref="IBookingEventsWriter"/> for the contract. This
/// implementation performs exactly one INSERT via the writer-only
/// <see cref="Infrastructure.BookingEventsDbContext"/> — separated from
/// the main BookingDbContext so ChangeTracker cannot accidentally UPDATE
/// a prior BookingEvent row (Pitfall 1 / D-49 enforcement).
/// </summary>
public sealed class BookingEventsWriter : IBookingEventsWriter
{
    private static readonly JsonSerializerOptions SerializerOptions = new()
    {
        WriteIndented = false,
        DefaultIgnoreCondition = System.Text.Json.Serialization.JsonIgnoreCondition.Never,
    };

    private readonly Infrastructure.BookingEventsDbContext _db;
    private readonly ILogger<BookingEventsWriter> _logger;

    public BookingEventsWriter(
        Infrastructure.BookingEventsDbContext db,
        ILogger<BookingEventsWriter> logger)
    {
        _db = db;
        _logger = logger;
    }

    public async Task WriteAsync(
        Guid bookingId,
        string eventType,
        string actor,
        Guid correlationId,
        object snapshotPayload,
        CancellationToken ct)
    {
        // Serialize first — a failure here is caller's fault (bad shape),
        // let it propagate.
        var snapshot = JsonSerializer.Serialize(snapshotPayload, SerializerOptions);

        var row = new BookingEvent(
            eventId: Guid.NewGuid(),
            bookingId: bookingId,
            eventType: eventType,
            occurredAt: DateTime.UtcNow,
            actor: actor,
            correlationId: correlationId,
            snapshot: snapshot);

        try
        {
            _db.Events.Add(row);
            await _db.SaveChangesAsync(ct);
        }
        catch (Exception ex)
        {
            // Fire-and-log: a failing audit append must NOT take down the
            // saga transition that already moved state (observability gap
            // flagged in logs for ops).
            _logger.LogError(
                ex,
                "booking-events-write-failed {BookingId} {EventType} {Actor}",
                bookingId, eventType, actor);
        }
    }
}
