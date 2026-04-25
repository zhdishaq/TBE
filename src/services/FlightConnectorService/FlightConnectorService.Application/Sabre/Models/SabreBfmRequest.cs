using System.Text.Json.Serialization;
namespace TBE.FlightConnectorService.Application.Sabre.Models;

public sealed class SabreBfmRequest
{
    [JsonPropertyName("OTA_AirLowFareSearchRQ")]
    public SabreOtaRequest OtaRequest { get; init; } = default!;
}

public sealed class SabreOtaRequest
{
    [JsonPropertyName("Version")]
    public string Version { get; init; } = "5";

    [JsonPropertyName("POS")]
    public SabrePos Pos { get; init; } = default!;

    [JsonPropertyName("OriginDestinationInformation")]
    public List<SabreOriginDest> OriginDestinationInformation { get; init; } = [];

    [JsonPropertyName("TravelPreferences")]
    public SabreTravelPreferences? TravelPreferences { get; init; }

    [JsonPropertyName("TravelerInfoSummary")]
    public SabreTravelerInfo TravelerInfoSummary { get; init; } = default!;

    [JsonPropertyName("TPA_Extensions")]
    public SabreTpaExtensions TpaExtensions { get; init; } = new();
}

// ── POS (Point of Sale) ───────────────────────────────────────
public sealed class SabrePos
{
    [JsonPropertyName("Source")]
    public List<SabrePosSource> Source { get; init; } = [];
}

public sealed class SabrePosSource
{
    [JsonPropertyName("PseudoCityCode")]
    public string PseudoCityCode { get; init; } = default!;

    [JsonPropertyName("RequestorID")]
    public SabreRequestorId RequestorId { get; init; } = default!;
}

public sealed class SabreRequestorId
{
    [JsonPropertyName("Type")]
    public string Type { get; init; } = "1";

    [JsonPropertyName("ID")]
    public string Id { get; init; } = "1";

    [JsonPropertyName("CompanyName")]
    public SabreCompanyName CompanyName { get; init; } = new();
}

public sealed class SabreCompanyName
{
    [JsonPropertyName("Code")]
    public string Code { get; init; } = "TN";
}

// ── Origin / Destination ──────────────────────────────────────
public sealed class SabreOriginDest
{
    [JsonPropertyName("RPH")]
    public string Rph { get; init; } = "1";

    [JsonPropertyName("DepartureDateTime")]
    public string DepartureDateTime { get; init; } = default!;

    [JsonPropertyName("OriginLocation")]
    public SabreLocation OriginLocation { get; init; } = default!;

    [JsonPropertyName("DestinationLocation")]
    public SabreLocation DestinationLocation { get; init; } = default!;
}

public sealed class SabreLocation
{
    [JsonPropertyName("LocationCode")]
    public string LocationCode { get; init; } = default!;
}

// ── Travel Preferences ────────────────────────────────────────
public sealed class SabreTravelPreferences
{
    [JsonPropertyName("MaxStopsQuantity")]
    public int? MaxStopsQuantity { get; init; }

    [JsonPropertyName("VendorPref")]
    public List<SabreVendorPref>? VendorPref { get; init; }
}

public sealed class SabreVendorPref
{
    [JsonPropertyName("Code")]
    public string Code { get; init; } = default!;
}

// ── Traveler Info ─────────────────────────────────────────────
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
    [JsonPropertyName("Code")]
    public string Code { get; init; } = default!;  // ADT | CNN | INF

    [JsonPropertyName("Quantity")]
    public int Quantity { get; init; }
}

// ── TPA Extensions ────────────────────────────────────────────
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

public sealed class SabreRequestType
{
    [JsonPropertyName("Name")]
    public string Name { get; init; } = "200ITINS";
}
