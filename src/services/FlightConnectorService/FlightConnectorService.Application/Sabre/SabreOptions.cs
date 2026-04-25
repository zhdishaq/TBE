namespace TBE.FlightConnectorService.Application.Sabre;

public sealed class SabreOptions
{
    public string ClientId     { get; set; } = default!;
    public string ClientSecret { get; set; } = default!;
    public string PseudoCityCode { get; set; } = "XXXX"; // Replace with your actual PCC from Sabre
    public string BaseUrl      { get; set; } = "https://api.cert.platform.sabre.com";
    public string TokenUrl     { get; set; } = "https://api.cert.platform.sabre.com/v2/auth/token";
}
