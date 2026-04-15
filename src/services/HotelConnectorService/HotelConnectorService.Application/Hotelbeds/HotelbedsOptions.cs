namespace TBE.HotelConnectorService.Application.Hotelbeds;

public sealed class HotelbedsOptions
{
    public string ApiKey { get; set; } = default!;
    public string SharedSecret { get; set; } = default!;
    public string BaseUrl { get; set; } = "https://api.test.hotelbeds.com/hotel-api/1.0";
}
