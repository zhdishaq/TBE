using QuestPDF.Fluent;
using QuestPDF.Infrastructure;
using TBE.NotificationService.API.Templates.Models;
using TBE.NotificationService.Application.Pdf;

namespace TBE.NotificationService.Infrastructure.Pdf;

/// <summary>
/// QuestPDF-backed implementation of <see cref="IHotelVoucherPdfGenerator"/>.
/// Returns a PDF byte array to be attached to the NOTF-02 hotel-voucher email.
/// Supplier reference is rendered prominently (HOTB-05 requirement).
/// </summary>
public sealed class HotelVoucherDocument : IHotelVoucherPdfGenerator
{
    static HotelVoucherDocument()
    {
        QuestPDF.Settings.License = LicenseType.Community;
        // TODO(prod): switch to LicenseType.Commercial before production launch.
        // QuestPDF Community license is valid only for companies with <$1M USD annual revenue.
    }

    public byte[] Generate(HotelVoucherModel m)
    {
        var nights = Math.Max(1, m.CheckOutDate.DayNumber - m.CheckInDate.DayNumber);

        return Document.Create(container =>
        {
            container.Page(page =>
            {
                page.Margin(30);
                page.Header()
                    .Text($"Hotel Voucher — {m.BrandName}")
                    .FontSize(18)
                    .Bold();

                page.Content().Column(col =>
                {
                    col.Spacing(8);

                    // Property block
                    col.Item().Text($"Property: {m.PropertyName}").Bold();
                    col.Item().Text(m.AddressLine);

                    // Stay block
                    col.Item().PaddingTop(10).Text("Stay").Bold();
                    col.Item().Text($"Check-in: {m.CheckInDate:yyyy-MM-dd}");
                    col.Item().Text($"Check-out: {m.CheckOutDate:yyyy-MM-dd}");
                    col.Item().Text($"Nights: {nights}");
                    col.Item().Text($"Rooms: {m.Rooms}");
                    col.Item().Text($"Guests: {m.Adults} adult(s), {m.Children} child(ren)");
                    col.Item().Text($"Lead guest: {m.GuestFullName}");

                    // Price block
                    col.Item().PaddingTop(10).Text("Price").Bold();
                    col.Item().Text($"Total: {m.TotalAmount:0.00} {m.Currency}");

                    // Supplier block (HOTB-05 — supplier_ref prominent)
                    col.Item().PaddingTop(10).Text("Supplier reference").Bold();
                    col.Item()
                        .Text(m.SupplierRef)
                        .FontFamily("Courier New")
                        .FontSize(14);
                    col.Item().Text($"Booking reference: {m.BookingReference}");
                });

                page.Footer()
                    .AlignCenter()
                    .Text($"Present this voucher at check-in. Support: {m.SupportPhone}");
            });
        }).GeneratePdf();
    }
}
