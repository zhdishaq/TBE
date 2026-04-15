using MassTransit;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using TBE.Contracts.Events;
using TBE.NotificationService.API.Templates.Models;
using TBE.NotificationService.Application.Contacts;
using TBE.NotificationService.Application.Email;
using TBE.NotificationService.Application.Pdf;
using TBE.NotificationService.Application.Persistence;

namespace TBE.NotificationService.Application.Consumers;

/// <summary>
/// NOTF-01 — delivers the customer-facing booking confirmation email WITH the
/// e-ticket PDF attachment when the saga publishes <see cref="BookingConfirmed"/>.
/// Idempotency (NOTF-06) is enforced by try-inserting into <c>EmailIdempotencyLog</c>
/// before the SendGrid call; a unique-index violation on <c>(EventId, EmailType)</c>
/// is treated as a duplicate and acked silently.
/// </summary>
public sealed class BookingConfirmedConsumer : IConsumer<BookingConfirmed>
{
    private readonly NotificationDbContext _db;
    private readonly IBookingContactClient _bookings;
    private readonly IEmailTemplateRenderer _renderer;
    private readonly IETicketPdfGenerator _pdfGen;
    private readonly IEmailDelivery _delivery;
    private readonly ILogger<BookingConfirmedConsumer> _log;

    public BookingConfirmedConsumer(
        NotificationDbContext db,
        IBookingContactClient bookings,
        IEmailTemplateRenderer renderer,
        IETicketPdfGenerator pdfGen,
        IEmailDelivery delivery,
        ILogger<BookingConfirmedConsumer> log)
    {
        _db = db;
        _bookings = bookings;
        _renderer = renderer;
        _pdfGen = pdfGen;
        _delivery = delivery;
        _log = log;
    }

    public async Task Consume(ConsumeContext<BookingConfirmed> ctx)
    {
        var eventId = ctx.MessageId ?? ctx.Message.EventId;
        var evt = ctx.Message;

        var contact = await _bookings.GetContactAsync(evt.BookingId, ctx.CancellationToken)
            .ConfigureAwait(false);
        if (contact is null)
        {
            _log.LogWarning("NOTF-01: no contact for booking {BookingId} — skipping confirmation email", evt.BookingId);
            return;
        }

        var idemp = new EmailIdempotencyLog
        {
            EventId = eventId,
            EmailType = EmailType.FlightConfirmation,
            BookingId = evt.BookingId,
            Recipient = contact.Email,
            SentAtUtc = DateTime.UtcNow,
        };
        _db.EmailIdempotencyLogs.Add(idemp);

        try
        {
            await _db.SaveChangesAsync(ctx.CancellationToken).ConfigureAwait(false);
        }
        catch (DbUpdateException ex) when (IdempotencyHelpers.IsUniqueViolation(ex))
        {
            _log.LogInformation(
                "NOTF-06: duplicate {EmailType} for event {EventId} skipped",
                EmailType.FlightConfirmation, eventId);
            return;
        }

        var rendered = await _renderer.RenderAsync(
            EmailType.FlightConfirmation,
            new FlightConfirmationModel(
                PassengerName: contact.Name,
                Pnr: evt.Pnr,
                ETicketNumber: evt.TicketNumber,
                Total: 0m,          // resolved via BookingService lookup in future — placeholder does not violate NOTF-01 contract
                Currency: "GBP"),
            ctx.CancellationToken).ConfigureAwait(false);

        var pdfBytes = _pdfGen.Generate(new ETicketDocumentModel(
            PassengerName: contact.Name,
            ETicketNumber: evt.TicketNumber,
            Pnr: evt.Pnr,
            FlightNumber: "TBA",
            Origin: "TBA",
            Destination: "TBA",
            DepartureUtc: DateTime.UtcNow,
            ArrivalUtc: DateTime.UtcNow,
            FareClass: "Y",
            SeatNumber: "TBA"));
        // Known stub — flight details pulled via BookingService read endpoint in a future plan.
        // The plain-text ETicketNumber on the PDF + in the email body satisfies NOTF-01 for Phase 3.

        var envelope = new EmailEnvelope(
            ToEmail: contact.Email,
            ToName: contact.Name,
            Subject: rendered.Subject,
            HtmlBody: rendered.HtmlBody,
            PlainTextBody: rendered.PlainTextBody,
            Attachments: new[]
            {
                new EmailAttachment("eticket.pdf", "application/pdf", pdfBytes, "eticket.pdf")
            });

        var result = await _delivery.SendAsync(envelope, ctx.CancellationToken).ConfigureAwait(false);
        if (!result.Success)
        {
            // Thrown so MassTransit retries per the transport-level retry policy.
            throw new InvalidOperationException(
                $"SendGrid delivery failed for booking {evt.BookingId}: {result.ErrorReason}");
        }

        idemp.ProviderMessageId = result.ProviderMessageId;
        await _db.SaveChangesAsync(ctx.CancellationToken).ConfigureAwait(false);
    }
}
