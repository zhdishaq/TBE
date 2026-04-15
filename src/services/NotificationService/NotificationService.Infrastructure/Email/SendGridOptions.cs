namespace TBE.NotificationService.Infrastructure.Email;

public sealed class SendGridOptions
{
    public string ApiKey { get; set; } = "";
    public string FromEmail { get; set; } = "no-reply@tbe.travel";
    public string FromName { get; set; } = "TBE Bookings";
    /// <summary>"true" routes through SendGrid sandbox (no real delivery) for dev.</summary>
    public string SandboxMode { get; set; } = "false";
}
