using System.Text.Json.Serialization;
namespace TBE.FlightConnectorService.Application.Amadeus.Models;

public sealed class AmadeusFlightOffersResponse
{
    [JsonPropertyName("data")] public List<AmadeusFlightOffer> Data { get; init; } = [];
}
public sealed class AmadeusFlightOffer
{
    [JsonPropertyName("id")]                public string Id { get; init; } = default!;
    [JsonPropertyName("source")]            public string Source { get; init; } = default!;
    [JsonPropertyName("lastTicketingDate")] public string? LastTicketingDate { get; init; }
    [JsonPropertyName("price")]             public AmadeusPrice Price { get; init; } = default!;
    [JsonPropertyName("itineraries")]       public List<AmadeusItinerary> Itineraries { get; init; } = [];
    [JsonPropertyName("travelerPricings")]  public List<AmadeusTravelerPricing> TravelerPricings { get; init; } = [];
}
public sealed class AmadeusPrice
{
    [JsonPropertyName("currency")]   public string Currency { get; init; } = default!;
    [JsonPropertyName("grandTotal")] public string GrandTotal { get; init; } = default!;
    [JsonPropertyName("base")]       public string Base { get; init; } = default!;
    [JsonPropertyName("fees")]       public List<AmadeusFee> Fees { get; init; } = [];
    [JsonPropertyName("taxes")]      public List<AmadeusTax> Taxes { get; init; } = [];
}
public sealed class AmadeusTax
{
    [JsonPropertyName("amount")] public string Amount { get; init; } = default!;
    [JsonPropertyName("code")]   public string Code { get; init; } = default!;
}
public sealed class AmadeusFee
{
    [JsonPropertyName("amount")] public string Amount { get; init; } = default!;
    [JsonPropertyName("type")]   public string Type { get; init; } = default!;
}
public sealed class AmadeusItinerary
{
    [JsonPropertyName("segments")] public List<AmadeusSegment> Segments { get; init; } = [];
}
public sealed class AmadeusSegment
{
    [JsonPropertyName("departure")]     public AmadeusPoint Departure { get; init; } = default!;
    [JsonPropertyName("arrival")]       public AmadeusPoint Arrival { get; init; } = default!;
    [JsonPropertyName("carrierCode")]   public string CarrierCode { get; init; } = default!;
    [JsonPropertyName("number")]        public string Number { get; init; } = default!;
    [JsonPropertyName("aircraft")]      public AmadeusAircraft? Aircraft { get; init; }
    [JsonPropertyName("duration")]      public string Duration { get; init; } = default!; // ISO 8601 e.g. "PT2H30M"
}
public sealed class AmadeusPoint
{
    [JsonPropertyName("iataCode")] public string IataCode { get; init; } = default!;
    [JsonPropertyName("at")]       public DateTimeOffset At { get; init; }
}
public sealed class AmadeusAircraft { [JsonPropertyName("code")] public string Code { get; init; } = default!; }
public sealed class AmadeusTravelerPricing
{
    [JsonPropertyName("fareDetailsBySegment")] public List<AmadeusFareDetail> FareDetailsBySegment { get; init; } = [];
}
public sealed class AmadeusFareDetail
{
    [JsonPropertyName("cabin")]        public string Cabin { get; init; } = default!;
    [JsonPropertyName("fareBasis")]    public string FareBasis { get; init; } = default!;
}
