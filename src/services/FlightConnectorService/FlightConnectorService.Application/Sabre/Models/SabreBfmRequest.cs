using System.Text.Json.Serialization;
namespace TBE.FlightConnectorService.Application.Sabre.Models;
public sealed class SabreBfmRequest
{
    [JsonPropertyName("OTA_AirLowFareSearchRQ")] public SabreOtaRequest OtaRequest { get; init; } = default!;
}
public sealed class SabreOtaRequest
{
    [JsonPropertyName("Version")]
    public string Version { get; init; } = "3.4.0";

    [JsonPropertyName("OriginDestinationInformation")]
    public List<SabreOriginDest> OriginDestinationInformation { get; init; } = [];

    [JsonPropertyName("TravelerInfoSummary")]
    public SabreTravelerInfo TravelerInfoSummary { get; init; } = default!;

    [JsonPropertyName("TPA_Extensions")]
    public SabreTpaExtensions TpaExtensions { get; init; } = new();
}
public sealed class SabreOriginDest
{
    [JsonPropertyName("RPH")]               public string Rph { get; init; } = "1";
    [JsonPropertyName("DepartureDateTime")] public string DepartureDateTime { get; init; } = default!;
    [JsonPropertyName("OriginLocation")]    public SabreLocation OriginLocation { get; init; } = default!;
    [JsonPropertyName("DestinationLocation")] public SabreLocation DestinationLocation { get; init; } = default!;
}
public sealed class SabreLocation { [JsonPropertyName("LocationCode")] public string LocationCode { get; init; } = default!; }
public sealed class SabreTravelerInfo
{
    [JsonPropertyName("SeatsRequested")]
    public List<int> SeatsRequested { get; init; } = [];

    [JsonPropertyName("AirTravelerAvail")]
    public List<SabreTravelerAvail> AirTravelerAvail { get; init; } = [];
}
public sealed class SabreTravelerAvail
{
    [JsonPropertyName("PassengerTypeQuantity")]
    public List<SabrePtq> PassengerTypeQuantity { get; init; } = [];
}
public sealed class SabrePtq
{
    [JsonPropertyName("Code")]     public string Code { get; init; } = default!;  // ADT | CNN | INF
    [JsonPropertyName("Quantity")] public int Quantity { get; init; }
}
public sealed class SabreTpaExtensions
{
    [JsonPropertyName("IntelliSellTransaction")]
    public SabreIntelliSell IntelliSellTransaction { get; init; } = new();
}
public sealed class SabreIntelliSell
{
    [JsonPropertyName("RequestType")]
    public SabreRequestType RequestType { get; init; } = new();
}
public sealed class SabreRequestType { [JsonPropertyName("Name")] public string Name { get; init; } = "200ITINS"; }
