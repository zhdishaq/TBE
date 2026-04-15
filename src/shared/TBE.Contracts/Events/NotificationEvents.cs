namespace TBE.Contracts.Events;

/// <summary>
/// Published by the 03-03 TTL monitor when a saga's ticketing deadline is within a watch
/// horizon. The <paramref name="Horizon"/> discriminates the notification copy
/// (subject + body wording) — currently either <c>"24h"</c> or <c>"2h"</c>.
/// Consumed by NotificationService to send a B2B-agent advisory email (NOTF-04).
/// </summary>
/// <remarks>
/// Contract owned by the Notification workstream but published by BookingService's TTL
/// monitor (03-03). Added in plan 03-04 so the consumer can be built in Wave 2 in parallel
/// with 03-03; 03-03 will reference this contract rather than redefining it.
/// </remarks>
public record TicketingDeadlineApproaching(
    Guid BookingId,
    string Horizon,
    DateTime DeadlineUtc,
    DateTimeOffset At);

/// <summary>
/// Published by BookingService's wallet repository (03-02) when an agency wallet drops
/// below the configured threshold. Consumed by NotificationService to send an internal
/// advisory email to the agency admin (NOTF-05).
/// </summary>
/// <remarks>
/// Contract added in plan 03-04 so the consumer can be built in Wave 2 in parallel with
/// 03-02. 03-02 will reference this contract when publishing from the wallet-debit path.
/// </remarks>
public record WalletLowBalance(
    Guid WalletId,
    decimal Balance,
    decimal Threshold,
    DateTimeOffset At);
