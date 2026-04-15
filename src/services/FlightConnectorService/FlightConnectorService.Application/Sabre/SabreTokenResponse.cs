using System.Text.Json.Serialization;
namespace TBE.FlightConnectorService.Application.Sabre;
internal sealed class SabreTokenResponse
{
    [JsonPropertyName("access_token")] public string AccessToken { get; init; } = default!;
    [JsonPropertyName("expires_in")]   public int ExpiresIn { get; init; }
}
