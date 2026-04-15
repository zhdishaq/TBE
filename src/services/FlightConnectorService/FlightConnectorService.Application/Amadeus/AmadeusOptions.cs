namespace TBE.FlightConnectorService.Application.Amadeus;
public sealed class AmadeusOptions
{
    public string ApiKey { get; set; } = default!;
    public string ApiSecret { get; set; } = default!;
    public string BaseUrl { get; set; } = "https://test.api.amadeus.com/v2";
    public string TokenUrl { get; set; } = "https://test.api.amadeus.com/v1/security/oauth2/token";
}
