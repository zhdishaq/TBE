namespace TBE.NotificationService.Application.Email;

/// <summary>
/// SendGrid-agnostic email delivery abstraction. NOTF-01 delivery backbone.
/// </summary>
public interface IEmailDelivery
{
    Task<EmailDeliveryResult> SendAsync(EmailEnvelope envelope, CancellationToken ct);
}

public sealed record EmailDeliveryResult(
    bool Success,
    string? ProviderMessageId,
    string? ErrorReason);
