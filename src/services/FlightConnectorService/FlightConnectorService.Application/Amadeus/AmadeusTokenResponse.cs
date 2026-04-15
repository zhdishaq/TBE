using System.Text.Json.Serialization;
namespace TBE.FlightConnectorService.Application.Amadeus;
internal sealed class AmadeusTokenResponse
{
    [JsonPropertyName("access_token")] public string AccessToken { get; init; } = default!;
    [JsonPropertyName("expires_in")]   public int ExpiresIn { get; init; }
    [JsonPropertyName("token_type")]   public string TokenType { get; init; } = default!;
}
