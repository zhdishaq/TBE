using System.Text.Json.Serialization;

namespace TBE.HotelConnectorService.Application.Hotelbeds.Models;

public sealed class HotelbedsAvailabilityRequest
{
    [JsonPropertyName("stay")]          public HotelbedsStay Stay { get; init; } = default!;
    [JsonPropertyName("occupancies")]   public List<HotelbedsOccupancy> Occupancies { get; init; } = [];
    [JsonPropertyName("destination")]   public HotelbedsDestination Destination { get; init; } = default!;
}

public sealed class HotelbedsStay
{
    [JsonPropertyName("checkIn")]  public string CheckIn { get; init; } = default!;   // yyyy-MM-dd
    [JsonPropertyName("checkOut")] public string CheckOut { get; init; } = default!;
}

public sealed class HotelbedsOccupancy
{
    [JsonPropertyName("rooms")]    public int Rooms { get; init; } = 1;
    [JsonPropertyName("adults")]   public int Adults { get; init; } = 2;
    [JsonPropertyName("children")] public int Children { get; init; }
}

public sealed class HotelbedsDestination
{
    [JsonPropertyName("code")] public string Code { get; init; } = default!;  // IATA city code
}
