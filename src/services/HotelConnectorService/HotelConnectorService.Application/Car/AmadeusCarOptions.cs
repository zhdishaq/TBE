namespace TBE.HotelConnectorService.Application.Car;

public sealed class AmadeusCarOptions
{
    public string ApiKey { get; set; } = default!;
    public string ApiSecret { get; set; } = default!;
    // Transfer API is on the v1 base URL, not v2 (flights use v2)
    public string BaseUrl { get; set; } = "https://test.api.amadeus.com/v1";
    public string TokenUrl { get; set; } = "https://test.api.amadeus.com/v1/security/oauth2/token";
}
