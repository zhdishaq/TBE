namespace TBE.BookingService.Application.Saga;

/// <summary>
/// Plan 06-01 Task 5 (BO-04 / BO-05 / D-49 / D-50) — append-only audit
/// row for every BookingSaga transition. One row per state change, full
/// snapshot JSON attached.
///
/// This entity is intentionally read-only at the field level: all values
/// are set in the constructor and exposed through getters only. The
/// table is DENY'd for UPDATE/DELETE at the SQL Server engine via the
/// <c>booking_events_writer</c> role (migration
/// 20260601100001_AddAppendOnlyRoleGrants).
///
/// Pitfall 1: this entity is mapped on the dedicated
/// <see cref="Infrastructure.BookingEventsDbContext"/> which has NO
/// <c>DbSet&lt;BookingSagaState&gt;</c> — so EF's ChangeTracker cannot
/// accidentally issue an UPDATE against a DENY'd row via the saga
/// persistence pipeline.
/// </summary>
public sealed class BookingEvent
{
    // EF / provider ctor
    private BookingEvent() { }

    public BookingEvent(
        Guid eventId,
        Guid bookingId,
        string eventType,
        DateTime occurredAt,
        string actor,
        Guid correlationId,
        string snapshot)
    {
        EventId = eventId;
        BookingId = bookingId;
        EventType = eventType;
        OccurredAt = occurredAt;
        Actor = actor;
        CorrelationId = correlationId;
        Snapshot = snapshot;
    }

    public Guid EventId { get; private set; }
    public Guid BookingId { get; private set; }
    public string EventType { get; private set; } = string.Empty;
    public DateTime OccurredAt { get; private set; }
    public string Actor { get; private set; } = string.Empty;
    public Guid CorrelationId { get; private set; }
    public string Snapshot { get; private set; } = string.Empty;
}
