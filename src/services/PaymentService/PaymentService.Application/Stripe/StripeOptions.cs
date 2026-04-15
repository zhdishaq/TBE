namespace TBE.PaymentService.Application.Stripe;

/// <summary>
/// Stripe configuration. Values are always sourced from environment variables
/// (<c>Stripe__ApiKey</c>, <c>Stripe__WebhookSecret</c>, <c>Stripe__PublishableKey</c>) —
/// committed appsettings.json MUST keep these as empty strings.
/// </summary>
public sealed class StripeOptions
{
    public string ApiKey { get; set; } = string.Empty;
    public string PublishableKey { get; set; } = string.Empty;
    public string WebhookSecret { get; set; } = string.Empty;
    public string DefaultCurrency { get; set; } = "GBP";
}
