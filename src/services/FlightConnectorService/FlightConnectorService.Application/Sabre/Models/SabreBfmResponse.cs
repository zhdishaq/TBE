using System.Text.Json.Serialization;
namespace TBE.FlightConnectorService.Application.Sabre.Models;

// NOTE: Sabre BFM REST response structure is ASSUMED from research.
// Verify against actual Sabre dev portal once credentials are obtained.
// All parsing is in SabreFlightProvider.MapItinerary — update only that method when structure is confirmed.
public sealed class SabreBfmResponse
{
    [JsonPropertyName("groupedItineraryResponse")]
    public SabreGroupedItineraryResponse? GroupedItinerary { get; init; }
}
public sealed class SabreGroupedItineraryResponse
{
    [JsonPropertyName("itineraryGroups")]
    public List<SabreItineraryGroup> ItineraryGroups { get; init; } = [];
    [JsonPropertyName("scheduleDescs")]
    public List<SabreScheduleDesc> ScheduleDescs { get; init; } = [];
    [JsonPropertyName("taxDescs")]
    public List<SabreTaxDesc> TaxDescs { get; init; } = [];
    [JsonPropertyName("fareComponentDescs")]
    public List<SabreFareComponentDesc> FareComponentDescs { get; init; } = [];
}
public sealed class SabreItineraryGroup
{
    [JsonPropertyName("itineraries")] public List<SabreItinerary> Itineraries { get; init; } = [];
}
public sealed class SabreItinerary
{
    [JsonPropertyName("id")]          public int Id { get; init; }
    [JsonPropertyName("pricingInformation")] public List<SabrePricingInfo> PricingInformation { get; init; } = [];
    [JsonPropertyName("legs")]        public List<SabreLeg> Legs { get; init; } = [];
}
public sealed class SabrePricingInfo
{
    [JsonPropertyName("fare")]        public SabreFare Fare { get; init; } = default!;
    [JsonPropertyName("taxes")]       public List<SabreTaxRef> Taxes { get; init; } = [];
}
public sealed class SabreFare
{
    [JsonPropertyName("totalFare")]   public SabreMonetary TotalFare { get; init; } = default!;
    [JsonPropertyName("passengerInfoList")] public List<SabrePassengerInfo> PassengerInfoList { get; init; } = [];
}
public sealed class SabreMonetary
{
    [JsonPropertyName("amount")]      public decimal Amount { get; init; }
    [JsonPropertyName("currency")]    public string Currency { get; init; } = default!;
}
public sealed class SabrePassengerInfo
{
    [JsonPropertyName("passengerInfo")] public SabrePassengerInfoDetail PassengerInfo { get; init; } = default!;
}
public sealed class SabrePassengerInfoDetail
{
    [JsonPropertyName("fareComponents")] public List<SabreFareComponentRef> FareComponents { get; init; } = [];
}
public sealed class SabreFareComponentRef
{
    [JsonPropertyName("ref")] public int Ref { get; init; }
}
public sealed class SabreLeg
{
    [JsonPropertyName("schedules")] public List<SabreScheduleRef> Schedules { get; init; } = [];
}
public sealed class SabreScheduleRef { [JsonPropertyName("ref")] public int Ref { get; init; } }
public sealed class SabreScheduleDesc
{
    [JsonPropertyName("id")]          public int Id { get; init; }
    [JsonPropertyName("departure")]   public SabreEndpoint Departure { get; init; } = default!;
    [JsonPropertyName("arrival")]     public SabreEndpoint Arrival { get; init; } = default!;
    [JsonPropertyName("carrier")]     public SabreCarrier Carrier { get; init; } = default!;
    [JsonPropertyName("elapsedTime")] public int ElapsedTime { get; init; }  // minutes
}
public sealed class SabreEndpoint
{
    [JsonPropertyName("airport")]  public string Airport { get; init; } = default!;
    [JsonPropertyName("time")]     public DateTimeOffset Time { get; init; }
}
public sealed class SabreCarrier
{
    [JsonPropertyName("marketing")]       public string Marketing { get; init; } = default!;
    [JsonPropertyName("marketingFlightNumber")] public int MarketingFlightNumber { get; init; }
    [JsonPropertyName("equipment")]       public SabreEquipment? Equipment { get; init; }
}
public sealed class SabreEquipment { [JsonPropertyName("code")] public string Code { get; init; } = default!; }
public sealed class SabreTaxDesc
{
    [JsonPropertyName("id")]     public int Id { get; init; }
    [JsonPropertyName("code")]   public string Code { get; init; } = default!;
    [JsonPropertyName("amount")] public decimal Amount { get; init; }
}
public sealed class SabreTaxRef { [JsonPropertyName("ref")] public int Ref { get; init; } }
public sealed class SabreFareComponentDesc
{
    [JsonPropertyName("id")]     public int Id { get; init; }
    [JsonPropertyName("cabin")]  public string? Cabin { get; init; }
}
