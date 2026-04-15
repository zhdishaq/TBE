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

/// <summary>
/// Published by BookingService's CreatePnrConsumer (03-03) when the raw GDS fare-rule payload
/// cannot be parsed by any of the per-GDS adapters. The consumer still publishes <c>PnrCreated</c>
/// with the D-07 fallback deadline (UtcNow + 2h), but this advisory lets ops investigate adapter
/// drift offline. The RawPayloadDigest is an SHA-256 hex of the first 1 KB of the raw payload
/// so the original fare-rule bytes are never persisted or transported (T-03-05 PII control).
/// </summary>
public record FareRuleParseFailedAlert(
    Guid BookingId,
    string GdsCode,
    string RawPayloadDigest,
    DateTimeOffset At);
