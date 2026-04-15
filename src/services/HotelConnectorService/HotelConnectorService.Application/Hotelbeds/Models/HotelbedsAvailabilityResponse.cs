using System.Text.Json.Serialization;

namespace TBE.HotelConnectorService.Application.Hotelbeds.Models;

public sealed class HotelbedsAvailabilityResponse
{
    [JsonPropertyName("hotels")] public HotelbedsHotelsContainer Hotels { get; init; } = default!;
}

public sealed class HotelbedsHotelsContainer
{
    [JsonPropertyName("hotels")] public List<HotelbedsHotel> Hotels { get; init; } = [];
}

public sealed class HotelbedsHotel
{
    [JsonPropertyName("code")]      public int Code { get; init; }
    [JsonPropertyName("name")]      public string Name { get; init; } = default!;
    [JsonPropertyName("rooms")]     public List<HotelbedsRoom> Rooms { get; init; } = [];
}

public sealed class HotelbedsRoom
{
    [JsonPropertyName("code")]  public string Code { get; init; } = default!;
    [JsonPropertyName("name")]  public string Name { get; init; } = default!;
    [JsonPropertyName("rates")] public List<HotelbedsRate> Rates { get; init; } = [];
}

public sealed class HotelbedsRate
{
    [JsonPropertyName("rateKey")]              public string RateKey { get; init; } = default!;
    [JsonPropertyName("net")]                  public string Net { get; init; } = default!;
    [JsonPropertyName("currency")]             public string Currency { get; init; } = default!;
    [JsonPropertyName("rateType")]             public string RateType { get; init; } = default!;  // "RECHECK" | "BOOKABLE"
    [JsonPropertyName("cancellationPolicies")] public List<HotelbedsCancellationPolicy> CancellationPolicies { get; init; } = [];
}

public sealed class HotelbedsCancellationPolicy
{
    [JsonPropertyName("amount")] public string Amount { get; init; } = default!;
    [JsonPropertyName("from")]   public string From { get; init; } = default!;  // ISO date
}
