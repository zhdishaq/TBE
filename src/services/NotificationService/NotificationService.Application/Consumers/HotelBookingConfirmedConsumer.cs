using MassTransit;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using TBE.Contracts.Events;
using TBE.NotificationService.API.Templates.Models;
using TBE.NotificationService.Application.Email;
using TBE.NotificationService.Application.Pdf;
using TBE.NotificationService.Application.Persistence;

namespace TBE.NotificationService.Application.Consumers;

/// <summary>
/// NOTF-02 — delivers the customer-facing hotel voucher email WITH the
/// voucher PDF attachment when the hotel saga publishes <see cref="HotelBookingConfirmed"/>.
/// Idempotency (NOTF-06) is enforced by try-inserting into <c>EmailIdempotencyLog</c>
/// before the SendGrid call; a unique-index violation on <c>(EventId, EmailType)</c>
/// is treated as a duplicate and acked silently (D-19).
/// Unlike <see cref="BookingConfirmedConsumer"/>, the hotel event carries the guest
/// contact inline — no secondary BookingService lookup is required.
/// </summary>
public sealed class HotelBookingConfirmedConsumer : IConsumer<HotelBookingConfirmed>
{
    private readonly NotificationDbContext _db;
    private readonly IEmailTemplateRenderer _renderer;
    private readonly IHotelVoucherPdfGenerator _pdfGen;
    private readonly IEmailDelivery _delivery;
    private readonly BrandOptions _brand;
    private readonly ILogger<HotelBookingConfirmedConsumer> _log;

    public HotelBookingConfirmedConsumer(
        NotificationDbContext db,
        IEmailTemplateRenderer renderer,
        IHotelVoucherPdfGenerator pdfGen,
        IEmailDelivery delivery,
        IOptions<BrandOptions> brand,
        ILogger<HotelBookingConfirmedConsumer> log)
    {
        _db = db;
        _renderer = renderer;
        _pdfGen = pdfGen;
        _delivery = delivery;
        _brand = brand.Value;
        _log = log;
    }

    public async Task Consume(ConsumeContext<HotelBookingConfirmed> ctx)
    {
        var eventId = ctx.MessageId ?? ctx.Message.EventId;
        var evt = ctx.Message;

        if (string.IsNullOrWhiteSpace(evt.GuestEmail))
        {
            _log.LogWarning(
                "NOTF-02: no GuestEmail on HotelBookingConfirmed for booking {BookingId} — skipping voucher email",
                evt.BookingId);
            return;
        }

        var idemp = new EmailIdempotencyLog
        {
            EventId = eventId,
            EmailType = EmailType.HotelVoucher,
            BookingId = evt.BookingId,
            Recipient = evt.GuestEmail,
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
                EmailType.HotelVoucher, eventId);
            return;
        }

        var model = new HotelVoucherModel(
            BookingReference: evt.BookingReference,
            SupplierRef: evt.SupplierRef,
            PropertyName: evt.PropertyName,
            AddressLine: evt.AddressLine,
            CheckInDate: evt.CheckInDate,
            CheckOutDate: evt.CheckOutDate,
            Rooms: evt.Rooms,
            Adults: evt.Adults,
            Children: evt.Children,
            TotalAmount: evt.TotalAmount,
            Currency: evt.Currency,
            GuestEmail: evt.GuestEmail,
            GuestFullName: evt.GuestFullName,
            BrandName: _brand.BrandName,
            SupportPhone: _brand.SupportPhone);

        var rendered = await _renderer.RenderAsync(
            EmailType.HotelVoucher,
            model,
            ctx.CancellationToken).ConfigureAwait(false);

        var pdfBytes = _pdfGen.Generate(model);

        var envelope = new EmailEnvelope(
            ToEmail: evt.GuestEmail,
            ToName: evt.GuestFullName,
            Subject: rendered.Subject,
            HtmlBody: rendered.HtmlBody,
            PlainTextBody: rendered.PlainTextBody,
            Attachments: new[]
            {
                new EmailAttachment("voucher.pdf", "application/pdf", pdfBytes, "voucher.pdf")
            });

        var result = await _delivery.SendAsync(envelope, ctx.CancellationToken).ConfigureAwait(false);
        if (!result.Success)
        {
            // Thrown so MassTransit retries per the transport-level retry policy.
            throw new InvalidOperationException(
                $"SendGrid delivery failed for hotel booking {evt.BookingId}: {result.ErrorReason}");
        }

        idemp.ProviderMessageId = result.ProviderMessageId;
        await _db.SaveChangesAsync(ctx.CancellationToken).ConfigureAwait(false);
    }
}
