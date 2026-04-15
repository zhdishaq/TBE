using System.Text.Json.Serialization;

namespace TBE.HotelConnectorService.Application.Car.Models;

// Amadeus Transfer Search: GET /v1/shopping/availability/transfer-offers
// Response structure is ASSUMED — verify against Amadeus API explorer once credentials obtained
public sealed class AmadeusTransferOffersResponse
{
    [JsonPropertyName("data")] public List<AmadeusTransferOffer> Data { get; init; } = [];
}

public sealed class AmadeusTransferOffer
{
    [JsonPropertyName("id")]               public string Id { get; init; } = default!;
    [JsonPropertyName("transferType")]     public string TransferType { get; init; } = default!;  // "PRIVATE" | "SHARED"
    [JsonPropertyName("vehicle")]          public AmadeusTransferVehicle Vehicle { get; init; } = default!;
    [JsonPropertyName("serviceProvider")]  public AmadeusServiceProvider ServiceProvider { get; init; } = default!;
    [JsonPropertyName("quotation")]        public AmadeusTransferQuotation Quotation { get; init; } = default!;
    [JsonPropertyName("methodsOfPayment")] public List<string> MethodsOfPayment { get; init; } = [];
}

public sealed class AmadeusTransferVehicle
{
    [JsonPropertyName("code")]        public string Code { get; init; } = default!;  // e.g. "CAT_LUXURY"
    [JsonPropertyName("description")] public string Description { get; init; } = default!;
    [JsonPropertyName("seats")]       public List<AmadeusVehicleSeat> Seats { get; init; } = [];
}

public sealed class AmadeusVehicleSeat
{
    [JsonPropertyName("count")] public int Count { get; init; }
}

public sealed class AmadeusServiceProvider
{
    [JsonPropertyName("code")]    public string Code { get; init; } = default!;
    [JsonPropertyName("name")]    public string Name { get; init; } = default!;
    [JsonPropertyName("logoUrl")] public string? LogoUrl { get; init; }
}

public sealed class AmadeusTransferQuotation
{
    [JsonPropertyName("currencyCode")] public string CurrencyCode { get; init; } = default!;
    [JsonPropertyName("base")]         public AmadeusTransferAmount Base { get; init; } = default!;
    [JsonPropertyName("totalAmount")]  public AmadeusTransferAmount TotalAmount { get; init; } = default!;
    [JsonPropertyName("taxes")]        public List<AmadeusTransferTax> Taxes { get; init; } = [];
}

public sealed class AmadeusTransferAmount
{
    [JsonPropertyName("monetaryAmount")] public string MonetaryAmount { get; init; } = default!;
}

public sealed class AmadeusTransferTax
{
    [JsonPropertyName("monetaryAmount")] public string MonetaryAmount { get; init; } = default!;
}
