namespace TBE.NotificationService.Application.Email;

/// <summary>
/// Brand copy surfaced in outbound transactional emails and PDFs. Bound from
/// <c>Branding:*</c> in configuration. Safe defaults let the service boot even
/// when config is absent (early-phase developer experience).
/// </summary>
public sealed class BrandOptions
{
    public string BrandName { get; set; } = "TBE Travel";
    public string SupportPhone { get; set; } = "+44 20 0000 0000";
}
