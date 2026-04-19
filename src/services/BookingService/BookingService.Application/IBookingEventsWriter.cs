namespace TBE.BookingService.Application;

/// <summary>
/// Plan 06-01 Task 5 — port for appending rows to <c>dbo.BookingEvents</c>.
///
/// Lives in the Application project so the saga (which is owned by
/// Application per MassTransit convention) can depend on it without
/// pulling Infrastructure. The concrete implementation lives in
/// Infrastructure because it owns the writer-only
/// <see cref="Infrastructure.BookingEventsDbContext"/>.
///
/// <para>
/// Contract: caller supplies a plain anonymous <c>snapshotPayload</c>
/// object; the implementation serialises to JSON and INSERTs a single
/// <c>BookingEvent</c> row. Implementation is fire-and-log on
/// persistence failure so a transient DB outage does not corrupt a
/// saga transition that already moved state (observability gap flagged
/// in logs for ops).
/// </para>
/// </summary>
public interface IBookingEventsWriter
{
    Task WriteAsync(
        Guid bookingId,
        string eventType,
        string actor,
        Guid correlationId,
        object snapshotPayload,
        CancellationToken ct);
}
