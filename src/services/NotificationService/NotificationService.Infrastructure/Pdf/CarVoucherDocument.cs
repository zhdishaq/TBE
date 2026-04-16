using QuestPDF.Fluent;
using QuestPDF.Infrastructure;
using TBE.NotificationService.API.Templates.Models;
using TBE.NotificationService.Application.Pdf;

namespace TBE.NotificationService.Infrastructure.Pdf;

/// <summary>
/// QuestPDF-backed implementation of <see cref="ICarVoucherPdfGenerator"/>.
/// Returns a PDF byte array to be attached to the NOTF-02 car-voucher email (CARB-03).
/// Supplier reference is rendered prominently for the rental-counter pickup workflow.
/// </summary>
public sealed class CarVoucherDocument : ICarVoucherPdfGenerator
{
    static CarVoucherDocument()
    {
        QuestPDF.Settings.License = LicenseType.Community;
        // TODO(prod): switch to LicenseType.Commercial before production launch.
        // QuestPDF Community license is valid only for companies with <$1M USD annual revenue.
    }

    public byte[] Generate(CarVoucherModel m)
    {
        return Document.Create(container =>
        {
            container.Page(page =>
            {
                page.Margin(30);
                page.Header()
                    .Text($"Car Hire Voucher — {m.BrandName}")
                    .FontSize(18)
                    .Bold();

                page.Content().Column(col =>
                {
                    col.Spacing(8);

                    // Vendor block
                    col.Item().Text($"Vendor: {m.VendorName}").Bold();

                    // Pickup block
                    col.Item().PaddingTop(10).Text("Pick-up").Bold();
                    col.Item().Text($"Location: {m.PickupLocation}");
                    col.Item().Text($"Time: {m.PickupAtUtc:yyyy-MM-dd HH:mm} UTC");

                    // Dropoff block
                    col.Item().PaddingTop(10).Text("Drop-off").Bold();
                    col.Item().Text($"Location: {m.DropoffLocation}");
                    col.Item().Text($"Time: {m.DropoffAtUtc:yyyy-MM-dd HH:mm} UTC");

                    // Driver block
                    col.Item().PaddingTop(10).Text("Driver").Bold();
                    col.Item().Text($"Lead driver: {m.GuestFullName}");

                    // Price block
                    col.Item().PaddingTop(10).Text("Price").Bold();
                    col.Item().Text($"Total: {m.TotalAmount:0.00} {m.Currency}");

                    // Supplier block — prominent for counter check-in
                    col.Item().PaddingTop(10).Text("Supplier reference").Bold();
                    col.Item()
                        .Text(m.SupplierRef)
                        .FontFamily("Courier New")
                        .FontSize(14);
                    col.Item().Text($"Booking reference: {m.BookingReference}");
                });

                page.Footer()
                    .AlignCenter()
                    .Text($"Present this voucher and your driver's license at pickup. Support: {m.SupportPhone}");
            });
        }).GeneratePdf();
    }
}
