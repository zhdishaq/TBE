namespace TBE.Contracts.Messages;

/// <summary>
/// Plan 05-04 Task 1 — B2B TTL warning event (24h horizon).
///
/// Published by the BookingService TTL monitor when a B2B saga's
/// <c>TicketingTimeLimit</c> falls within 24h but is still more than 2h away
/// AND the per-saga <c>Warn24HSent</c> flag is false. The monitor flips the
/// flag in the same DB transaction as the publish so crash/resume cannot
/// duplicate the warning (T-05-04-07).
///
/// Distinct contract from the Phase-3
/// <see cref="TBE.Contracts.Events.TicketingDeadlineApproaching"/> record:
/// that one carries a string <c>Horizon</c> discriminator and is consumed by
/// the B2C <c>TicketingDeadlineApproachingConsumer</c>. This record is the
/// B2B-flavoured variant carrying <c>AgencyId</c> + <c>ClientName</c> so the
/// consumer can fan out to the agency's agent admins.
/// </summary>
public sealed record TicketingDeadlineWarning(
    Guid BookingId,
    Guid AgencyId,
    string Pnr,
    DateTime TicketingTimeLimit,
    decimal HoursRemaining,
    string? ClientName);
