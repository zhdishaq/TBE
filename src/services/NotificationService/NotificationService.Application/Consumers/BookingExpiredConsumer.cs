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
/// NOTF-03 family — sends the customer an "unfortunately your booking expired" email when
/// the 03-03 TTL monitor publishes <see cref="BookingExpired"/>. Reuses the cancellation
/// template wording with an explicit "expired at ticketing deadline" reason.
/// </summary>
public sealed class BookingExpiredConsumer : IConsumer<BookingExpired>
{
    private readonly NotificationDbContext _db;
    private readonly IBookingContactClient _bookings;
    private readonly IEmailTemplateRenderer _renderer;
    private readonly IEmailDelivery _delivery;
    private readonly ILogger<BookingExpiredConsumer> _log;

    public BookingExpiredConsumer(
        NotificationDbContext db,
        IBookingContactClient bookings,
        IEmailTemplateRenderer renderer,
        IEmailDelivery delivery,
        ILogger<BookingExpiredConsumer> log)
    {
        _db = db; _bookings = bookings; _renderer = renderer; _delivery = delivery; _log = log;
    }

    public async Task Consume(ConsumeContext<BookingExpired> ctx)
    {
        var eventId = ctx.MessageId ?? ctx.Message.EventId;
        var evt = ctx.Message;

        var contact = await _bookings.GetContactAsync(evt.BookingId, ctx.CancellationToken).ConfigureAwait(false);
        if (contact is null)
        {
            _log.LogWarning("NOTF-03 (expired): no contact for booking {BookingId}", evt.BookingId);
            return;
        }

        var idemp = new EmailIdempotencyLog
        {
            EventId = eventId,
            EmailType = EmailType.BookingExpired,
            BookingId = evt.BookingId,
            Recipient = contact.Email,
            SentAtUtc = DateTime.UtcNow,
        };
        _db.EmailIdempotencyLogs.Add(idemp);
        try { await _db.SaveChangesAsync(ctx.CancellationToken).ConfigureAwait(false); }
        catch (DbUpdateException ex) when (IdempotencyHelpers.IsUniqueViolation(ex))
        {
            _log.LogInformation("NOTF-06: duplicate BookingExpired for {EventId} skipped", eventId);
            return;
        }

        // Reuse FlightCancellation template with "expired at ticketing deadline" reason copy.
        var rendered = await _renderer.RenderAsync(
            EmailType.FlightCancellation,
            new FlightCancellationModel(
                PassengerName: contact.Name,
                Pnr: evt.BookingId.ToString("N").Substring(0, 6).ToUpperInvariant(),
                Reason: "Your booking expired before ticketing could complete.",
                RefundAmount: 0m,
                Currency: "GBP"),
            ctx.CancellationToken).ConfigureAwait(false);

        var envelope = new EmailEnvelope(
            contact.Email, contact.Name, rendered.Subject,
            rendered.HtmlBody, rendered.PlainTextBody,
            Array.Empty<EmailAttachment>());

        var result = await _delivery.SendAsync(envelope, ctx.CancellationToken).ConfigureAwait(false);
        if (!result.Success)
            throw new InvalidOperationException($"SendGrid failed for BookingExpired {evt.BookingId}: {result.ErrorReason}");

        idemp.ProviderMessageId = result.ProviderMessageId;
        await _db.SaveChangesAsync(ctx.CancellationToken).ConfigureAwait(false);
    }
}
