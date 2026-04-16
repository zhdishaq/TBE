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
/// NOTF-02 family / CARB-03 — delivers the customer-facing car-hire voucher email WITH
/// the voucher PDF attachment when the car saga publishes <see cref="CarBookingConfirmed"/>.
/// Idempotency (NOTF-06) is enforced by try-inserting into <c>EmailIdempotencyLog</c>
/// before the SendGrid call; a unique-index violation on <c>(EventId, EmailType)</c>
/// is treated as a duplicate and acked silently.
/// </summary>
public sealed class CarBookingConfirmedConsumer : IConsumer<CarBookingConfirmed>
{
    private readonly NotificationDbContext _db;
    private readonly IEmailTemplateRenderer _renderer;
    private readonly ICarVoucherPdfGenerator _pdfGen;
    private readonly IEmailDelivery _delivery;
    private readonly BrandOptions _brand;
    private readonly ILogger<CarBookingConfirmedConsumer> _log;

    public CarBookingConfirmedConsumer(
        NotificationDbContext db,
        IEmailTemplateRenderer renderer,
        ICarVoucherPdfGenerator pdfGen,
        IEmailDelivery delivery,
        IOptions<BrandOptions> brand,
        ILogger<CarBookingConfirmedConsumer> log)
    {
        _db = db;
        _renderer = renderer;
        _pdfGen = pdfGen;
        _delivery = delivery;
        _brand = brand.Value;
        _log = log;
    }

    public async Task Consume(ConsumeContext<CarBookingConfirmed> ctx)
    {
        var eventId = ctx.MessageId ?? ctx.Message.EventId;
        var evt = ctx.Message;

        if (string.IsNullOrWhiteSpace(evt.GuestEmail))
        {
            _log.LogWarning(
                "CARB-03: no GuestEmail on CarBookingConfirmed for booking {BookingId} — skipping voucher email",
                evt.BookingId);
            return;
        }

        var idemp = new EmailIdempotencyLog
        {
            EventId = eventId,
            EmailType = EmailType.CarVoucher,
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
                EmailType.CarVoucher, eventId);
            return;
        }

        var model = new CarVoucherModel(
            BookingReference: evt.BookingReference,
            SupplierRef: evt.SupplierRef,
            VendorName: evt.VendorName,
            PickupLocation: evt.PickupLocation,
            DropoffLocation: evt.DropoffLocation,
            PickupAtUtc: evt.PickupAtUtc,
            DropoffAtUtc: evt.DropoffAtUtc,
            TotalAmount: evt.TotalAmount,
            Currency: evt.Currency,
            GuestEmail: evt.GuestEmail,
            GuestFullName: evt.GuestFullName,
            BrandName: _brand.BrandName,
            SupportPhone: _brand.SupportPhone);

        var rendered = await _renderer.RenderAsync(
            EmailType.CarVoucher,
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
                new EmailAttachment("car-voucher.pdf", "application/pdf", pdfBytes, "car-voucher.pdf")
            });

        var result = await _delivery.SendAsync(envelope, ctx.CancellationToken).ConfigureAwait(false);
        if (!result.Success)
        {
            throw new InvalidOperationException(
                $"SendGrid delivery failed for car booking {evt.BookingId}: {result.ErrorReason}");
        }

        idemp.ProviderMessageId = result.ProviderMessageId;
        await _db.SaveChangesAsync(ctx.CancellationToken).ConfigureAwait(false);
    }
}
