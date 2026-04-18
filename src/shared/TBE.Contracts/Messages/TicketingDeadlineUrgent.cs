namespace TBE.Contracts.Messages;

/// <summary>
/// Plan 05-04 Task 1 — B2B TTL urgent event (2h horizon).
///
/// Same shape as <see cref="TicketingDeadlineWarning"/> but different record
/// type so MassTransit routes it to a different consumer handler with urgent
/// copy (red-500 inline style; subject prefixed "URGENT:").
///
/// Published by the BookingService TTL monitor when a B2B saga's
/// <c>TicketingTimeLimit</c> is within 2h AND the per-saga <c>Warn2HSent</c>
/// flag is false. The flag flip + publish happen in the same DB transaction
/// so crash/resume is idempotent (T-05-04-07).
/// </summary>
public sealed record TicketingDeadlineUrgent(
    Guid BookingId,
    Guid AgencyId,
    string Pnr,
    DateTime TicketingTimeLimit,
    decimal HoursRemaining,
    string? ClientName);
