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
/// NOTF-01 supporting — renders the internal "ticket issued" lifecycle email. The
/// customer-facing confirmation + e-ticket PDF is dispatched by
/// <see cref="BookingConfirmedConsumer"/>; this consumer is a lightweight audit notice
/// for internal/agent-visibility flows.
/// </summary>
public sealed class TicketIssuedConsumer : IConsumer<TicketIssued>
{
    private readonly NotificationDbContext _db;
    private readonly IBookingContactClient _bookings;
    private readonly IEmailTemplateRenderer _renderer;
    private readonly IEmailDelivery _delivery;
    private readonly ILogger<TicketIssuedConsumer> _log;

    public TicketIssuedConsumer(
        NotificationDbContext db,
        IBookingContactClient bookings,
        IEmailTemplateRenderer renderer,
        IEmailDelivery delivery,
        ILogger<TicketIssuedConsumer> log)
    {
        _db = db; _bookings = bookings; _renderer = renderer; _delivery = delivery; _log = log;
    }

    public async Task Consume(ConsumeContext<TicketIssued> ctx)
    {
        var eventId = ctx.MessageId ?? Guid.NewGuid();
        var evt = ctx.Message;

        var contact = await _bookings.GetContactAsync(evt.BookingId, ctx.CancellationToken).ConfigureAwait(false);
        if (contact is null)
        {
            _log.LogWarning("NOTF-01 (ticket-issued notice): no contact for booking {BookingId}", evt.BookingId);
            return;
        }

        var idemp = new EmailIdempotencyLog
        {
            EventId = eventId,
            EmailType = EmailType.TicketIssued,
            BookingId = evt.BookingId,
            Recipient = contact.Email,
            SentAtUtc = DateTime.UtcNow,
        };
        _db.EmailIdempotencyLogs.Add(idemp);
        try { await _db.SaveChangesAsync(ctx.CancellationToken).ConfigureAwait(false); }
        catch (DbUpdateException ex) when (IdempotencyHelpers.IsUniqueViolation(ex))
        {
            _log.LogInformation("NOTF-06: duplicate TicketIssued notice for {EventId} skipped", eventId);
            return;
        }

        var rendered = await _renderer.RenderAsync(
            EmailType.TicketIssued,
            new TicketIssuedModel(
                PassengerName: contact.Name,
                Pnr: "—",
                ETicketNumber: evt.TicketNumber,
                FlightNumber: "—"),
            ctx.CancellationToken).ConfigureAwait(false);

        var envelope = new EmailEnvelope(
            contact.Email, contact.Name, rendered.Subject,
            rendered.HtmlBody, rendered.PlainTextBody,
            Array.Empty<EmailAttachment>());

        var result = await _delivery.SendAsync(envelope, ctx.CancellationToken).ConfigureAwait(false);
        if (!result.Success)
            throw new InvalidOperationException($"SendGrid failed for TicketIssued {evt.BookingId}: {result.ErrorReason}");

        idemp.ProviderMessageId = result.ProviderMessageId;
        await _db.SaveChangesAsync(ctx.CancellationToken).ConfigureAwait(false);
    }
}
