using TBE.NotificationService.API.Templates.Models;

namespace TBE.NotificationService.Application.Pdf;

/// <summary>
/// Generates a hotel-voucher PDF byte array. NOTF-02 attachment backbone.
/// QuestPDF implementation lives in <c>NotificationService.Infrastructure.Pdf</c>.
/// </summary>
public interface IHotelVoucherPdfGenerator
{
    byte[] Generate(HotelVoucherModel model);
}
