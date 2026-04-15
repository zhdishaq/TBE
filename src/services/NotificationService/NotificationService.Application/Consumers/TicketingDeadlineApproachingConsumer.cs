using MassTransit;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using TBE.Contracts.Events;
using TBE.NotificationService.API.Templates.Models;
using TBE.NotificationService.Application.Contacts;
using TBE.NotificationService.Application.Email;
using TBE.NotificationService.Application.Persistence;

namespace TBE.NotificationService.Application.Consumers;

/// <summary>
/// NOTF-04 — sends a B2B-agent advisory email when the 03-03 TTL monitor publishes
/// <see cref="TicketingDeadlineApproaching"/>. The event's <c>Horizon</c> discriminates
/// subject-line + body wording (24h vs. 2h) — a single consumer handles both.
/// </summary>
public sealed class TicketingDeadlineApproachingConsumer : IConsumer<TicketingDeadlineApproaching>
{
    private readonly NotificationDbContext _db;
    private readonly IBookingContactClient _bookings;
    private readonly IEmailTemplateRenderer _renderer;
    private readonly IEmailDelivery _delivery;
    private readonly ILogger<TicketingDeadlineApproachingConsumer> _log;

    public TicketingDeadlineApproachingConsumer(
        NotificationDbContext db,
        IBookingContactClient bookings,
        IEmailTemplateRenderer renderer,
        IEmailDelivery delivery,
        ILogger<TicketingDeadlineApproachingConsumer> log)
    {
        _db = db; _bookings = bookings; _renderer = renderer; _delivery = delivery; _log = log;
    }

    public async Task Consume(ConsumeContext<TicketingDeadlineApproaching> ctx)
    {
        var eventId = ctx.MessageId ?? Guid.NewGuid();
        var evt = ctx.Message;

        var contact = await _bookings.GetContactAsync(evt.BookingId, ctx.CancellationToken).ConfigureAwait(false);
        if (contact is null)
        {
            _log.LogWarning("NOTF-04: no contact for booking {BookingId}", evt.BookingId);
            return;
        }

        // NOTF-06 idempotency — keyed per horizon so the 24h + 2h notices are BOTH allowed.
        var idemp = new EmailIdempotencyLog
        {
            EventId = eventId,
            EmailType = $"{EmailType.TicketingDeadlineApproaching}:{evt.Horizon}",
            BookingId = evt.BookingId,
            Recipient = contact.Email,
            SentAtUtc = DateTime.UtcNow,
        };
        _db.EmailIdempotencyLogs.Add(idemp);
        try { await _db.SaveChangesAsync(ctx.CancellationToken).ConfigureAwait(false); }
        catch (DbUpdateException ex) when (IdempotencyHelpers.IsUniqueViolation(ex))
        {
            _log.LogInformation("NOTF-06: duplicate TicketingDeadline ({Horizon}) for {EventId} skipped", evt.Horizon, eventId);
            return;
        }

        var rendered = await _renderer.RenderAsync(
            EmailType.TicketingDeadlineApproaching,
            new TicketingDeadlineModel(
                PassengerName: contact.Name,
                Pnr: evt.BookingId.ToString("N").Substring(0, 6).ToUpperInvariant(),
                Horizon: evt.Horizon,
                DeadlineUtc: evt.DeadlineUtc),
            ctx.CancellationToken).ConfigureAwait(false);

        var envelope = new EmailEnvelope(
            contact.Email, contact.Name, rendered.Subject,
            rendered.HtmlBody, rendered.PlainTextBody,
            Array.Empty<EmailAttachment>());

        var result = await _delivery.SendAsync(envelope, ctx.CancellationToken).ConfigureAwait(false);
        if (!result.Success)
            throw new InvalidOperationException($"SendGrid failed for TicketingDeadline ({evt.Horizon}) {evt.BookingId}: {result.ErrorReason}");

        idemp.ProviderMessageId = result.ProviderMessageId;
        await _db.SaveChangesAsync(ctx.CancellationToken).ConfigureAwait(false);
    }
}
