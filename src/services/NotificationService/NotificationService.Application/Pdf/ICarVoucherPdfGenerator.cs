using TBE.NotificationService.API.Templates.Models;

namespace TBE.NotificationService.Application.Pdf;

/// <summary>
/// Generates a car-hire voucher PDF byte array. CARB-03 attachment backbone.
/// QuestPDF implementation lives in <c>NotificationService.Infrastructure.Pdf</c>.
/// </summary>
public interface ICarVoucherPdfGenerator
{
    byte[] Generate(CarVoucherModel model);
}
