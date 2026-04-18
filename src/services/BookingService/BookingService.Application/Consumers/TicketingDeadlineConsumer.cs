using MassTransit;
using Microsoft.Extensions.Logging;
using TBE.BookingService.Application.Keycloak;
using TBE.Contracts.Messages;

namespace TBE.BookingService.Application.Consumers;

/// <summary>
/// Plan 05-04 Task 1 (B2B-09) — fans out a B2B-agent advisory e-mail when
/// the Plan 03-03 TTL monitor publishes
/// <see cref="TicketingDeadlineWarning"/> (24h horizon, amber "Heads-up") or
/// <see cref="TicketingDeadlineUrgent"/> (2h horizon, red "URGENT:"). A
/// single consumer class implements both <see cref="IConsumer{T}"/> handlers
/// so the Keycloak lookup and logging paths stay in one file.
/// </summary>
/// <remarks>
/// <para>
/// <b>Anti-spoofing (T-05-04-07):</b> the recipient list is always resolved
/// fresh from Keycloak using the event's <c>AgencyId</c>, never read from
/// the message body or any per-agency cache. The
/// <see cref="IKeycloakB2BAdminClient"/> implementation intersects the
/// agency-id attribute match with the realm-role allow-list (default
/// <c>agent-admin</c>+<c>agent</c>; <c>agent-readonly</c> excluded — plan).
/// </para>
/// <para>
/// <b>Idempotency:</b> the TTL monitor flips its <c>Warn24HSent</c> /
/// <c>Warn2HSent</c> flags in the same DB transaction as the publish
/// (BookingService.Infrastructure.Ttl), so at-most-once delivery per saga
/// per horizon is already enforced at the publisher. The consumer just
/// guards against the "empty recipients" edge case (agency exists but has
/// no eligible users) by logging and returning — the send-stub remains a
/// no-op in that path.
/// </para>
/// <para>
/// <b>Audit note — 05-04 deferred-items:</b> the deferred list flagged that
/// nothing was consuming these events, so a running BookingService would
/// silently accumulate <c>skipped_messages</c> on the default exchange.
/// Registering this consumer in <c>Program.cs</c> (Wave C) resolves that.
/// </para>
/// </remarks>
public sealed class TicketingDeadlineConsumer :
    IConsumer<TicketingDeadlineWarning>,
    IConsumer<TicketingDeadlineUrgent>
{
    private readonly IKeycloakB2BAdminClient _keycloak;
    private readonly ITicketingDeadlineEmailSender _email;
    private readonly ILogger<TicketingDeadlineConsumer> _log;

    public TicketingDeadlineConsumer(
        IKeycloakB2BAdminClient keycloak,
        ITicketingDeadlineEmailSender email,
        ILogger<TicketingDeadlineConsumer> log)
    {
        _keycloak = keycloak;
        _email = email;
        _log = log;
    }

    public Task Consume(ConsumeContext<TicketingDeadlineWarning> ctx) =>
        HandleAsync(
            TicketingDeadlineHorizon.Warning,
            ctx.Message.BookingId,
            ctx.Message.AgencyId,
            ctx.Message.Pnr,
            ctx.Message.TicketingTimeLimit,
            ctx.Message.HoursRemaining,
            ctx.Message.ClientName,
            ctx.CancellationToken);

    public Task Consume(ConsumeContext<TicketingDeadlineUrgent> ctx) =>
        HandleAsync(
            TicketingDeadlineHorizon.Urgent,
            ctx.Message.BookingId,
            ctx.Message.AgencyId,
            ctx.Message.Pnr,
            ctx.Message.TicketingTimeLimit,
            ctx.Message.HoursRemaining,
            ctx.Message.ClientName,
            ctx.CancellationToken);

    private async Task HandleAsync(
        TicketingDeadlineHorizon horizon,
        Guid bookingId,
        Guid agencyId,
        string pnr,
        DateTime ticketingTimeLimit,
        decimal hoursRemaining,
        string? clientName,
        CancellationToken ct)
    {
        // Anti-spoofing — always fetch recipients fresh.
        var recipients = await _keycloak
            .GetAgentContactsForAgencyAsync(agencyId, ct)
            .ConfigureAwait(false);

        if (recipients.Count == 0)
        {
            _log.LogWarning(
                "ticketing-deadline {Horizon} e-mail not sent — no eligible recipients agency={AgencyId} booking={BookingId}",
                horizon, agencyId, bookingId);
            return;
        }

        await _email.SendDeadlineEmailAsync(
            recipients,
            horizon,
            bookingId,
            agencyId,
            pnr,
            ticketingTimeLimit,
            hoursRemaining,
            clientName,
            ct).ConfigureAwait(false);

        _log.LogInformation(
            "ticketing-deadline {Horizon} advisory dispatched booking={BookingId} agency={AgencyId} recipients={Count} hoursRemaining={Hours}",
            horizon, bookingId, agencyId, recipients.Count, hoursRemaining);
    }
}
