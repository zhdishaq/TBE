namespace TBE.FlightConnectorService.Application.Sabre;
public sealed class SabreOptions
{
    public string ClientId { get; set; } = default!;
    public string ClientSecret { get; set; } = default!;
    public string BaseUrl { get; set; } = "https://api.havail.sabre.com";
    public string TokenUrl { get; set; } = "https://api.havail.sabre.com/v2/auth/token";
}
