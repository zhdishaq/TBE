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
/// Plan 04-04 / PKG-03 / D-09 — single combined basket-confirmation email consumer.
/// One consumer class handles BOTH terminal outcomes:
/// <list type="bullet">
///   <item><see cref="BasketConfirmed"/> — full success, both legs ticketed / confirmed.</item>
///   <item><see cref="BasketPartiallyConfirmed"/> — D-09 partial failure with release-remainder.</item>
/// </list>
/// Both handlers converge on <see cref="HandleAsync"/> which inserts a single
/// <c>EmailIdempotencyLog</c> row keyed on <c>(EventId, EmailType.BasketConfirmation)</c>
/// per NOTF-06 — either outcome is "the basket email" from the customer's perspective,
/// so they share the same EmailType discriminator.
/// <para>
/// Full success: attach both flight e-ticket + hotel voucher PDF (two attachments).
/// Partial success: attach only the succeeded leg's PDF (one attachment).
/// Copy for the partial case explicitly tells the customer "ONE charge on your
/// statement" to pre-empt support tickets (D-09 single-statement-entry language).
/// </para>
/// </summary>
public sealed class BasketConfirmedConsumer :
    IConsumer<BasketConfirmed>,
    IConsumer<BasketPartiallyConfirmed>
{
    private readonly NotificationDbContext _db;
    private readonly IEmailTemplateRenderer _renderer;
    private readonly IETicketPdfGenerator _flightPdf;
    private readonly IHotelVoucherPdfGenerator _hotelPdf;
    private readonly IEmailDelivery _delivery;
    private readonly BrandOptions _brand;
    private readonly ILogger<BasketConfirmedConsumer> _log;

    public BasketConfirmedConsumer(
        NotificationDbContext db,
        IEmailTemplateRenderer renderer,
        IETicketPdfGenerator flightPdf,
        IHotelVoucherPdfGenerator hotelPdf,
        IEmailDelivery delivery,
        IOptions<BrandOptions> brand,
        ILogger<BasketConfirmedConsumer> log)
    {
        _db = db;
        _renderer = renderer;
        _flightPdf = flightPdf;
        _hotelPdf = hotelPdf;
        _delivery = delivery;
        _brand = brand.Value;
        _log = log;
    }

    public Task Consume(ConsumeContext<BasketConfirmed> ctx)
    {
        var evt = ctx.Message;
        var model = new BasketConfirmationModel(
            BrandName: _brand.BrandName,
            SupportPhone: _brand.SupportPhone,
            GuestFullName: evt.GuestFullName,
            GuestEmail: evt.GuestEmail,
            FlightSection: string.IsNullOrEmpty(evt.FlightBookingReference)
                ? null
                : new BasketConfirmationFlightSection(evt.FlightBookingReference, Pnr: evt.FlightBookingReference, ETicketNumber: null),
            HotelSection: string.IsNullOrEmpty(evt.HotelSupplierRef)
                ? null
                : new BasketConfirmationHotelSection(
                    BookingReference: evt.HotelSupplierRef,
                    SupplierRef: evt.HotelSupplierRef,
                    PropertyName: string.Empty,
                    CheckInDate: default,
                    CheckOutDate: default),
            CarSection: null,
            TotalAmount: evt.TotalAmount,
            ChargedAmount: evt.TotalAmount,
            RefundedAmount: 0m,
            Currency: evt.Currency,
            IsPartial: false,
            PartialFailureCause: null);

        return HandleAsync(
            eventId: ctx.MessageId ?? evt.EventId,
            basketId: evt.BasketId,
            recipientEmail: evt.GuestEmail,
            recipientName: evt.GuestFullName,
            model: model,
            attachFlight: !string.IsNullOrEmpty(evt.FlightBookingReference),
            attachHotel: !string.IsNullOrEmpty(evt.HotelSupplierRef),
            hotelVoucherModel: BuildHotelVoucherModelFromBasketConfirmed(evt),
            ct: ctx.CancellationToken);
    }

    public Task Consume(ConsumeContext<BasketPartiallyConfirmed> ctx)
    {
        var evt = ctx.Message;
        // D-09 partial success — one leg succeeded, the other failed. Only attach the
        // succeeded leg's PDF, render the alert block, and use the "ONE charge" copy.
        var flightSucceeded = string.Equals(evt.SucceededComponent, "Flight", StringComparison.OrdinalIgnoreCase);
        var hotelSucceeded = string.Equals(evt.SucceededComponent, "Hotel", StringComparison.OrdinalIgnoreCase);

        var model = new BasketConfirmationModel(
            BrandName: _brand.BrandName,
            SupportPhone: _brand.SupportPhone,
            GuestFullName: evt.GuestFullName,
            GuestEmail: evt.GuestEmail,
            FlightSection: (flightSucceeded && !string.IsNullOrEmpty(evt.FlightBookingReference))
                ? new BasketConfirmationFlightSection(evt.FlightBookingReference!, Pnr: evt.FlightBookingReference!, ETicketNumber: null)
                : null,
            HotelSection: (hotelSucceeded && !string.IsNullOrEmpty(evt.HotelSupplierRef))
                ? new BasketConfirmationHotelSection(
                    BookingReference: evt.HotelSupplierRef!,
                    SupplierRef: evt.HotelSupplierRef!,
                    PropertyName: string.Empty,
                    CheckInDate: default,
                    CheckOutDate: default)
                : null,
            CarSection: null,
            TotalAmount: evt.ChargedAmount + evt.RefundedAmount,
            ChargedAmount: evt.ChargedAmount,
            RefundedAmount: evt.RefundedAmount,
            Currency: evt.Currency,
            IsPartial: true,
            PartialFailureCause: $"{evt.FailedComponent} could not be booked: {evt.Cause}");

        return HandleAsync(
            eventId: ctx.MessageId ?? evt.EventId,
            basketId: evt.BasketId,
            recipientEmail: evt.GuestEmail,
            recipientName: evt.GuestFullName,
            model: model,
            attachFlight: flightSucceeded,
            attachHotel: hotelSucceeded,
            hotelVoucherModel: null, // partial-success hotel never has a voucher
            ct: ctx.CancellationToken);
    }

    /// <summary>
    /// Shared send path for full and partial outcomes. NOTF-06 idempotency via
    /// <c>EmailIdempotencyLog (EventId, EmailType.BasketConfirmation)</c> unique index —
    /// DbUpdateException on insert means "already delivered" and is acked silently.
    /// </summary>
    private async Task HandleAsync(
        Guid eventId,
        Guid basketId,
        string recipientEmail,
        string recipientName,
        BasketConfirmationModel model,
        bool attachFlight,
        bool attachHotel,
        HotelVoucherModel? hotelVoucherModel,
        CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(recipientEmail))
        {
            _log.LogWarning(
                "PKG-03: no GuestEmail on basket-confirmation event for basket {BasketId} — skipping email",
                basketId);
            return;
        }

        var idemp = new EmailIdempotencyLog
        {
            EventId = eventId,
            EmailType = EmailType.BasketConfirmation,
            BookingId = basketId,
            Recipient = recipientEmail,
            SentAtUtc = DateTime.UtcNow,
        };
        _db.EmailIdempotencyLogs.Add(idemp);

        try
        {
            await _db.SaveChangesAsync(ct).ConfigureAwait(false);
        }
        catch (DbUpdateException ex) when (IdempotencyHelpers.IsUniqueViolation(ex))
        {
            _log.LogInformation(
                "NOTF-06: duplicate {EmailType} for event {EventId} skipped",
                EmailType.BasketConfirmation, eventId);
            return;
        }

        var rendered = await _renderer.RenderAsync(
            EmailType.BasketConfirmation,
            model,
            ct).ConfigureAwait(false);

        var attachments = new List<EmailAttachment>();
        if (attachFlight && model.FlightSection is not null)
        {
            var pdfBytes = _flightPdf.Generate(new ETicketDocumentModel(
                PassengerName: recipientName,
                ETicketNumber: model.FlightSection.ETicketNumber ?? model.FlightSection.Pnr,
                Pnr: model.FlightSection.Pnr,
                FlightNumber: "TBA",
                Origin: "TBA",
                Destination: "TBA",
                DepartureUtc: DateTime.UtcNow,
                ArrivalUtc: DateTime.UtcNow,
                FareClass: "Y",
                SeatNumber: "TBA"));
            attachments.Add(new EmailAttachment("eticket.pdf", "application/pdf", pdfBytes, "eticket.pdf"));
        }
        if (attachHotel && hotelVoucherModel is not null)
        {
            var pdfBytes = _hotelPdf.Generate(hotelVoucherModel);
            attachments.Add(new EmailAttachment("voucher.pdf", "application/pdf", pdfBytes, "voucher.pdf"));
        }

        var envelope = new EmailEnvelope(
            ToEmail: recipientEmail,
            ToName: recipientName,
            Subject: rendered.Subject,
            HtmlBody: rendered.HtmlBody,
            PlainTextBody: rendered.PlainTextBody,
            Attachments: attachments.ToArray());

        var result = await _delivery.SendAsync(envelope, ct).ConfigureAwait(false);
        if (!result.Success)
        {
            throw new InvalidOperationException(
                $"SendGrid delivery failed for basket {basketId}: {result.ErrorReason}");
        }

        idemp.ProviderMessageId = result.ProviderMessageId;
        await _db.SaveChangesAsync(ct).ConfigureAwait(false);
    }

    private HotelVoucherModel? BuildHotelVoucherModelFromBasketConfirmed(BasketConfirmed evt)
    {
        if (string.IsNullOrEmpty(evt.HotelSupplierRef)) return null;
        // Minimal model for the PDF — the full hotel-details fetch lives in a future plan;
        // for Phase 4 basket confirmations we attach a summary voucher keyed by SupplierRef.
        return new HotelVoucherModel(
            BookingReference: evt.HotelSupplierRef,
            SupplierRef: evt.HotelSupplierRef,
            PropertyName: "Hotel reservation",
            AddressLine: string.Empty,
            CheckInDate: default,
            CheckOutDate: default,
            Rooms: 1,
            Adults: 1,
            Children: 0,
            TotalAmount: evt.TotalAmount,
            Currency: evt.Currency,
            GuestEmail: evt.GuestEmail,
            GuestFullName: evt.GuestFullName,
            BrandName: _brand.BrandName,
            SupportPhone: _brand.SupportPhone);
    }
}
